using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ELegalDocument
{
    Terms,
    Privacy,
}

// 이용약관과 개인정보처리방침을 게임 안에서 보여 주는 팝업이다. 기획서 §21.11.
//
// 글은 앱에 들어 있는 원본(TextAsset)을 그대로 보여 주므로 네트워크 없이 열리고, 브라우저로 튕겨 나가지 않아
// 로그인 전의 동의 흐름이 끊기지 않는다. 같은 원본이 docs의 웹 문서도 만들기 때문에(LegalDocumentExporter)
// 두 곳이 서로 다른 약관을 말하지 않는다. 웹으로 보고 싶은 사람을 위해 같은 문서의 웹 주소도 열 수 있다.
//
// 동의 화면(로그인 씬)과 옵션 화면(게임 씬)이 같은 팝업을 쓴다. 게임 씬에서는 뒤로가기를 GameExitManager에
// 맡기고, 그것이 없는 로그인 씬에서는 직접 읽는다.
public sealed class LegalDocumentUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private PopupMotion _motion;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _webButton;
    [SerializeField] private TextAsset _termsAsset;
    [SerializeField] private TextAsset _privacyAsset;
    [Tooltip("뒤로가기를 맡길 곳입니다. 로그인 씬에는 없으므로 비워 둡니다.")]
    [SerializeField] private GameExitManager _gameExitManager;

    private ELegalDocument _document;
    private bool _isShown;

    // 열려 있는 동안 다른 화면이 같은 뒤로가기를 가로채지 않도록 알린다.
    public static bool IsAnyOpen { get; private set; }

    public bool IsReady =>
        _root != null &&
        _titleText != null &&
        _bodyText != null &&
        _scrollRect != null &&
        _closeButton != null &&
        _termsAsset != null &&
        _privacyAsset != null;

    private void Awake()
    {
        if (!IsReady)
        {
            Debug.LogError("약관 보기 팝업의 참조가 비어 있습니다.", this);
            return;
        }

        _closeButton.onClick.AddListener(Close);
        if (_webButton != null) _webButton.onClick.AddListener(OpenWeb);
        _root.SetActive(false);
    }

    private void Update()
    {
        // 게임 씬에서는 GameExitManager가 뒤로가기를 준다. 로그인 씬에서만 직접 읽는다.
        if (_isShown && _gameExitManager == null &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_webButton != null) _webButton.onClick.RemoveListener(OpenWeb);
        if (_isShown) IsAnyOpen = false;
        if (_gameExitManager != null) _gameExitManager.UnregisterBackHandler(this);
    }

    public void Show(ELegalDocument document)
    {
        if (!IsReady || _isShown) return;

        _document = document;
        string source = (document == ELegalDocument.Terms ? _termsAsset : _privacyAsset).text;
        _titleText.text = LegalTextFormatter.GetTitle(source);
        _bodyText.text = LegalTextFormatter.ToTmp(source);
        // 이전에 읽던 자리가 남아 있으면 처음부터 읽지 못한다.
        _scrollRect.verticalNormalizedPosition = 1f;
        _isShown = true;
        IsAnyOpen = true;
        transform.SetAsLastSibling();
        _root.SetActive(true);
        // 본문 높이가 정해진 다음에 맨 위로 돌려야 한다. 켜자마자 위치를 잡으면 이전 높이 기준으로 어긋난다.
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 1f;
        _motion?.PlayOpen();
        _gameExitManager?.RegisterBackHandler(this, TryClose);
    }

    public void Close()
    {
        TryClose();
    }

    private bool TryClose()
    {
        if (!_isShown) return false;

        _isShown = false;
        IsAnyOpen = false;
        _gameExitManager?.UnregisterBackHandler(this);
        if (_motion == null)
        {
            _root.SetActive(false);
            return true;
        }

        _motion.PlayClose().OnComplete(() =>
        {
            // 닫히는 동안 다시 열렸으면 끄지 않는다.
            if (!_isShown) _root.SetActive(false);
        });
        return true;
    }

    private void OpenWeb()
    {
        Application.OpenURL(_document == ELegalDocument.Terms ? LegalLinks.TermsUrl : LegalLinks.PrivacyUrl);
    }
}
