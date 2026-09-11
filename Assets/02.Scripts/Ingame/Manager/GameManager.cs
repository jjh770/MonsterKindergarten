using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static event Action OnAllDataInitialized;
    public event Action OnGameplayActivated;

    [Header("Loading")]
    [Tooltip("이 시간 안에 저장 데이터를 불러오지 못하면 로그인 화면으로 돌려보냅니다.")]
    [SerializeField, Min(1f)] private float _initializationTimeoutSeconds = 30f;

    private bool _isUpgradeInitialized;
    private bool _isSlimeInitialized;
    private bool _isCurrencyInitialized;
    private bool _isAllInitialized;
    private bool _isReturningToLogin;

    // TODO : 데이터 초기화 고려사항
    // 1. 이렇게 전체 데이터를 이벤트 구독해서 확인하는 방법도 있지만
    // 2. GameManager에서 모든 매니저의 데이터를 초기화하라고 시키는 방법도 있음. (이러면 GameManager에서 순차적으로 진행하기 때문에 살짝 느릴 수 있다.)
    // 3. 아예 로딩씬에서 데이터를 모두 초기화하고 게임 씬으로 넘어가는 방법도 있다.
    public bool IsAllDataInitialized => _isAllInitialized;

    private bool _isGameplayActive;
    public bool IsGameplayActive
    {
        get => _isGameplayActive && !GameplaySaveGate.IsResetting;
        private set
        {
            if (_isGameplayActive == value) return;

            _isGameplayActive = value;
            if (value)
            {
                OnGameplayActivated?.Invoke();
            }
        }
    }

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
        UpgradeManager.OnDataInitialized += OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized += OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized += OnCurrencyDataInitialized;
        SaveDataLoadGuard.Failed += OnSaveDataLoadFailed;
        // 이 구독이 게임플레이를 켜는 유일한 경로다. 매니저가 없으면 커튼은 걷히는데
        // 아무것도 조작할 수 없는 화면이 되므로 조용히 넘어가면 안 된다.
        if (OfflineRewardManager.Instance == null)
        {
            Debug.LogError("오프라인 보상 매니저가 씬에 없습니다.", this);
        }
        else
        {
            OfflineRewardManager.Instance.PresentationBlockChanged +=
                OnOfflineRewardBlockChanged;
        }

        WatchInitializationTimeout().Forget();

        // 이미 실패가 신고된 경우
        if (SaveDataLoadGuard.HasFailure)
        {
            OnSaveDataLoadFailed();
        }
    }

    private void OnDestroy()
    {
        UpgradeManager.OnDataInitialized -= OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized -= OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized -= OnCurrencyDataInitialized;
        SaveDataLoadGuard.Failed -= OnSaveDataLoadFailed;
        if (OfflineRewardManager.Instance != null)
        {
            OfflineRewardManager.Instance.PresentationBlockChanged -=
                OnOfflineRewardBlockChanged;
        }
    }

    // 불러오기가 실패가 아니라 멈추면 아무도 신고하지 않는다.
    //
    // 각 매니저는 읽기에 실패했을 때만 신고한다. 응답이 아예 오지 않으면 실패도
    // 아니어서 OnAllDataInitialized가 영영 발화하지 않고, 커튼이 걷히지 않은 채
    // 남는다. 그 상태에서는 뒤로 가기도 커튼에 가려 강제 종료 말고 나갈 길이 없다.
    //
    // 로그인은 이미 네트워크를 통과한 뒤이므로, 여기서 걸리는 것은 연결이 로그인
    // 직후에 끊긴 경우다. Unreachable로 신고하면 기존 경로가 로그인 화면으로
    // 돌려보내고 재시도를 안내한다. 초기화 패널은 열리지 않으므로 멀쩡한 진행도를
    // 지울 위험도 없다.
    private async UniTaskVoid WatchInitializationTimeout()
    {
        await UniTask.Delay(
                TimeSpan.FromSeconds(_initializationTimeoutSeconds),
                DelayType.Realtime,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();

        if (this == null || _isAllInitialized) return;
        if (SaveDataLoadGuard.HasFailure || GameplaySaveGate.IsResetting) return;

        SaveDataLoadGuard.Report(
            ESaveLoadFailure.Unreachable,
            $"저장 데이터를 {_initializationTimeoutSeconds:0}초 안에 불러오지 못했습니다.");
    }

    // 저장 데이터를 확인하지 못한 세션은 게임에 들어가지 않는다.
    //
    // 그냥 두면 OnAllDataInitialized가 영영 발화하지 않아 화면이 멈추고,
    // 기본값으로 진행시키면 첫 저장이 확인하지 못한 원본을 덮어써 복구할 수 없다.
    // 로그인 화면으로 돌려보내 다시 시도하게 한다.
    private void OnSaveDataLoadFailed()
    {
        if (_isReturningToLogin) return;

        _isReturningToLogin = true;
        LoginScene.PendingLoadFailure = SaveDataLoadGuard.Failure;
        AudioManager.Instance?.SaveVolumeSettings();
        AccountManager.Instance?.Logout();

        if (SceneManagerEx.Instance != null)
        {
            SceneManagerEx.Instance.LoadLoginScene();
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
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
            if (!HasConsistentStoredSaveData()) return;

            _isAllInitialized = true;
            InitializeTutorialProgress();
            OfflineRewardManager.Instance?.Grant();
            OnAllDataInitialized?.Invoke();
        }
    }

    // 세 저장 문서는 함께 만들어지고 함께 지워진다. 튜토리얼을 마칠 때 셋을 같이
    // 저장하고, 초기화와 계정 삭제도 셋을 한 배치로 지운다.
    //
    // 그래서 일부만 없는 상태는 신규 계정이 아니라 결손이다. 기본값으로 출발하면
    // 남은 도메인은 복원되고 없는 도메인만 초기화된 채 시작하며, 다음 저장이 그
    // 손실을 확정한다. 도메인별 가드는 자기 안만 보므로 여기서 교차로 확인한다.
    private bool HasConsistentStoredSaveData()
    {
        bool hasCurrency = CurrencyManager.Instance.HasStoredSaveData;
        bool hasSlime = SlimeManager.Instance.HasStoredSaveData;
        bool hasUpgrade = UpgradeManager.Instance.HasStoredSaveData;

        if (hasCurrency == hasSlime && hasSlime == hasUpgrade) return true;

        SaveDataLoadGuard.Report(
            ESaveLoadFailure.Unreadable,
            "저장 문서가 일부만 있습니다. : " +
            $"재화 {Describe(hasCurrency)}, " +
            $"슬라임 {Describe(hasSlime)}, " +
            $"업그레이드 {Describe(hasUpgrade)}");
        return false;
    }

    private static string Describe(bool hasStoredSaveData)
    {
        return hasStoredSaveData ? "있음" : "없음";
    }

    private void InitializeTutorialProgress()
    {
        bool hasExistingProgress =
            CurrencyManager.Instance.HasExistingProgress ||
            SlimeManager.Instance.HasExistingProgress ||
            UpgradeManager.Instance.HasExistingProgress;

        TutorialProgress.Initialize(AccountManager.Instance.UserId);
        TutorialProgress.Register(
            TutorialIds.Main,
            order: 0,
            completeByDefault: hasExistingProgress);
        TutorialProgress.Register(
            TutorialIds.DisplayRoom,
            order: (int)UnlockGrades.DisplayRoom,
            completeByDefault: false,
            completeStoredIncomplete: false);
        TutorialProgress.Register(
            TutorialIds.HigherGradeSpawn,
            order: (int)SlimeManager.Instance.HigherGradeSpawnUnlockGrade,
            completeByDefault: SlimeManager.Instance.IsHigherGradeSpawnUnlocked,
            completeStoredIncomplete: false);
        // 가챠는 이번에 추가된 기능이라 이미 Lv.7을 넘긴 플레이어도 안내를 받아야
        // 한다. 해금 여부로 완료 처리하면 지금 플레이 중인 사람 전원이 건너뛴다.
        TutorialProgress.Register(
            TutorialIds.Gacha,
            order: (int)UnlockGrades.Gacha,
            completeByDefault: false,
            completeStoredIncomplete: false);
        GameplaySaveGate.SetSavingEnabled(
            TutorialProgress.IsCompleted(TutorialIds.Main));
    }

    public async UniTask CompleteTutorialAsync()
    {
        if (GameplaySaveGate.IsResetting) return;
        if (!TutorialProgress.IsRegistered(TutorialIds.Main) ||
            TutorialProgress.IsCompleted(TutorialIds.Main))
        {
            GameplaySaveGate.SetSavingEnabled(true);
            return;
        }

        GameplaySaveGate.SetSavingEnabled(true);

        await UniTask.WhenAll(
            CurrencyManager.Instance.SaveCurrentAsync(),
            SlimeManager.Instance.SaveCurrentAsync(),
            UpgradeManager.Instance.SaveCurrentAsync());

        TutorialProgress.MarkCompleted(TutorialIds.Main);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!_isAllInitialized || GameplaySaveGate.IsResetting) return;

        if (pauseStatus)
        {
            // 받지 않은 보상이 있으면 마지막 저장 시간을 유지해 다음 실행에서 누적한다.
            if (!HasPendingOfflineReward)
            {
                CurrencyManager.Instance.SaveCurrent();
                // 간격을 기다리다 프로세스가 멈추면 클라우드에 못 올라간다.
                CurrencyManager.Instance.FlushPendingSave();
                UpgradeManager.Instance?.FlushPendingSave();
            }
        }
        else
        {
            OfflineRewardManager.Instance?.GrantAfterResync().Forget();
        }
    }

    private static bool HasPendingOfflineReward =>
        OfflineRewardManager.Instance != null &&
        OfflineRewardManager.Instance.HasPending;

    // 보상 팝업이 화면을 잡는 동안에는 플레이를 멈춘다. 판단은 보상 쪽이 하고
    // 실제로 끄고 켜는 것은 여기서 한다. IsGameplayActive는 초기화·진행도
    // 리셋과도 얽혀 있어 주인이 하나여야 한다.
    private void OnOfflineRewardBlockChanged(bool isBlocked)
    {
        IsGameplayActive = !isBlocked;
    }
}
