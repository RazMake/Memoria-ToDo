using System.Security.Cryptography;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// The shared fake secure key storage. A store made with <c>new</c> has a constant public key and cannot sign, which
/// costs nothing. A store made with <see cref="Signing"/> uses one immutable RSA key, made once per process on first
/// use, so a transport can sign for real.
/// It counts generated and opened handles and how many are still open, so tests can check handle lifetime.
/// </summary>
public sealed class FakeDeviceKeyStore : IDeviceKeyStore
{
    private readonly object gate = new();
    // One RSA object is shared by every signing store, and it isn't safe to export or sign from several threads at once.
    private static readonly object RsaGate = new();
    private static readonly Lazy<RSA> Shared = new(() => RSA.Create(2048));
    // A well-formed ssh-rsa public key (exponent 65537) that no private key matches.
    private static readonly Lazy<byte[]> ConstantBlob = new(() => BlobOf(new byte[] { 1, 0, 1 }, Enumerable.Range(1, 256).Select(i => (byte)(i | 0x81)).ToArray()));
    private readonly bool signs;
    private bool hasKey;
    private int generateCalls;
    private int openCalls;
    private int openHandles;

    /// <summary>A store whose key really signs, for tests that run a transport against a real SSH server.</summary>
    public static FakeDeviceKeyStore Signing() => new(signs: true);

    public FakeDeviceKeyStore() : this(signs: false)
    {
    }

    private FakeDeviceKeyStore(bool signs) => this.signs = signs;

    private byte[] PublicBlob => signs ? Blob(Shared.Value) : ConstantBlob.Value;

    /// <summary>When true, <see cref="Open"/> fails with an <see cref="IOException"/>, as an unusable stored key does.</summary>
    public bool FailOpen { get; set; }

    /// <summary>When set, <see cref="Open"/> throws it (after counting the call) once a key is stored.</summary>
    public Exception? OpenFailure { get; set; }

    /// <summary>When set, reading <see cref="HasKey"/> throws it, as an unreadable key location does.</summary>
    public Exception? HasKeyFailure { get; set; }

    /// <summary>When set, <see cref="Generate"/> throws it and stores no key, as an unwritable key location does.</summary>
    public Exception? GenerateFailure { get; set; }

    public int GenerateCalls => Volatile.Read(ref generateCalls);

    public int OpenCalls => Volatile.Read(ref openCalls);

    /// <summary>Handles returned and not yet disposed.</summary>
    public int OpenHandles => Volatile.Read(ref openHandles);

    /// <inheritdoc />
    public bool HasKey
    {
        get { lock (gate) { return HasKeyFailure is null ? hasKey : throw HasKeyFailure; } }
    }

    /// <summary>The stored key's SSH public-key blob; fails when there is no key.</summary>
    public byte[] PublicKeyBlob
    {
        get { lock (gate) { return hasKey ? PublicBlob : throw new InvalidOperationException("No key is stored."); } }
    }

    /// <summary>The SSH public-key blob, as the SSH wire format writes an RSA key.</summary>
    public static byte[] Blob(RSA rsa)
    {
        RSAParameters parameters;
        lock (RsaGate)
            parameters = rsa.ExportParameters(false);

        return BlobOf(parameters.Exponent!, parameters.Modulus!);
    }

    private static byte[] BlobOf(byte[] exponent, byte[] modulus)
    {
        using var stream = new MemoryStream();
        WriteString(stream, "ssh-rsa"u8);
        WriteString(stream, Mpint(exponent));
        WriteString(stream, Mpint(modulus));
        return stream.ToArray();
    }

    /// <summary>Stores a key, as if an earlier run generated it, without counting a generate call.</summary>
    public void SeedKey()
    {
        lock (gate) { hasKey = true; }
    }

    /// <inheritdoc />
    public IDeviceSigningKey Generate()
    {
        lock (gate)
        {
            if (hasKey)
                throw new InvalidOperationException("A device key already exists.");
            if (GenerateFailure is not null)
                throw GenerateFailure;
            hasKey = true;
            Interlocked.Increment(ref generateCalls);
            return Handle();
        }
    }

    /// <inheritdoc />
    public IDeviceSigningKey Open()
    {
        lock (gate)
        {
            Interlocked.Increment(ref openCalls);
            if (!hasKey)
                throw new InvalidOperationException("No device key is stored.");
            if (OpenFailure is not null)
                throw OpenFailure;
            return FailOpen ? throw new IOException("The stored key can't be opened.") : Handle();
        }
    }

    private SigningHandle Handle()
    {
        Interlocked.Increment(ref openHandles);
        return new SigningHandle(signs ? Shared.Value : null, PublicBlob, () =>
        {
            Interlocked.Decrement(ref openHandles);
            OnHandleDisposed?.Invoke();
        });
    }

    /// <summary>Runs each time a handle is disposed, so a test can record what was true at that moment.</summary>
    public Action? OnHandleDisposed { get; set; }

    private static byte[] Mpint(byte[] unsigned)
    {
        var trimmed = unsigned.SkipWhile(b => b == 0).ToArray();
        return trimmed.Length > 0 && trimmed[0] >= 0x80 ? [0, .. trimmed] : trimmed;
    }

    private static void WriteString(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }

    private sealed class SigningHandle(RSA? rsa, byte[] blob, Action released) : IDeviceSigningKey
    {
        private int disposed;

        public ReadOnlyMemory<byte> PublicKeyBlob => blob;

        public byte[] Sign(ReadOnlySpan<byte> data, RsaSignatureAlgorithm algorithm)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            if (rsa is null)
                throw new NotSupportedException("Make the store with FakeDeviceKeyStore.Signing() to sign.");
            var hash = algorithm == RsaSignatureAlgorithm.Sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512;
            lock (RsaGate)
                return rsa.SignData(data, hash, RSASignaturePadding.Pkcs1);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                released();
        }
    }
}
