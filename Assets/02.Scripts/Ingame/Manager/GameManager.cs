using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public static event Action OnAllDataInitialized;
    public event Action OnOfflineRewardReady;

    [Header("Offline Reward")]
    [SerializeField] private float _minimumOfflineSeconds = 60f;
    [SerializeField] private float _maximumOfflineHours = 8f;
    [SerializeField, Range(0f, 1f)] private float _offlineRewardEfficiency = 0.5f;

    private bool _isUpgradeInitialized;
    private bool _isSlimeInitialized;
    private bool _isCurrencyInitialized;
    private bool _isAllInitialized;
    private OfflineRewardResult? _pendingOfflineReward;
    private bool _isOfflineRewardConsumed;
    private bool _isOfflineRewardClaimed;

    // TODO : 데이터 초기화 고려사항
    // 1. 이렇게 전체 데이터를 이벤트 구독해서 확인하는 방법도 있지만
    // 2. GameManager에서 모든 매니저의 데이터를 초기화하라고 시키는 방법도 있음. (이러면 GameManager에서 순차적으로 진행하기 때문에 살짝 느릴 수 있다.)
    // 3. 아예 로딩씬에서 데이터를 모두 초기화하고 게임 씬으로 넘어가는 방법도 있다.
    public bool IsAllDataInitialized => _isAllInitialized;
    public bool IsGameplayActive { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        UpgradeManager.OnDataInitialized += OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized += OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized += OnCurrencyDataInitialized;
    }

    private void OnDestroy()
    {
        UpgradeManager.OnDataInitialized -= OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized -= OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized -= OnCurrencyDataInitialized;
    }

    private void OnUpgradeDataInitialized()
    {
        _isUpgradeInitialized = true;
        TryInvokeAllInitialized();
    }

    private void OnSlimeDataInitialized()
    {
        _isSlimeInitialized = true;
        TryInvokeAllInitialized();
    }

    private void OnCurrencyDataInitialized()
    {
        _isCurrencyInitialized = true;
        TryInvokeAllInitialized();
    }

    private void TryInvokeAllInitialized()
    {
        if (_isAllInitialized) return;

        if (_isUpgradeInitialized && _isSlimeInitialized && _isCurrencyInitialized)
        {
            _isAllInitialized = true;
            InitializeTutorialProgress();
            GrantOfflineReward();
            OnAllDataInitialized?.Invoke();
        }
    }

    private void InitializeTutorialProgress()
    {
        bool hasExistingProgress =
            CurrencyManager.Instance.HasExistingProgress ||
            SlimeManager.Instance.HasExistingProgress ||
            UpgradeManager.Instance.HasExistingProgress;

        TutorialProgress.Initialize(
            AccountManager.Instance.UserId,
            hasExistingProgress);
        GameplaySaveGate.SetSavingEnabled(TutorialProgress.IsCompleted);
    }

    public async UniTask CompleteTutorialAsync()
    {
        if (!TutorialProgress.IsInitialized || TutorialProgress.IsCompleted)
        {
            GameplaySaveGate.SetSavingEnabled(true);
            return;
        }

        GameplaySaveGate.SetSavingEnabled(true);

        await UniTask.WhenAll(
            CurrencyManager.Instance.SaveCurrentAsync(),
            SlimeManager.Instance.SaveCurrentAsync(),
            UpgradeManager.Instance.SaveCurrentAsync());

        TutorialProgress.MarkCompleted();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!_isAllInitialized) return;

        if (pauseStatus)
        {
            // 받지 않은 보상이 있으면 마지막 저장 시간을 유지해 다음 실행에서 누적한다.
            if (!_pendingOfflineReward.HasValue)
            {
                CurrencyManager.Instance.SaveCurrent();
            }
        }
        else if (!_pendingOfflineReward.HasValue || !_isOfflineRewardClaimed)
        {
            GrantOfflineReward();
        }
    }

    private void OnApplicationQuit()
    {
        if (_isAllInitialized && !_pendingOfflineReward.HasValue)
        {
            CurrencyManager.Instance.SaveCurrent();
        }
    }

    private void GrantOfflineReward()
    {
        DateTime lastSaveTime = CurrencyManager.Instance.LastSaveTime;
        DateTime currentTime = DateTime.UtcNow;

        if (lastSaveTime == DateTime.MinValue || currentTime <= lastSaveTime)
        {
            CurrencyManager.Instance.SaveCurrent();
            ActivateGameplayIfNoPendingReward();
            return;
        }

        double elapsedSeconds = (currentTime - lastSaveTime).TotalSeconds;
        if (elapsedSeconds < _minimumOfflineSeconds)
        {
            CurrencyManager.Instance.SaveCurrent();
            ActivateGameplayIfNoPendingReward();
            return;
        }

        double maximumSeconds = Math.Max(0f, _maximumOfflineHours) * 60d * 60d;
        elapsedSeconds = Math.Min(elapsedSeconds, maximumSeconds);

        double pointPerSecond = CalculateAutoPointPerSecond();
        double reward = Math.Floor(pointPerSecond * elapsedSeconds * _offlineRewardEfficiency);

        if (reward > 0d)
        {
            Currency pointBeforeReward = CurrencyManager.Instance.Point;
            Currency pointAfterReward = pointBeforeReward + (Currency)reward;

            IsGameplayActive = false;
            _isOfflineRewardConsumed = false;
            _isOfflineRewardClaimed = false;
            _pendingOfflineReward = new OfflineRewardResult(
                TimeSpan.FromSeconds(elapsedSeconds),
                reward,
                pointBeforeReward,
                pointAfterReward);

            OnOfflineRewardReady?.Invoke();
            Debug.Log($"오프라인 보상 계산: {reward:N0} Point / {elapsedSeconds:N0}초");
        }
        else
        {
            CurrencyManager.Instance.SaveCurrent();
            ActivateGameplayIfNoPendingReward();
        }
    }

    private static double CalculateAutoPointPerSecond()
    {
        double total = 0d;

        foreach (var pair in SlimeManager.Instance.Status.ActiveSlimes)
        {
            Slime slime = SlimeManager.Instance.Get(pair.Key);
            if (slime == null || pair.Value <= 0 || slime.SpecData.AutoClickInterval <= 0f) continue;

            double point = PointCalculator.Calculate(
                slime.SpecData.Point,
                pair.Key,
                EClickType.Auto);

            total += point / slime.SpecData.AutoClickInterval * pair.Value;
        }

        return total;
    }

    public bool TryConsumeOfflineReward(out OfflineRewardResult result)
    {
        if (!_pendingOfflineReward.HasValue || _isOfflineRewardConsumed)
        {
            result = default;
            return false;
        }

        result = _pendingOfflineReward.Value;
        _isOfflineRewardConsumed = true;
        return true;
    }

    public bool TryGetCurrentOfflineReward(out OfflineRewardResult result)
    {
        if (!_pendingOfflineReward.HasValue)
        {
            result = default;
            return false;
        }

        result = _pendingOfflineReward.Value;
        return true;
    }

    public void CompleteOfflineRewardPresentation()
    {
        if (!_isOfflineRewardClaimed) return;

        _pendingOfflineReward = null;
        _isOfflineRewardConsumed = false;
        _isOfflineRewardClaimed = false;
        IsGameplayActive = true;
    }

    public bool TryClaimOfflineReward()
    {
        if (!_pendingOfflineReward.HasValue || _isOfflineRewardClaimed)
        {
            return false;
        }

        OfflineRewardResult result = _pendingOfflineReward.Value;
        CurrencyManager.Instance.Add(ECurrencyType.Point, result.Reward);
        _isOfflineRewardClaimed = true;
        Debug.Log($"오프라인 보상 수령: {result.Reward} Point");
        return true;
    }

    private void ActivateGameplayIfNoPendingReward()
    {
        if (!_pendingOfflineReward.HasValue)
        {
            IsGameplayActive = true;
        }
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
