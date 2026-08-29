using Cysharp.Threading.Tasks;

public interface IAccountRepository
{
    UniTask<AccountResult> Login(bool useManualSignIn);
    UniTask DeleteAccount();
    void Logout();
}
