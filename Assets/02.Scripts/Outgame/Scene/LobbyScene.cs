using Cysharp.Threading.Tasks;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyScene : MonoBehaviour
{
    private enum LobbySceneMode
    {
        Login,
        Signup,
    }

    [Header("로그인 UI 영역")]
    [SerializeField] private GameObject _loginPanel;
    [SerializeField] private TMP_InputField _loginEmailInputField;
    [SerializeField] private TMP_InputField _loginPasswordInputField;

    [Header("로그인 버튼 영역")]
    [SerializeField] private Button _signupButton; // 회원가입 창열기
    [SerializeField] private Button _loginButton;  // 로그인 시도

    [Header("회원가입 UI 영역")]
    [SerializeField] private GameObject _signupPanel;
    [SerializeField] private TMP_InputField _signupEmailInputField;
    [SerializeField] private TMP_InputField _signupPasswordInputField;
    [SerializeField] private TMP_InputField _verifyPasswordInputField;

    [Header("회원가입 버튼 영역")]
    [SerializeField] private Button _registerSignupButton; // 회원가입 시도
    [SerializeField] private Button _cancelButton;         // 로그인 창열기

    private string _popupText;
    private LobbySceneMode _mode = LobbySceneMode.Login;

    private void Start()
    {
        AddButtonEvents();
        Refresh();
        StartCoroutine(TabNavigationRoutine());
    }

    private void AddButtonEvents()
    {
        _signupButton.onClick.AddListener(() => ChangeSceneMode(LobbySceneMode.Signup));
        _cancelButton.onClick.AddListener(() => ChangeSceneMode(LobbySceneMode.Login));
        _loginButton.onClick.AddListener(() => _ = Login());
        _registerSignupButton.onClick.AddListener(() => _ = Signup());
    }

    private void ChangeSceneMode(LobbySceneMode mode)
    {
        _mode = mode;
        Refresh();
    }

    private void Refresh()
    {
        if (_mode == LobbySceneMode.Login)
        {
            _loginPanel.SetActive(true);
            _signupPanel.SetActive(false);
        }
        else if (_mode == LobbySceneMode.Signup)
        {
            _loginPanel.SetActive(false);
            _signupPanel.SetActive(true);
        }
    }

    private async UniTask Login()
    {
        string email = _loginEmailInputField.text;
        string password = _loginPasswordInputField.text;

        var result = await AccountManager.Instance.TryLogin(email, password);
        if (result.Success)
        {
            SceneManagerEx.Instance.LoadGameScene();
        }
        else
        {
            _popupText = result.ErrorMessage;
            ShowLobbyPopup();
        }
    }

    private async UniTask Signup()
    {
        string email = _signupEmailInputField.text;
        string password = _signupPasswordInputField.text;
        string verifyPassword = _verifyPasswordInputField.text;

        var emailSpec = new AccountEmailSpecification();

        if (!emailSpec.IsSatisfiedBy(email))
        {
            _popupText = emailSpec.ErrorMessage;
            ShowLobbyPopup();
            return;
        }

        if (string.IsNullOrEmpty(verifyPassword) || password != verifyPassword)
        {
            _popupText = "<color=#FF5F5F>패스워드</color>를\n확인해주세요.";
            ShowLobbyPopup();
            return;
        }

        var result = await AccountManager.Instance.TryRegister(email, password);
        if (result.Success)
        {
            //SuccessSignup();
            _popupText = "회원가입 되었습니다!";
            ShowLobbyPopup();
            ChangeSceneMode(LobbySceneMode.Login);
        }
        else
        {
            _popupText = result.ErrorMessage;
            ShowLobbyPopup();
        }
    }

    private IEnumerator TabNavigationRoutine()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (_mode == LobbySceneMode.Login)
                {
                    if (_loginEmailInputField.isFocused)
                        _loginPasswordInputField.Select();
                    else
                        _loginEmailInputField.Select();
                }
                else if (_mode == LobbySceneMode.Signup)
                {
                    if (_signupEmailInputField.isFocused)
                        _signupPasswordInputField.Select();
                    else if (_signupPasswordInputField.isFocused)
                        _verifyPasswordInputField.Select();
                    else
                        _signupEmailInputField.Select();
                }
            }
            yield return null;
        }
    }

    private void ShowLobbyPopup()
    {
        LobbyPopupUI.Instance.Show(_popupText);
    }
}
