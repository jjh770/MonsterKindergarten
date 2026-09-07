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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;


#if UNITY_ANDROID && !UNITY_EDITOR
        // Firebase 의존성 확인이 끝난 뒤 TryLogin에서 생성한다.
#else
        _repository = new LocalAccountRepository();
#endif
    }


    public async UniTask<AccountResult> TryLogin(bool useManualSignIn = false)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_repository == null)
        {
            FirebaseInitializer initializer = FirebaseInitializer.Instance;
            if (initializer == null || !await initializer.WaitForInitializationAsync())
            {
                return new AccountResult
                {
                    Success = false,
                    ErrorMessage = "Firebase를 초기화하지 못했습니다.\n앱을 다시 실행해 주세요.",
                };
            }

            try
            {
                _repository = new FirebaseAccountRepository();
            }
            catch (System.Exception e)
            {
                return new AccountResult
                {
                    Success = false,
                    ErrorMessage = $"Firebase를 초기화하지 못했습니다.\n{e.Message}",
                };
            }
        }
#endif

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

    public async UniTask DeleteAccount()
    {
        if (string.IsNullOrWhiteSpace(UserId))
            throw new System.InvalidOperationException("로그인한 계정을 확인할 수 없습니다.");

        await _repository.DeleteAccount();
        UserId = null;
    }
}
