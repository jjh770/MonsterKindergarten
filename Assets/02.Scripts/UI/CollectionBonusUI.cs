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
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private ToastMessageUI _toast;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;
    [Tooltip("보너스가 오를 때 띄우는 토스트의 표시 시간(초)입니다. 두 줄이라 기본보다 길게 둡니다.")]
    [SerializeField, Min(0f)] private float _bonusToastDuration = 2.8f;

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
        _gameManager.AllDataInitialized += RefreshAvailability;
        TutorialManager.Started += RefreshAvailability;
        TutorialManager.Finished += RefreshAvailability;
        _gameManager.OnGameplayActivated += RefreshAvailability;

        _slimeManager.NormalCollectionCountChanged += OnCollectionCountChanged;
        RefreshAvailability();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _openButton?.onClick.RemoveListener(Open);
        _closeButton?.onClick.RemoveListener(Close);
        TutorialManager.Started -= RefreshAvailability;
        TutorialManager.Finished -= RefreshAvailability;
        if (_gameManager != null)
        {
            _gameManager.AllDataInitialized -= RefreshAvailability;
            _gameManager.OnGameplayActivated -= RefreshAvailability;
        }

        if (_slimeManager != null)
        {
            _slimeManager.NormalCollectionCountChanged -= OnCollectionCountChanged;
        }
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
                             _gameExitManager != null &&
                             _slimeManager != null &&
                             _gameManager != null &&
                             _toast != null;
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
        ShowBonusToast(count);
        if (_isOpen)
        {
            RefreshContent();
        }
    }

    // 등록할 때마다 한 칸씩 늘어나므로 이전 수와 비교하면 보너스가 오른 순간만 골라낼 수 있다.
    // 15종 뒤에는 보너스가 오르지 않아 토스트도 뜨지 않는다.
    private void ShowBonusToast(int count)
    {
        double current = NormalCollectionRules.GetPointBonusPercent(count);
        if (current <= NormalCollectionRules.GetPointBonusPercent(count - 1)) return;

        _toast.Show(
            "도감에 더 많은 슬라임들이 등록되었어요!" + "\n" +
            "슬라임 포인트 획득량이 " +
            NormalCollectionRules.PointBonusPercentPerStep.ToString("0") +
            "% 추가됩니다. 현재 +" + current.ToString("0") + "%",
            _bonusToastDuration);
    }

    private void RefreshContent()
    {
        int count = _slimeManager.NormalCollectionCount;
        _progressText.text = $"현재 도감  {count}/{SlimeStatusSaveData.NormalCollectionSize}";

        var builder = new StringBuilder();
        AppendPointBonus(builder, count);
        AppendBonus(builder, count, NormalCollectionRules.AutoMergeCount,
            "자동 합성", "버튼을 누르면 같은 등급 슬라임을 한 번에 합성해요.");
        AppendBonus(builder, count, NormalCollectionRules.TicketBulkCollectCount,
            "티켓 회수", "필드에 떨어진 티켓을 한 번에 회수해요.");
        AppendBonus(builder, count, NormalCollectionRules.OfflineTicketRewardCount,
            "오프라인 티켓 회수", "접속하지 않은 시간에 티켓도 모아줘요.");
        // 20종을 채우면 숨겨 둔 마지막 항목이 졸업식으로 바뀐다. 그 전에는 무엇인지 알리지 않는다.
        bool isEndingUnlocked = count >= NormalCollectionRules.MainEndingCount;
        AppendBonus(builder, count, NormalCollectionRules.MainEndingCount,
            isEndingUnlocked ? "유치원 졸업식" : "???",
            isEndingUnlocked
                ? "유치원 졸업을 축하합니다!\n도감에서 다시 볼 수 있어요."
                : "???");
        _bonusText.text = builder.ToString();
    }

    // 해금이 아니라 계속 쌓이는 보너스라 마일스톤 항목과 따로 적는다.
    private static void AppendPointBonus(StringBuilder builder, int currentCount)
    {
        double current = NormalCollectionRules.GetPointBonusPercent(currentCount);
        int step = NormalCollectionRules.PointBonusStepCount;
        int next = (currentCount / step + 1) * step;
        string nextText = currentCount / step < NormalCollectionRules.PointBonusMaxStepCount
            ? $"다음 {next}종"
            : "최대";

        builder.Append("<b>도감 ")
            .Append(step)
            .Append("종마다 - 포인트 +")
            .Append(NormalCollectionRules.PointBonusPercentPerStep.ToString("0"))
            .Append("%</b>   <color=#4F8A3B>현재 +")
            .Append(current.ToString("0"))
            .Append("%</color>\n<size=88%>모든 슬라임의 터치, 자동 포인트가 늘어나요. (")
            .Append(nextText)
            .Append(")</size>");
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
