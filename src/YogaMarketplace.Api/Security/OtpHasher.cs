using System.Security.Cryptography;
using System.Text;

namespace YogaMarketplace.Api.Security;

public static class OtpHasher
{
    public static string Hash(string code, string pepper)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pepper + ":" + code));
        return Convert.ToHexString(bytes);
    }

    public static bool Matches(string computedHash, string storedHash)
    {
        var left = Encoding.ASCII.GetBytes(computedHash);
        var right = Encoding.ASCII.GetBytes(storedHash);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
