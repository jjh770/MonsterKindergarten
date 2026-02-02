using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 암호화 유틸리티 (Repository 계층)
/// SHA256 해시 + Salt 지원
/// </summary>
public static class Crypto
{
    /// <summary>
    /// 비밀번호를 SHA256으로 해시
    /// </summary>
    public static string HashPassword(string plainText, string salt = "")
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("입력 문자열이 비어 있을 수 없습니다.");

        string combined = plainText + salt;

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(combined);
            byte[] hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    /// <summary>
    /// 비밀번호 검증
    /// </summary>
    public static bool VerifyPassword(string plainText, string hashedPassword, string salt = "")
    {
        string inputHash = HashPassword(plainText, salt);
        return inputHash == hashedPassword;
    }
}
