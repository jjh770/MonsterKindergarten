using Cysharp.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(OfflineRewardPopupUI))]
public sealed class OfflineRewardPopupController : MonoBehaviour
{
    [SerializeField] private OfflineRewardPopupUI _view;
    [SerializeField] private OfflineRewardManager _offlineRewardManager;
    [SerializeField] private AdRewardService _adRewardService;
    [SerializeField] private ToastMessageUI _toast;

    private OfflineRewardResult? _displayedReward;
    private bool _isWatchingAd;

    private void Awake()
    {
        if (_view == null)
        {
            _view = GetComponent<OfflineRewardPopupUI>();
        }
    }

    private void Start()
    {
        if (_view == null)
        {
            Debug.LogError("OfflineRewardPopupUI가 없어 오프라인 보상 팝업을 초기화할 수 없습니다.", this);
            enabled = false;
            return;
        }

        if (_offlineRewardManager == null)
        {
            Debug.LogError("보상 매니저가 없어 오프라인 보상 팝업을 초기화할 수 없습니다.", this);
            enabled = false;
            return;
        }

        _view.ConfirmRequested += OnConfirmRequested;
        _view.PresentationCompleted += OnPresentationCompleted;
        _view.AdDoubleRequested += OnAdDoubleRequested;
        _offlineRewardManager.Ready += ShowPendingReward;

        if (_adRewardService != null)
        {
            _adRewardService.ReadyChanged += RefreshAdDoubleButton;
        }
        ShowPendingReward();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.ConfirmRequested -= OnConfirmRequested;
            _view.PresentationCompleted -= OnPresentationCompleted;
            _view.AdDoubleRequested -= OnAdDoubleRequested;
        }

        if (_adRewardService != null)
        {
            _adRewardService.ReadyChanged -= RefreshAdDoubleButton;
        }

        if (_offlineRewardManager != null)
        {
            _offlineRewardManager.Ready -= ShowPendingReward;
        }
    }

    private void ShowPendingReward()
    {
        if (_displayedReward.HasValue ||
            _offlineRewardManager == null ||
            !_offlineRewardManager.TryConsume(out OfflineRewardResult result))
        {
            return;
        }

        _displayedReward = result;
        _view.Show(result.ElapsedTime, result.Reward, result.TicketReward);
        RefreshAdDoubleButton();
    }

    private void OnConfirmRequested()
    {
        if (!_displayedReward.HasValue ||
            _isWatchingAd ||
            _offlineRewardManager == null ||
            !_offlineRewardManager.TryClaim())
        {
            return;
        }

        PlayClaim(_displayedReward.Value);
    }

    private void PlayClaim(OfflineRewardResult result)
    {
        float duration = _view.PlayCollect(
            result.ElapsedTime,
            result.TicketReward);

        if ((double)result.Reward > 0d)
        {
            PointCountUpEvents.Request(new PointCountUpRequest(
                result.PointBeforeReward,
                result.PointAfterReward,
                duration));
        }
    }

    // 광고를 볼 수 있고 받을 포인트가 있을 때만 `광고 보고 2배 받기`를 보여 준다. 광고를 보는 동안은
    // 재생하느라 준비 상태가 바뀌므로 건드리지 않는다.
    private void RefreshAdDoubleButton()
    {
        if (_isWatchingAd) return;

        bool isVisible = _displayedReward.HasValue &&
                         (double)_displayedReward.Value.Reward > 0d &&
                         _adRewardService != null &&
                         _adRewardService.GetAvailability(EAdPlacement.OfflineDouble) ==
                         EAdAvailability.Available;
        _view.SetAdDoubleVisible(isVisible);
    }

    private void OnAdDoubleRequested()
    {
        ShowAdDoubleAsync().Forget();
    }

    private async UniTaskVoid ShowAdDoubleAsync()
    {
        if (!_displayedReward.HasValue || _isWatchingAd || _adRewardService == null) return;

        _isWatchingAd = true;
        _view.SetBusy(true);
        EAdShowResult result = await _adRewardService.ShowAsync(EAdPlacement.OfflineDouble);
        if (this == null) return;

        _isWatchingAd = false;
        if (!_displayedReward.HasValue) return;

        if (result == EAdShowResult.Rewarded)
        {
            ClaimDoubled();
            return;
        }

        _view.SetBusy(false);
        _toast?.Show(GetAdFailureMessage(result));
        RefreshAdDoubleButton();
    }

    private void ClaimDoubled()
    {
        double multiplier = _adRewardService.Table.OfflineRewardMultiplier;
        if (!_offlineRewardManager.TryClaim(multiplier, out OfflineRewardResult claimed))
        {
            _view.SetBusy(false);
            return;
        }

        _displayedReward = claimed;
        _view.ShowDoubledReward(claimed.Reward, claimed.TicketReward);
        PlayClaim(claimed);
    }

    private static string GetAdFailureMessage(EAdShowResult result)
    {
        switch (result)
        {
            case EAdShowResult.ClosedEarly:
                return UiMessages.AdClosedEarly;
            case EAdShowResult.Failed:
                return UiMessages.AdFailed;
            default:
                return UiMessages.AdNotReady;
        }
    }

    private void OnPresentationCompleted()
    {
        if (!_displayedReward.HasValue) return;

        _offlineRewardManager?.CompletePresentation();
        _displayedReward = null;
    }
}
