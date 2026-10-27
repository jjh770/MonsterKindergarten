using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 저장된 진행도가 앱보다 새 버전이라 게임에 들어갈 수 없을 때 띄우는 업데이트 안내 팝업.
//
// 공용 안내(MessagePopupUI)는 잠깐 뒤 저절로 사라져 스토어로 갈 틈이 없다. 이 팝업은 플레이어가 닫을 때까지
// 남아 있고, 업데이트 버튼은 스토어 페이지를 연다. 닫아도 로그인 화면에 머물 뿐 게임에 들어가지는 않는다.
// 언제 띄울지는 LoginScene이 정한다.
public sealed class AppUpdatePopupUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _updateButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private PopupMotion _motion;

    private bool _isShown;

    // 참조가 하나라도 비면 호출부가 공용 안내로 대체할 수 있어야 한다.
    public bool IsReady =>
        _root != null &&
        _messageText != null &&
        _updateButton != null &&
        _closeButton != null;

    private void Awake()
    {
        if (!IsReady)
        {
            Debug.LogError("업데이트 안내 팝업의 참조가 비어 있습니다.", this);
            return;
        }

        _updateButton.onClick.AddListener(OnUpdateClicked);
        _closeButton.onClick.AddListener(Hide);
        _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_updateButton != null) _updateButton.onClick.RemoveListener(OnUpdateClicked);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
    }

    public void Show(string message)
    {
        if (!IsReady) return;

        _messageText.text = message;
        _isShown = true;
        _root.SetActive(true);
        _motion?.PlayOpen();
    }

    public void Hide()
    {
        if (_root == null) return;

        bool wasShown = _isShown;
        _isShown = false;
        if (!_root.activeSelf) return;

        if (!wasShown || _motion == null)
        {
            _root.SetActive(false);
            return;
        }

        _motion.PlayClose().OnComplete(() =>
        {
            // 닫히는 동안 다시 열렸으면 끄지 않는다.
            if (!_isShown) _root.SetActive(false);
        });
    }

    // 스토어로 보낸 뒤에도 팝업은 남겨 둔다. 업데이트하지 않고 돌아온 플레이어가 다시 누를 수 있어야 한다.
    private void OnUpdateClicked()
    {
        StoreLinks.OpenStorePage();
    }
}
