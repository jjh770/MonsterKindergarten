using Cysharp.Threading.Tasks;

public class LocalAccountRepository : IAccountRepository
{
    private const string LocalUserId = "LocalPlayer";

    public UniTask<AccountResult> Login(bool useManualSignIn)
    {
        return UniTask.FromResult(new AccountResult
        {
            Success = true,
            UserId = LocalUserId,
        });
    }

    public void Logout()
    {
        // 로컬 모드는 유지할 세션이 없어 정리할 상태가 없다.
    }
}
