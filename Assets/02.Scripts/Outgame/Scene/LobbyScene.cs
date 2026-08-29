using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyScene : MonoBehaviour
{
    public static bool SkipNextAutomaticLogin { get; set; }
    [SerializeField] private Button _loginButton;

    private string _popupText;
    private TMP_Text _loginButtonText;
    private bool _isLoggingIn;

    private void Start()
    {
        GameplaySaveGate.EndReset();
        _loginButtonText = _loginButton.GetComponentInChildren<TMP_Text>(true);
        _loginButtonText.text = "Google Play 로그인";
        _loginButton.onClick.AddListener(() => Login(true).Forget());
        bool skipAutomaticLogin = SkipNextAutomaticLogin;
        SkipNextAutomaticLogin = false;
        if (!skipAutomaticLogin) Login(false).Forget();
    }

    private async UniTask Login(bool useManualSignIn)
    {
        if (_isLoggingIn)
        {
            return;
        }

        _isLoggingIn = true;
        _loginButton.interactable = false;
        _loginButtonText.text = "로그인 중...";

        AccountResult result = await AccountManager.Instance.TryLogin(useManualSignIn);
        if (result.Success)
        {
            if (GameAccountDeletionService.HasPendingDeletion(result.UserId))
            {
                _loginButtonText.text = "계정 삭제 마무리 중...";
                try
                {
                    await GameAccountDeletionService.DeleteAsync(result.UserId);
                    GameplaySaveGate.EndReset();
                    _isLoggingIn = false;
                    _loginButton.interactable = true;
                    _loginButtonText.text = "Google Play 로그인";
                    _popupText = "게임 계정을 삭제했습니다.";
                    ShowLobbyPopup();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"게임 계정 삭제를 완료하지 못했습니다: {e.Message}");
                    _isLoggingIn = false;
                    _loginButton.interactable = true;
                    _loginButtonText.text = "계정 삭제 다시 시도";
                    _popupText = "계정 삭제를 완료하지 못했어요. 인터넷 연결을 확인하고 다시 시도해 주세요.";
                    ShowLobbyPopup();
                }
                return;
            }

            if (GameDataResetService.HasPendingReset(result.UserId))
            {
                _loginButtonText.text = "초기화 마무리 중...";
                try
                {
                    await GameDataResetService.ResetAsync(result.UserId);
                }
                catch (Exception e)
                {
                    // 초기화 결과가 불명확한 계정은 기존 진행도로 게임에 진입하지 않는다.
                    Debug.LogWarning($"게임 데이터 초기화를 완료하지 못했습니다: {e.Message}");
                    _isLoggingIn = false;
                    _loginButton.interactable = true;
                    _loginButtonText.text = "초기화 다시 시도";
                    _popupText = "초기화를 완료하지 못했어요. 인터넷 연결을 확인하고 다시 시도해 주세요.";
                    ShowLobbyPopup();
                    return;
                }
            }
            GameplaySaveGate.EndReset();
            SceneManagerEx.Instance.LoadGameScene();
            return;
        }

        _isLoggingIn = false;
        _loginButton.interactable = true;
        _loginButtonText.text = "Google Play 로그인";

        if (useManualSignIn)
        {
            _popupText = result.ErrorMessage;
            ShowLobbyPopup();
        }
    }

    private void ShowLobbyPopup()
    {
        MessagePopupUI.Instance.Show(_popupText);
    }
}
