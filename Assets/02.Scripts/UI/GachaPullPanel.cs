using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 기계 화면 아래의 1회·5회 버튼과 우측 상단의 닫기 버튼을 맡는다.
//
// 버튼은 티켓 그림과 그 아래 글자로만 이루어져 있다. 필요한 장수가 모자라면 눌리지 않고 흐리게 보인다.
// 자리가 모자란 것은 눌러 봐야 아는 일이라 여기서 막지 않고 토스트로 알린다. 이 패널은 언제 무엇을
// 허용할지는 정하지 않는다. 눌린 것을 알리는 이벤트와 보여 주는 것만 한다.
public sealed class GachaPullPanel : MonoBehaviour
{
    public const int SingleCount = 1;
    public const int MultiCount = 5;

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Button _closeButton;
    [SerializeField] private CanvasGroup _closeGroup;

    [Header("Single")]
    [SerializeField] private Button _singleButton;
    [SerializeField] private RectTransform _singleIcon;
    [SerializeField] private CanvasGroup _singleGroup;
    [SerializeField] private TMP_Text _singleLabel;

    [Header("Multi")]
    [SerializeField] private Button _multiButton;
    [SerializeField] private RectTransform _multiIcon;
    [SerializeField] private CanvasGroup _multiGroup;
    [SerializeField] private TMP_Text _multiLabel;

    [SerializeField] private ToastMessageUI _toast;

    [SerializeField, Min(0f)] private float _fadeSeconds = 0.25f;

    public event Action<int> PullRequested;
    public event Action CloseRequested;

    private Vector2 _singleAuthoredPosition;
    private Vector3 _singleIconBaseScale;
    private Tween _fadeTween;
    private Tween _iconTween;
    private bool _isReady;

    // 티켓 그림이 날아갈 때 시작점과 모양으로 쓴다.
    public RectTransform GetIcon(int count) => count == MultiCount ? _multiIcon : _singleIcon;

    public Sprite GetIconSprite(int count)
    {
        RectTransform icon = GetIcon(count);
        Image image = icon != null ? icon.GetComponent<Image>() : null;
        return image != null ? image.sprite : null;
    }

    private void Awake()
    {
        if (_group == null || _closeButton == null || _closeGroup == null ||
            _singleButton == null || _singleIcon == null || _singleGroup == null || _singleLabel == null ||
            _multiButton == null || _multiIcon == null || _multiGroup == null || _multiLabel == null ||
            _toast == null)
        {
            Debug.LogError("뽑기 버튼 패널의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _singleAuthoredPosition = ((RectTransform)_singleButton.transform).anchoredPosition;
        _singleIconBaseScale = _singleIcon.localScale;
        _closeButton.onClick.AddListener(OnCloseClicked);
        _singleButton.onClick.AddListener(OnSingleClicked);
        _multiButton.onClick.AddListener(OnMultiClicked);
        _isReady = true;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _iconTween?.Kill();
        if (_closeButton != null) _closeButton.onClick.RemoveListener(OnCloseClicked);
        if (_singleButton != null) _singleButton.onClick.RemoveListener(OnSingleClicked);
        if (_multiButton != null) _multiButton.onClick.RemoveListener(OnMultiClicked);
    }

    // 버튼을 켜고 보유 장수에 맞게 누를 수 있는 것만 밝게 한다. 튜토리얼에서는 1회 하나만 남기고 가운데에 둔다.
    public void Show(int tickets, bool tutorialOnly)
    {
        if (!_isReady) return;

        _fadeTween?.Kill();
        _iconTween?.Kill();
        gameObject.SetActive(true);
        _group.interactable = true;
        _group.blocksRaycasts = true;
        _group.alpha = 0f;
        _fadeTween = _group.DOFade(1f, _fadeSeconds).SetUpdate(true);

        _closeGroup.alpha = 1f;
        _closeGroup.gameObject.SetActive(!tutorialOnly);
        _multiGroup.gameObject.SetActive(!tutorialOnly);
        _singleGroup.alpha = 1f;
        _multiGroup.alpha = 1f;
        _singleIcon.localScale = _singleIconBaseScale;
        ((RectTransform)_singleButton.transform).anchoredPosition = tutorialOnly
            ? new Vector2(0f, _singleAuthoredPosition.y)
            : _singleAuthoredPosition;

        SetButtonAvailable(_singleButton, _singleLabel, tickets >= SingleCount);
        SetButtonAvailable(_multiButton, _multiLabel, tickets >= MultiCount);
        _closeButton.interactable = true;
    }

    // 튜토리얼에서 1회 버튼이 눌려 달라고 졸졸 뛴다.
    public void PulseSingle(float elapsed)
    {
        if (!_isReady) return;

        _singleIcon.localScale = _singleIconBaseScale * (1f + Mathf.Abs(Mathf.Sin(elapsed * 3.2f)) * 0.12f);
    }

    // 뽑기가 확정되면 눌린 버튼의 그림만 남기고 나머지는 사라진다. 그림은 티켓이 날아갈 때 따로 감춘다.
    public void SetCommitted(int pressedCount)
    {
        if (!_isReady) return;

        _fadeTween?.Kill();
        _iconTween?.Kill();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _closeGroup.alpha = 0f;
        _closeGroup.gameObject.SetActive(false);
        CanvasGroup other = pressedCount == MultiCount ? _singleGroup : _multiGroup;
        other.alpha = 0f;
        TMP_Text pressedLabel = pressedCount == MultiCount ? _multiLabel : _singleLabel;
        pressedLabel.alpha = 0f;
    }

    // 날아간 티켓의 원래 그림을 감춘다.
    public void HideIcon(int count)
    {
        if (!_isReady) return;

        CanvasGroup group = count == MultiCount ? _multiGroup : _singleGroup;
        group.alpha = 0f;
    }

    public void Hide()
    {
        if (!_isReady) return;

        _fadeTween?.Kill();
        _iconTween?.Kill();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public void ShowToast(string message)
    {
        if (_isReady) _toast.Show(message);
    }

    private static void SetButtonAvailable(Button button, TMP_Text label, bool isAvailable)
    {
        button.interactable = isAvailable;
        label.alpha = isAvailable ? 1f : 0.45f;
    }

    private void OnCloseClicked()
    {
        CloseRequested?.Invoke();
    }

    private void OnSingleClicked()
    {
        PullRequested?.Invoke(SingleCount);
    }

    private void OnMultiClicked()
    {
        PullRequested?.Invoke(MultiCount);
    }
}
