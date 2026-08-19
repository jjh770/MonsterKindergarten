using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyScene : MonoBehaviour
{
    [SerializeField] private Button _loginButton;

    private string _popupText;
    private TMP_Text _loginButtonText;
    private bool _isLoggingIn;

    private void Start()
    {
        _loginButtonText = _loginButton.GetComponentInChildren<TMP_Text>(true);
        _loginButtonText.text = "Google Play 로그인";
        _loginButton.onClick.AddListener(() => Login(true).Forget());
        Login(false).Forget();
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
        LobbyErrorPopupUI.Instance.Show(_popupText);
    }
}
