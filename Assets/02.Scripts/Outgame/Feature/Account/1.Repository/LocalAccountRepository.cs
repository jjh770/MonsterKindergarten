using Cysharp.Threading.Tasks;
using UnityEngine;

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
        Debug.Log("로그아웃 됐습니다.");
    }
}
