using UnityEngine;

[RequireComponent(typeof(OfflineRewardPopupUI))]
public sealed class OfflineRewardPopupController : MonoBehaviour
{
    [SerializeField] private OfflineRewardPopupUI _view;
    [SerializeField] private OfflineRewardManager _offlineRewardManager;

    private OfflineRewardResult? _displayedReward;

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
        _offlineRewardManager.Ready += ShowPendingReward;
        ShowPendingReward();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.ConfirmRequested -= OnConfirmRequested;
            _view.PresentationCompleted -= OnPresentationCompleted;
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
    }

    private void OnConfirmRequested()
    {
        if (!_displayedReward.HasValue ||
            _offlineRewardManager == null ||
            !_offlineRewardManager.TryClaim())
        {
            return;
        }

        OfflineRewardResult result = _displayedReward.Value;
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

    private void OnPresentationCompleted()
    {
        if (!_displayedReward.HasValue) return;

        _offlineRewardManager?.CompletePresentation();
        _displayedReward = null;
    }
}
