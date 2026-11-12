using System.Security.Cryptography;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Windows data protection (DPAPI) for the current user. Thin: the key store's tests use a fake.</summary>
internal sealed class WindowsUserProtection : IUserProtection
{
    public byte[] Protect(byte[] value) => ProtectedData.Protect(value, null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] value) => ProtectedData.Unprotect(value, null, DataProtectionScope.CurrentUser);
}
