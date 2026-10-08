using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 보상형 광고를 한 번 보여 주고 보상을 지급한다. 광고를 어떻게 보여 주는지는 IAdService가, 하루 횟수와 부스트
// 남은 시간의 저장은 SlimeManager가 맡고, 여기서는 볼 수 있는지 판단하고, 광고가 겹치지 않게 하고, 보상 신호가 온
// 한 번에 보상을 지급한다.
//
// 지급하는 것은 뽑기권과 포인트 부스트 시간이다. 오프라인 보상 2배는 팝업이 수령할 때 곱하므로 여기서는
// 보상 신호만 낸다. 부스트 배율을 포인트 계산에 곱는 일도 이 서비스 밖에서 한다.
//
// 플랫폼 분기는 CreateAdService 한 곳에 있다. 에디터는 가짜 광고를 쓰고, 그 밖의 빌드는 AdMob을 붙일 때까지
// 광고가 없는 것으로 다룬다. 에디터의 가짜 광고가 기기 동작을 대신하지 않게 하려는 것이다.
public sealed class AdRewardService : MonoBehaviour
{
    // 앱이 멈췄다 돌아온 첫 프레임의 큰 간격을 부스트 시간에서 빼지 않게 한다.
    private const float MaxElapsedPerFrame = 1f;

    [SerializeField] private AdRewardTableSO _table;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private CurrencyManager _currencyManager;

    private IAdService _adService;
    private bool _isRequesting;

    public AdRewardTableSO Table => _table;
    public bool IsShowingAd => _adService != null && _adService.IsShowing;

    // 부스트가 켜져 있으면 표의 배율, 아니면 1이다. 터치와 자동 생산만 이 값을 포인트 계산에 넘긴다.
    public double PointBoostMultiplier =>
        enabled && _slimeManager.AdRewards.HasPointBoost ? _table.PointBoostMultiplier : 1d;

    // 에디터에서 중도 닫기와 불러오기 실패를 흉내 낼 때 가짜 광고를 꺼내 쓴다.
    public IAdService AdService => _adService;

    public event Action<EAdPlacement> RewardEarned;
    public event Action ReadyChanged;

    private void Awake()
    {
        if (_table == null || _slimeManager == null || _currencyManager == null)
        {
            Debug.LogError("광고 보상 서비스의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _adService = CreateAdService();
    }

    private void Start()
    {
        if (!enabled) return;

        _adService.ReadyChanged += OnReadyChanged;
        _adService.Preload();
    }

    private void Update()
    {
        // 광고를 보는 동안은 앱이 멈춘 것과 같으므로 부스트 시간도 흐르지 않게 한다.
        if (!_slimeManager.IsInitialized || _adService.IsShowing) return;

        float elapsed = Mathf.Min(Time.unscaledDeltaTime, MaxElapsedPerFrame);
        _slimeManager.ElapseAdPointBoost(elapsed);
    }

    private void OnDestroy()
    {
        if (_adService != null)
        {
            _adService.ReadyChanged -= OnReadyChanged;
        }
    }

    public EAdAvailability GetAvailability(EAdPlacement placement)
    {
        if (!enabled || !_slimeManager.IsInitialized) return EAdAvailability.NotReady;

        int dayIndex = GetTodayIndex();
        AdRewardProgress progress = _slimeManager.AdRewards;
        switch (placement)
        {
            case EAdPlacement.Ticket:
                if (!_slimeManager.IsGachaUnlocked) return EAdAvailability.Locked;
                if (progress.GetTicketCount(dayIndex) >= _table.TicketDailyLimit)
                {
                    return EAdAvailability.DailyLimitReached;
                }

                break;
            case EAdPlacement.PointBoost:
                if (progress.GetPointBoostCount(dayIndex) >= _table.PointBoostDailyLimit)
                {
                    return EAdAvailability.DailyLimitReached;
                }

                if (GetPointBoostRemainingSeconds() + _table.PointBoostSecondsPerAd >
                    _table.PointBoostMaxRemainingSeconds)
                {
                    return EAdAvailability.BoostFull;
                }

                break;
        }

        return _adService.IsReady ? EAdAvailability.Available : EAdAvailability.NotReady;
    }

    // 표의 상한보다 큰 값이 저장돼 있어도(상한을 낮춘 경우) 표의 상한으로 잘라서 돌려준다.
    public float GetPointBoostRemainingSeconds()
    {
        if (!enabled || !_slimeManager.IsInitialized) return 0f;

        double remaining = _slimeManager.AdRewards.PointBoostRemainingSeconds;
        return (float)Math.Min(remaining, _table.PointBoostMaxRemainingSeconds);
    }

    public int GetRemainingDailyCount(EAdPlacement placement)
    {
        if (!enabled || !_slimeManager.IsInitialized) return 0;

        int dayIndex = GetTodayIndex();
        AdRewardProgress progress = _slimeManager.AdRewards;
        switch (placement)
        {
            case EAdPlacement.Ticket:
                return Mathf.Max(0, _table.TicketDailyLimit - progress.GetTicketCount(dayIndex));
            case EAdPlacement.PointBoost:
                return Mathf.Max(0, _table.PointBoostDailyLimit - progress.GetPointBoostCount(dayIndex));
            default:
                return int.MaxValue;
        }
    }

    public async UniTask<EAdShowResult> ShowAsync(EAdPlacement placement)
    {
        if (!enabled || _adService == null) return EAdShowResult.NotReady;
        if (_isRequesting) return EAdShowResult.Busy;

        EAdAvailability availability = GetAvailability(placement);
        if (availability == EAdAvailability.NotReady) return EAdShowResult.NotReady;
        if (availability != EAdAvailability.Available) return EAdShowResult.NotAllowed;

        _isRequesting = true;
        try
        {
            EAdShowResult result = await _adService.ShowRewardedAsync(destroyCancellationToken);
            if (result != EAdShowResult.Rewarded) return result;

            if (!TryGrant(placement))
            {
                Debug.LogError($"광고 보상을 지급하지 못했습니다. : {placement}", this);
                return EAdShowResult.Failed;
            }

            RewardEarned?.Invoke(placement);
            return EAdShowResult.Rewarded;
        }
        finally
        {
            _isRequesting = false;
        }
    }

    // 광고가 하루 경계를 넘겨 길어질 수 있어 날짜는 지급하는 순간에 다시 잰다.
    private bool TryGrant(EAdPlacement placement)
    {
        int dayIndex = GetTodayIndex();
        switch (placement)
        {
            case EAdPlacement.Ticket:
                return TryGrantTicket(dayIndex);
            case EAdPlacement.PointBoost:
                return _slimeManager.TryRecordAdPointBoost(
                    dayIndex,
                    _table.PointBoostSecondsPerAd,
                    _table.PointBoostMaxRemainingSeconds,
                    _table.PointBoostDailyLimit);
            default:
                return true;
        }
    }

    // 횟수를 먼저 세고 지갑에 넣는다. 지갑에 넣지 못하면 센 횟수를 되돌린다.
    private bool TryGrantTicket(int dayIndex)
    {
        int reward = _table.TicketReward;
        bool granted = EconomyTransactionService.TryGrantAfterConsume(
            _currencyManager,
            ECurrencyType.GachaTicket,
            (double)reward,
            () => _slimeManager.TryRecordAdTicketReward(dayIndex, _table.TicketDailyLimit),
            _slimeManager.UndoAdTicketReward);
        if (!granted) return false;

        _slimeManager.RecordGachaTicketsObtained(reward);
        return true;
    }

    private static int GetTodayIndex()
    {
        return AdRewardProgress.GetDayIndex(ServerClock.TrustedUtcNow);
    }

    private void OnReadyChanged() => ReadyChanged?.Invoke();

    private static IAdService CreateAdService()
    {
#if UNITY_EDITOR
        return new FakeAdService();
#else
        return new NullAdService();
#endif
    }
}
