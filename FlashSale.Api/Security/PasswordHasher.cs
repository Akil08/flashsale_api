using System.Security.Cryptography;

namespace FlashSale.Api.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 100000;

    public static string HashPassword(string password)
    {
        byte[] salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Use the modern, non-obsolete static Pbkdf2 method
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, 
            salt, 
            Iterations, 
            HashAlgorithmName.SHA256, 
            HashSize);

        byte[] hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        byte[] hashBytes = Convert.FromBase64String(storedHash);

        byte[] salt = new byte[SaltSize];
        Array.Copy(hashBytes, 0, salt, 0, SaltSize);

        // Use the modern, non-obsolete static Pbkdf2 method
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, 
            salt, 
            Iterations, 
            HashAlgorithmName.SHA256, 
            HashSize);

        for (int i = 0; i < HashSize; i++)
        {
            if (hashBytes[i + SaltSize] != hash[i])
            {
                return false;
            }
        }
        return true;
    }
}