public interface IAccountRepository
{
    // 이메일 중복검사
    bool IsEmailAvailable(string email);
    AuthResult Register(string email, string password);
    AuthResult Login(string email, string password);
    void Logout();
}
