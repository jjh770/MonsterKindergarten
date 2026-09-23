using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 일반 슬라임 도감 등록 수로 해금되는 부가 효과를 한곳에서 보여준다.
// 해금 기준은 NormalCollectionRules를 직접 사용해 실제 기능과 표시가 어긋나지 않게 한다.
public sealed class CollectionBonusUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Button _openButton;
    [SerializeField] private GameObject _popupRoot;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private TMP_Text _bonusText;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;

    private Tween _fadeTween;
    private bool _isOpen;
    private bool _isClosing;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _popupRoot.SetActive(false);
        _openButton.onClick.AddListener(Open);
        _closeButton.onClick.AddListener(Close);
        GameManager.OnAllDataInitialized += RefreshAvailability;
        TutorialManager.Started += RefreshAvailability;
        TutorialManager.Finished += RefreshAvailability;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated += RefreshAvailability;
        }

        SlimeManager.OnNormalCollectionCountChanged += OnCollectionCountChanged;
        RefreshAvailability();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _openButton?.onClick.RemoveListener(Open);
        _closeButton?.onClick.RemoveListener(Close);
        GameManager.OnAllDataInitialized -= RefreshAvailability;
        TutorialManager.Started -= RefreshAvailability;
        TutorialManager.Finished -= RefreshAvailability;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated -= RefreshAvailability;
        }

        SlimeManager.OnNormalCollectionCountChanged -= OnCollectionCountChanged;
        _clicker?.ReleaseMode(this);
        _gameExitManager?.UnregisterBackHandler(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isOpen || _isClosing) return;
        if (RectTransformUtility.RectangleContainsScreenPoint(
                _panel,
                eventData.position,
                eventData.pressEventCamera))
        {
            return;
        }

        TryClose();
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _openButton != null &&
                             _popupRoot != null &&
                             _panel != null &&
                             _canvasGroup != null &&
                             _closeButton != null &&
                             _progressText != null &&
                             _bonusText != null &&
                             _clicker != null &&
                             _gameExitManager != null;
        if (!hasReferences)
        {
            Debug.LogError("도감 효과 UI의 필수 씬 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void Open()
    {
        if (_isOpen || _isClosing || !CanOpen()) return;

        _isOpen = true;
        transform.SetAsLastSibling();
        _popupRoot.SetActive(true);
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        RefreshContent();
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        _gameExitManager.RegisterBackHandler(this, TryClose);

        _fadeTween?.Kill();
        _fadeTween = _canvasGroup
            .DOFade(1f, _fadeDuration)
            .SetUpdate(true)
            .OnComplete(() => _fadeTween = null);
    }

    private void Close()
    {
        TryClose();
    }

    private bool TryClose()
    {
        if (!_isOpen) return false;
        if (_isClosing) return true;

        _isClosing = true;
        _canvasGroup.interactable = false;
        _gameExitManager.UnregisterBackHandler(this);
        _clicker.ReleaseMode(this);

        _fadeTween?.Kill();
        _fadeTween = _canvasGroup
            .DOFade(0f, _fadeDuration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _fadeTween = null;
                _popupRoot.SetActive(false);
                _isOpen = false;
                _isClosing = false;
            });
        return true;
    }

    private void RefreshAvailability()
    {
        if (_openButton == null) return;
        _openButton.gameObject.SetActive(CanOpen());
    }

    private void OnCollectionCountChanged(int count)
    {
        if (_isOpen)
        {
            RefreshContent();
        }
    }

    private void RefreshContent()
    {
        int count = SlimeManager.Instance?.NormalCollectionCount ?? 0;
        _progressText.text = $"현재 도감  {count}/{SlimeStatusSaveData.NormalCollectionSize}";

        var builder = new StringBuilder();
        AppendBonus(builder, count, NormalCollectionRules.AutoMergeCount,
            "자동 합성", "버튼을 누르면 같은 등급 슬라임을 한 번에 합성해요.");
        AppendBonus(builder, count, NormalCollectionRules.TicketBulkCollectCount,
            "티켓 회수", "필드에 떨어진 가챠권을 한 번에 회수해요.");
        AppendBonus(builder, count, NormalCollectionRules.OfflineTicketRewardCount,
            "오프라인 가챠권 보상", "접속하지 않은 시간에 가챠권도 모아줘요.");
        AppendBonus(builder, count, NormalCollectionRules.HiddenFeverCount,
            "???", "???");
        _bonusText.text = builder.ToString();
    }

    private static void AppendBonus(
        StringBuilder builder,
        int currentCount,
        int requiredCount,
        string title,
        string description)
    {
        if (builder.Length > 0)
        {
            builder.Append("\n\n");
        }

        bool unlocked = currentCount >= requiredCount;
        string stateColor = unlocked ? "#4F8A3B" : "#8B7868";
        string state = unlocked ? "해금 완료" : $"{currentCount}/{requiredCount}";
        builder.Append("<b>도감 ")
            .Append(requiredCount)
            .Append("종 - ")
            .Append(title)
            .Append("</b>   <color=")
            .Append(stateColor)
            .Append('>')
            .Append(state)
            .Append("</color>\n<size=88%>")
            .Append(description)
            .Append("</size>");
    }

    private static bool CanOpen()
    {
        return GameplayGate.IsDisplayRoomAvailable;
    }
}
