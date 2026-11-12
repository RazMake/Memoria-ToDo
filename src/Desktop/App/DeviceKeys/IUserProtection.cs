namespace Memoria.ToDo.Desktop.App;

/// <summary>Protects small secrets for the current Windows account, so only that account can read them back.</summary>
internal interface IUserProtection
{
    byte[] Protect(byte[] value);

    /// <summary>Throws a <see cref="System.Security.Cryptography.CryptographicException"/> when this account can't open <paramref name="value"/>.</summary>
    byte[] Unprotect(byte[] value);
}
