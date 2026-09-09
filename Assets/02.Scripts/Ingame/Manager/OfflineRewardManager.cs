using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 자리를 비운 동안의 자동 생산을 계산해 지급까지 책임진다.
//
// GameManager에서 떼어 냈다. 그쪽의 본업은 세 매니저의 초기화를 조율하는 것인데,
// 이 계산과 상태가 파일의 절반을 차지해 정작 초기화가 묻혀 있었다.
//
// 플레이를 멈추는 것은 여전히 GameManager가 한다. 여기서는 팝업이 화면을 잡아야
// 하는지만 알린다. IsGameplayActive는 초기화·진행도 리셋과도 얽혀 있어서, 두 곳이
// 각자 켜고 끄면 누가 마지막에 무엇을 했는지 알 수 없게 된다.
public sealed class OfflineRewardManager : MonoBehaviour
{
    public static OfflineRewardManager Instance { get; private set; }

    [SerializeField] private float _minimumOfflineSeconds = 60f;
    [SerializeField] private float _maximumOfflineHours = 8f;
    [SerializeField, Range(0f, 1f)] private float _offlineRewardEfficiency = 0.5f;

    private OfflineRewardResult? _pendingReward;
    private bool _isConsumed;
    private bool _isClaimed;

    // 계산이 끝났지만 아직 발표하지 못한 보상이 있는지. 앱이 내려갈 때 마지막 저장
    // 시각을 유지할지 판단하는 데 쓴다.
    public bool HasPending => _pendingReward.HasValue;

    // 이미 받은 보상의 연출이 끝나기 전인지. 이때 다시 계산하면 받았다는 표시가
    // 풀려 같은 보상을 두 번 받을 수 있다.
    private bool IsPresentingClaimedReward => _pendingReward.HasValue && _isClaimed;

    // true면 팝업이 화면을 잡아야 하고 false면 놓아도 된다는 뜻이다. 발표를 미루는
    // 동안에는 아무것도 보내지 않는다. 플레이는 그대로 이어져야 하기 때문이다.
    public event Action<bool> PresentationBlockChanged;

    public event Action Ready;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        TutorialManager.Finished += TryPresent;
    }

    private void OnDestroy()
    {
        TutorialManager.Finished -= TryPresent;
    }

    // 보정값은 기기 시계와의 차이라, 실행 중에 시계가 바뀌면 함께 어긋난다.
    // 복귀할 때마다 다시 맞춘다. 실패해도 기존 보정값으로 지급한다. 버리면 오프라인
    // 상태로 돌아온 정직한 플레이어가 쌓인 보상을 잃는다.
    public async UniTaskVoid GrantAfterResync()
    {
        if (IsPresentingClaimedReward) return;

        await ServerClock.TrySync(AccountManager.Instance?.UserId, force: true);

        if (this == null || GameplaySaveGate.IsResetting) return;
        if (GameManager.Instance == null ||
            !GameManager.Instance.IsAllDataInitialized) return;
        if (IsPresentingClaimedReward) return;

        Grant();
    }

    public void Grant()
    {
        DateTime lastSaveTime = CurrencyManager.Instance.LastSaveTime;
        DateTime currentTime = ServerClock.TrustedUtcNow;

        if (lastSaveTime == DateTime.MinValue || currentTime <= lastSaveTime)
        {
            SettleWithoutReward();
            return;
        }

        double elapsedSeconds = (currentTime - lastSaveTime).TotalSeconds;
        if (elapsedSeconds < _minimumOfflineSeconds)
        {
            SettleWithoutReward();
            return;
        }

        double maximumSeconds = Math.Max(0f, _maximumOfflineHours) * 60d * 60d;
        elapsedSeconds = Math.Min(elapsedSeconds, maximumSeconds);

        double pointPerSecond = CalculateAutoPointPerSecond();
        double reward = Math.Floor(
            pointPerSecond * elapsedSeconds * _offlineRewardEfficiency);

        if (reward <= 0d)
        {
            SettleWithoutReward();
            return;
        }

        // 발표를 미룬 사이에도 플레이는 이어지므로 클릭마다 LastSaveTime이 갱신된다.
        // 그 뒤 다시 계산하면 경과 시간이 짧아지므로, 아직 받지 않은 보상이 더
        // 크면 그대로 둔다. 팝업이 떠 있는 동안의 누적은 그대로 동작한다.
        bool keepPendingReward =
            _pendingReward.HasValue &&
            !_isClaimed &&
            _pendingReward.Value.Reward >= (Currency)reward;

        if (!keepPendingReward)
        {
            _isConsumed = false;
            _isClaimed = false;
            _pendingReward = new OfflineRewardResult(
                TimeSpan.FromSeconds(elapsedSeconds),
                reward,
                CurrencyManager.Instance.Point,
                CurrencyManager.Instance.Point + (Currency)reward);
        }

        TryPresent();
    }

    // 계산과 발표를 나눈다. 튜토리얼이 도는 동안 팝업을 띄우면 서로의 입력을 막아
    // 어느 쪽도 진행할 수 없으므로, 보상은 계산해 두고 튜토리얼이 끝난 뒤 띄운다.
    private void TryPresent()
    {
        if (!_pendingReward.HasValue || _isClaimed) return;
        if (TutorialManager.IsRunning) return;

        // 미뤄 둔 사이 포인트가 늘었을 수 있다. 카운트업 시작값은 발표 시점에 잡는다.
        OfflineRewardResult pendingReward = _pendingReward.Value;
        Currency pointBeforeReward = CurrencyManager.Instance.Point;
        _pendingReward = new OfflineRewardResult(
            pendingReward.ElapsedTime,
            pendingReward.Reward,
            pointBeforeReward,
            pointBeforeReward + pendingReward.Reward);

        _isConsumed = false;
        PresentationBlockChanged?.Invoke(true);
        Ready?.Invoke();
    }

    // 줄 것이 없으면 그동안의 진행도만 저장하고 플레이를 연다. 첫 진입에서
    // 게임플레이가 켜지는 경로이기도 하다.
    private void SettleWithoutReward()
    {
        CurrencyManager.Instance.SaveCurrent();
        if (_pendingReward.HasValue) return;

        PresentationBlockChanged?.Invoke(false);
    }

    private static double CalculateAutoPointPerSecond()
    {
        double total = 0d;

        foreach (SlimeInstance instance in SlimeManager.Instance.Status.ActiveSlimes)
        {
            if (instance.Location != ESlimeLocation.MainStage)
            {
                continue;
            }

            ESlimeGrade grade = instance.Grade;
            Slime slime = SlimeManager.Instance.Get(grade);
            if (slime == null || slime.SpecData.AutoClickInterval <= 0f) continue;

            double point = PointCalculator.Calculate(
                slime.SpecData.Point,
                grade,
                EClickType.Auto);

            total += point / slime.SpecData.AutoClickInterval;
        }

        return total;
    }

    public bool TryConsume(out OfflineRewardResult result)
    {
        if (!_pendingReward.HasValue || _isConsumed)
        {
            result = default;
            return false;
        }

        result = _pendingReward.Value;
        _isConsumed = true;
        return true;
    }

    public bool TryGetCurrent(out OfflineRewardResult result)
    {
        if (!_pendingReward.HasValue)
        {
            result = default;
            return false;
        }

        result = _pendingReward.Value;
        return true;
    }

    public bool TryClaim()
    {
        if (!_pendingReward.HasValue || _isClaimed)
        {
            return false;
        }

        OfflineRewardResult result = _pendingReward.Value;
        CurrencyManager.Instance.Add(ECurrencyType.Point, result.Reward);
        _isClaimed = true;
        return true;
    }

    public void CompletePresentation()
    {
        if (!_isClaimed) return;

        _pendingReward = null;
        _isConsumed = false;
        _isClaimed = false;
        PresentationBlockChanged?.Invoke(false);
    }
}

public readonly struct OfflineRewardResult
{
    public TimeSpan ElapsedTime { get; }
    public Currency Reward { get; }
    public Currency PointBeforeReward { get; }
    public Currency PointAfterReward { get; }

    public OfflineRewardResult(
        TimeSpan elapsedTime,
        Currency reward,
        Currency pointBeforeReward,
        Currency pointAfterReward)
    {
        ElapsedTime = elapsedTime;
        Reward = reward;
        PointBeforeReward = pointBeforeReward;
        PointAfterReward = pointAfterReward;
    }
}
