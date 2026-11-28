using Renci.SshNet;
using Renci.SshNet.Security;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Offers the device key to SSH.NET as RSA SHA-2 only; hosts refuse SHA-1 signatures. The private key never leaves
/// the signing handle: SSH.NET gets only the public blob and finished signatures.
/// </summary>
internal sealed class DeviceKeySource : IPrivateKeySource
{
    public DeviceKeySource(IDeviceSigningKey key)
    {
        HostKeyAlgorithms =
        [
            new RsaSha2("rsa-sha2-512", key, RsaSignatureAlgorithm.Sha512),
            new RsaSha2("rsa-sha2-256", key, RsaSignatureAlgorithm.Sha256),
        ];
    }

    public IReadOnlyCollection<HostAlgorithm> HostKeyAlgorithms { get; }

    private sealed class RsaSha2(string name, IDeviceSigningKey key, RsaSignatureAlgorithm hash) : HostAlgorithm(name)
    {
        public override byte[] Data => Classified(() => key.PublicKeyBlob.ToArray());

        public override byte[] Sign(byte[] data) => Classified(() => SshWire.SignatureBlob(Name, key.Sign(data, hash)));

        private static byte[] Classified(Func<byte[]> useKey)
        {
            try
            {
                return useKey();
            }
            catch (Exception ex)
            {
                // A key that can't sign or give its public key is a fault of this device or of the code; SSH.NET must not mistake it for the network.
                throw FailureReport.ToLocalFailure(ex);
            }
        }

        public override bool VerifySignature(byte[] data, byte[] signature) =>
            throw new NotSupportedException("The device key only signs.");
    }
}
