using System.Diagnostics;
using System.Security.Cryptography;
using Android.Runtime;
using Android.Security.Keystore;
using Java.Security;
using Java.Security.Interfaces;
using Renci.SshNet;
using Renci.SshNet.Security;

namespace Memoria.Proofs;

/// <summary>
/// An RSA 3072 key held in the Android Keystore, offered to SSH.NET with SHA-2 signatures only.
/// The Keystore never hands out the private key, so each signature is made by the Keystore.
/// </summary>
public sealed class KeystoreRsaKey : IPrivateKeySource
{
    private const string Alias = "memoria-proof-ssh";
    private const string Provider = "AndroidKeyStore";
    private readonly IPrivateKey privateKey;

    private KeystoreRsaKey(IPrivateKey privateKey, IRSAPublicKey publicKey)
    {
        this.privateKey = privateKey;
        PublicKeyBlob = OpenSshRsa.PublicKeyBlob(new RSAParameters
        {
            Modulus = publicKey.Modulus!.ToByteArray(),
            Exponent = publicKey.PublicExponent!.ToByteArray(),
        });
        HostKeyAlgorithms =
        [
            new Algorithm("rsa-sha2-512", "SHA512withRSA", this),
            new Algorithm("rsa-sha2-256", "SHA256withRSA", this),
        ];
    }

    public IReadOnlyCollection<HostAlgorithm> HostKeyAlgorithms { get; }

    public byte[] PublicKeyBlob { get; }

    public string PublicKeyLine =>
        OpenSshRsa.PublicKeyLine(PublicKeyBlob, $"memoria-proof-{Android.OS.Build.Model?.Replace(' ', '-')}");

    public string Fingerprint => SshTransport.Fingerprint(PublicKeyBlob);

    /// <summary>False when the Keystore refuses to hand out the private key, as it should.</summary>
    public bool PrivateKeyReadable => privateKey.GetEncoded() is not null;

    public string KeySize => $"{privateKey.JavaCast<IRSAKey>()!.Modulus!.BitLength()} bits";

    public string SecurityLevel
    {
        get
        {
            var factory = KeyFactory.GetInstance(privateKey.Algorithm, Provider)!;
            var info = factory.GetKeySpec(privateKey, Java.Lang.Class.FromType(typeof(KeyInfo)))!.JavaCast<KeyInfo>()!;
            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
#pragma warning disable CA1422 // The replacement, SecurityLevel, needs Android 12.
                return info.IsInsideSecureHardware ? "secure hardware" : "software";
#pragma warning restore CA1422
            }

            return (int)info.SecurityLevel switch
            {
                0 => "software",
                1 => "trusted execution environment (TEE)",
                2 => "StrongBox",
                -2 => "unknown secure hardware",
                int other => $"unknown ({other})",
            };
        }
    }

    public static KeystoreRsaKey? TryLoad()
    {
        var store = KeyStore.GetInstance(Provider)!;
        store.Load(null as KeyStore.ILoadStoreParameter);
        if (!store.ContainsAlias(Alias))
        {
            return null;
        }

        var key = store.GetKey(Alias, null)!.JavaCast<IPrivateKey>()!;
        var publicKey = store.GetCertificate(Alias)!.PublicKey!.JavaCast<IRSAPublicKey>()!;
        return new KeystoreRsaKey(key, publicKey);
    }

    public static KeystoreRsaKey Create()
    {
        var spec = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Sign)
            .SetKeySize(3072)
            .SetDigests(KeyProperties.DigestSha256, KeyProperties.DigestSha512)
            .SetSignaturePaddings(KeyProperties.SignaturePaddingRsaPkcs1)
            .Build();
        var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmRsa, Provider)!;
        generator.Initialize(spec);
        var pair = generator.GenerateKeyPair()!;
        return new KeystoreRsaKey(pair.Private!, pair.Public!.JavaCast<IRSAPublicKey>()!);
    }

    private sealed class Algorithm(string name, string javaName, KeystoreRsaKey key) : HostAlgorithm(name)
    {
        public override byte[] Data => key.PublicKeyBlob;

        public override byte[] Sign(byte[] data)
        {
            long started = Stopwatch.GetTimestamp();
            var signer = Java.Security.Signature.GetInstance(javaName)!;
            signer.InitSign(key.privateKey);
            signer.Update(data);
            byte[] signature = signer.Sign()!;
            SshTransport.Log($"The Keystore signed with {Name} in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
            return OpenSshRsa.SignatureBlob(Name, signature);
        }

        public override bool VerifySignature(byte[] data, byte[] signature) =>
            throw new NotSupportedException("The client key only signs.");
    }
}
