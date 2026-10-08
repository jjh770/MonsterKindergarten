using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 보상형 광고를 보는 팝업이다. 하단 메뉴의 `광고 보너스` 버튼으로 열고, 포인트 부스트와 뽑기권 두 줄에서
// 광고를 본다. 광고와 보상은 AdRewardService가 맡고, 여기서는 볼 수 있는지와 남은 횟수를 보여 주고 결과를 알린다.
//
// 결과 알림은 토스트가 아니라 팝업 안의 한 줄이다. 공용 토스트는 이 팝업보다 아래 캔버스에 있어 가려진다.
public sealed class AdBonusUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Button _openButton;
    [SerializeField] private GameObject _popupRoot;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private PopupMotion _panelMotion;
    [SerializeField] private Button _closeButton;
    [SerializeField] private AdBonusItemView _boostItem;
    [SerializeField] private AdBonusItemView _ticketItem;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private AdRewardService _adRewardService;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private GachaResultDirector _gachaDirector;
    [SerializeField] private DisplayRoomUI _displayRoomUI;
    [SerializeField] private DisplayRoomInfoUI _displayRoomInfoUI;
    [SerializeField] private MainEndingUI _mainEndingUI;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;
    [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;
    [SerializeField, Min(0f)] private float _messageSeconds = 2.4f;

    private Tween _fadeTween;
    private bool _isOpen;
    private bool _isClosing;
    private bool _isWatching;
    private float _refreshTimer;
    private float _messageTimer;

    // 하단 메뉴의 버튼이다. 광고 보너스 안내가 이 버튼을 가리킨다.
    public RectTransform ButtonTarget => _openButton != null ? _openButton.transform as RectTransform : null;

    // 지금 팝업을 열 수 있는 상태인가. 이미 열려 있거나 닫히는 중이면 false다.
    public bool CanOpenNow => !_isOpen && !_isClosing && CanOpen();

    // 팝업이 열린 직후에 불린다. 안내가 열리기를 기다릴 때 쓴다.
    public event Action Opened;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _popupRoot.SetActive(false);
        _messageText.text = string.Empty;
        _openButton.onClick.AddListener(Open);
        _closeButton.onClick.AddListener(Close);
        _boostItem.WatchButton.onClick.AddListener(OnBoostWatchClicked);
        _ticketItem.WatchButton.onClick.AddListener(OnTicketWatchClicked);
        _adRewardService.ReadyChanged += OnReadyChanged;
        _gameManager.AllDataInitialized += RefreshAvailability;
        _gameManager.OnGameplayActivated += RefreshAvailability;
        TutorialManager.Started += RefreshAvailability;
        TutorialManager.Finished += RefreshAvailability;
        RefreshAvailability();
    }

    private void Update()
    {
        if (!_isOpen) return;

        float delta = Time.unscaledDeltaTime;
        _refreshTimer += delta;
        if (_refreshTimer >= _refreshInterval)
        {
            _refreshTimer = 0f;
            RefreshContent();
        }

        if (_messageTimer > 0f)
        {
            _messageTimer -= delta;
            if (_messageTimer <= 0f)
            {
                _messageText.text = string.Empty;
            }
        }
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _openButton?.onClick.RemoveListener(Open);
        _closeButton?.onClick.RemoveListener(Close);
        _boostItem?.WatchButton.onClick.RemoveListener(OnBoostWatchClicked);
        _ticketItem?.WatchButton.onClick.RemoveListener(OnTicketWatchClicked);
        TutorialManager.Started -= RefreshAvailability;
        TutorialManager.Finished -= RefreshAvailability;
        if (_adRewardService != null)
        {
            _adRewardService.ReadyChanged -= OnReadyChanged;
        }

        if (_gameManager != null)
        {
            _gameManager.AllDataInitialized -= RefreshAvailability;
            _gameManager.OnGameplayActivated -= RefreshAvailability;
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
                             _boostItem != null &&
                             _ticketItem != null &&
                             _messageText != null &&
                             _adRewardService != null &&
                             _clicker != null &&
                             _gameExitManager != null &&
                             _slimeManager != null &&
                             _gameManager != null &&
                             _gachaDirector != null &&
                             _displayRoomUI != null &&
                             _displayRoomInfoUI != null &&
                             _mainEndingUI != null;
        if (!hasReferences)
        {
            Debug.LogError("광고 보너스 UI의 필수 씬 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private void Open()
    {
        if (_isOpen || _isClosing || !CanOpen()) return;

        _isOpen = true;
        _refreshTimer = 0f;
        _messageTimer = 0f;
        _messageText.text = string.Empty;
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
        _panelMotion?.PlayOpen();
        Opened?.Invoke();
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
        _panelMotion?.PlayClose();
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

    private void OnReadyChanged()
    {
        if (_isOpen)
        {
            RefreshContent();
        }
    }

    private void RefreshAvailability()
    {
        if (_openButton == null) return;

        _openButton.gameObject.SetActive(IsUnlocked());
    }

    private void OnBoostWatchClicked() => WatchAsync(EAdPlacement.PointBoost).Forget();

    private void OnTicketWatchClicked() => WatchAsync(EAdPlacement.Ticket).Forget();

    private async UniTaskVoid WatchAsync(EAdPlacement placement)
    {
        if (_isWatching) return;

        _isWatching = true;
        RefreshContent();
        EAdShowResult result = await _adRewardService.ShowAsync(placement);
        if (this == null) return;

        _isWatching = false;
        ShowResultMessage(result, placement);
        RefreshContent();
    }

    private void ShowResultMessage(EAdShowResult result, EAdPlacement placement)
    {
        switch (result)
        {
            case EAdShowResult.Rewarded:
                AudioManager.Instance?.PlaySFX(EAudioSfx.UpgradeBuy);
                ShowMessage(placement == EAdPlacement.PointBoost
                    ? UiMessages.AdBoostRewarded
                    : UiMessages.AdTicketRewarded);
                break;
            case EAdShowResult.ClosedEarly:
                ShowMessage(UiMessages.AdClosedEarly);
                break;
            case EAdShowResult.NotReady:
                ShowMessage(UiMessages.AdNotReady);
                break;
            case EAdShowResult.NotAllowed:
                ShowMessage(GetUnavailableMessage(_adRewardService.GetAvailability(placement)));
                break;
            case EAdShowResult.Failed:
                ShowMessage(UiMessages.AdFailed);
                break;
        }
    }

    private void ShowMessage(string message)
    {
        _messageText.text = message;
        _messageTimer = _messageSeconds;
    }

    private void RefreshContent()
    {
        AdRewardTableSO table = _adRewardService.Table;
        _boostItem.Apply(
            $"{Mathf.RoundToInt(table.PointBoostSecondsPerAd / 60f)}분 동안 포인트 {table.PointBoostMultiplier:0.#}배",
            BuildBoostStatus(table),
            CanWatch(EAdPlacement.PointBoost));
        _ticketItem.Apply(
            $"뽑기권 {table.TicketReward}장",
            BuildTicketStatus(table),
            CanWatch(EAdPlacement.Ticket));
    }

    private string BuildBoostStatus(AdRewardTableSO table)
    {
        EAdAvailability availability = _adRewardService.GetAvailability(EAdPlacement.PointBoost);
        string countLine = BuildCountLine(
            _adRewardService.GetRemainingDailyCount(EAdPlacement.PointBoost),
            table.PointBoostDailyLimit);
        int seconds = Mathf.CeilToInt(_adRewardService.GetPointBoostRemainingSeconds());
        string remainingLine = seconds > 0 ? $"남은 시간 {seconds / 60}:{seconds % 60:00}, " : string.Empty;
        if (availability == EAdAvailability.Available || availability == EAdAvailability.NotReady)
        {
            return remainingLine + countLine;
        }

        return remainingLine + GetUnavailableMessage(availability);
    }

    private string BuildTicketStatus(AdRewardTableSO table)
    {
        EAdAvailability availability = _adRewardService.GetAvailability(EAdPlacement.Ticket);
        if (availability == EAdAvailability.Available || availability == EAdAvailability.NotReady)
        {
            return BuildCountLine(
                _adRewardService.GetRemainingDailyCount(EAdPlacement.Ticket),
                table.TicketDailyLimit);
        }

        return GetUnavailableMessage(availability);
    }

    private static string BuildCountLine(int remaining, int limit)
    {
        return $"오늘 {remaining}/{limit}회";
    }

    private static string GetUnavailableMessage(EAdAvailability availability)
    {
        switch (availability)
        {
            case EAdAvailability.Locked:
                return UiMessages.AdLocked;
            case EAdAvailability.DailyLimitReached:
                return UiMessages.AdLimitReached;
            case EAdAvailability.BoostFull:
                return UiMessages.AdBoostFull;
            default:
                return UiMessages.AdNotReady;
        }
    }

    private bool CanWatch(EAdPlacement placement)
    {
        return !_isWatching &&
               _adRewardService.GetAvailability(placement) == EAdAvailability.Available;
    }

    // 하단 버튼을 보여도 되는가. 메인 튜토리얼이 끝난 뒤부터다.
    private static bool IsUnlocked()
    {
        return GameplayGate.IsReady &&
               TutorialProgress.IsCompleted(TutorialIds.Main);
    }

    // 지금 팝업을 열어도 되는가. 다른 화면이 입력을 쥐고 있는 동안은 열지 않는다.
    // 튜토리얼이 도는 동안에도 광고 보너스 안내만은 연다. 그 안내가 이 팝업을 직접 보여 준다.
    private bool CanOpen()
    {
        return IsUnlocked() &&
               (!TutorialManager.IsRunning || TutorialManager.IsActive(TutorialIds.AdBonus)) &&
               !ScholarGuideUI.IsAnyOpen &&
               !_gachaDirector.IsPlaying &&
               !_displayRoomUI.IsSendMode &&
               !_displayRoomInfoUI.IsObserving &&
               !_mainEndingUI.IsPresenting;
    }
}
