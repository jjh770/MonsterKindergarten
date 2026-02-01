// 인증 결과
// 로그인/회원가입에대 한 성공 여부와 에러메시지와 Account

public struct AuthResult
{
    public bool Success;
    public string ErrorMessage;
    public Account Account;
}
