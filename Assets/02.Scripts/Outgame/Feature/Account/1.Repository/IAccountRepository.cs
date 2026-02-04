using Cysharp.Threading.Tasks;

public interface IAccountRepository
{
    // 이메일 중복검사
    //bool IsEmailAvailable(string email);
    UniTask<AccountResult> Register(string email, string password);
    UniTask<AccountResult> Login(string email, string password);
    void Logout();
}
