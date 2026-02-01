// Account의 Email에 대한 명세(규칙)
using System.Text.RegularExpressions;

public class AccountEmailSpecification
{
    // 정규표현식 (컴파일하여 성능 최적화)
    private static readonly Regex EmailRegex = new Regex(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );


    private string _errorMessage;
    public string ErrorMessage => _errorMessage;


    public bool IsSatisfiedBy(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            _errorMessage = "이메일은 비어있을 수 없습니다.";
            return false;
        }

        if (!EmailRegex.IsMatch(email))
        {
            _errorMessage = "올바르지 않은 이메일 형식입니다.";
            return false;
        }

        return true;
    }
}
