using UnityEngine;

[RequireComponent(typeof(OfflineRewardPopupUI))]
public sealed class OfflineRewardPopupController : MonoBehaviour
{
    [SerializeField] private OfflineRewardPopupUI _view;

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

        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager가 없어 오프라인 보상 팝업을 초기화할 수 없습니다.", this);
            enabled = false;
            return;
        }

        _view.ConfirmRequested += OnConfirmRequested;
        _view.PresentationCompleted += OnPresentationCompleted;
        GameManager.Instance.OnOfflineRewardReady += ShowPendingReward;
        ShowPendingReward();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.ConfirmRequested -= OnConfirmRequested;
            _view.PresentationCompleted -= OnPresentationCompleted;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnOfflineRewardReady -= ShowPendingReward;
        }
    }

    private void ShowPendingReward()
    {
        if (_displayedReward.HasValue ||
            GameManager.Instance == null ||
            !GameManager.Instance.TryConsumeOfflineReward(out OfflineRewardResult result))
        {
            return;
        }

        _displayedReward = result;
        _view.Show(result.ElapsedTime, result.Reward);
    }

    private void OnConfirmRequested()
    {
        if (!_displayedReward.HasValue ||
            GameManager.Instance == null ||
            !GameManager.Instance.TryClaimOfflineReward())
        {
            return;
        }

        OfflineRewardResult result = _displayedReward.Value;
        float duration = _view.PlayCollect(result.ElapsedTime);

        PointCountUpEvents.Request(new PointCountUpRequest(
            result.PointBeforeReward,
            result.PointAfterReward,
            duration));
    }

    private void OnPresentationCompleted()
    {
        if (!_displayedReward.HasValue) return;

        GameManager.Instance?.CompleteOfflineRewardPresentation();
        _displayedReward = null;
    }
}
