using Cysharp.Threading.Tasks;

public interface IAccountRepository
{
    UniTask<AccountResult> Login(bool useManualSignIn);
    void Logout();
}
