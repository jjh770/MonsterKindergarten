using Cysharp.Threading.Tasks;
using UnityEngine;


// 매니저의 역할:
// 1. 도메인 관리 : 생성/조회/수정/삭제와 같은 비즈니스 로직
// 2. 외부와의 소통 창구
public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }

    public string UserId { get; private set; }

    private IAccountRepository _repository;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }


#if !UNITY_WEBGL || UNITY_EDITOR
        _repository = new FirebaseAccountRepository();
#else
        _repository = new LocalAccountRepository();
#endif
    }


    public async UniTask<AccountResult> TryLogin(bool useManualSignIn = false)
    {
        AccountResult result = await _repository.Login(useManualSignIn);
        if (result.Success)
        {
            UserId = result.UserId;
            return new AccountResult
            {
                Success = true,
                UserId = UserId,
            };
        }

        return new AccountResult
        {
            Success = false,
            ErrorMessage = result.ErrorMessage,
        };
    }

    public void Logout()
    {
        _repository.Logout();
        UserId = null;
    }
}
