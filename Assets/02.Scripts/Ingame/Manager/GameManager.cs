using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event Action AllDataInitialized;
    public event Action OnGameplayActivated;

    [Header("Loading")]
    [Tooltip("이 시간 안에 저장 데이터를 불러오지 못하면 로그인 화면으로 돌려보냅니다.")]
    [SerializeField, Min(1f)] private float _initializationTimeoutSeconds = 30f;

    [Header("Scene References")]
    [SerializeField] private CurrencyManager _currencyManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private UpgradeManager _upgradeManager;
    [SerializeField] private OfflineRewardManager _offlineRewardManager;

    private bool _isAllInitialized;
    private bool _isReturningToLogin;
    private readonly List<IGameDataDomainManager> _dataManagers = new();
    private readonly List<GameDataDomainDefinition> _dataDomains = new();

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
        if (!TryBindDataManagers())
        {
            enabled = false;
            return;
        }

        foreach (IGameDataDomainManager manager in _dataManagers)
        {
            manager.DataInitialized += OnDataInitialized;
        }
        SaveDataLoadGuard.Failed += OnSaveDataLoadFailed;
        _offlineRewardManager.PresentationBlockChanged +=
            OnOfflineRewardBlockChanged;

        WatchInitializationTimeout().Forget();

        // 이미 실패가 신고된 경우
        if (SaveDataLoadGuard.HasFailure)
        {
            OnSaveDataLoadFailed();
            return;
        }

        // 실행 순서가 바뀌어 Start 전에 로드가 끝났어도 이벤트 재발화를 요구하지 않는다.
        TryInvokeAllInitialized();
    }

    private void OnDestroy()
    {
        foreach (IGameDataDomainManager manager in _dataManagers)
        {
            manager.DataInitialized -= OnDataInitialized;
        }

        SaveDataLoadGuard.Failed -= OnSaveDataLoadFailed;
        if (_offlineRewardManager != null)
        {
            _offlineRewardManager.PresentationBlockChanged -=
                OnOfflineRewardBlockChanged;
        }
    }

    private bool TryBindDataManagers()
    {
        if (!TryAddDataManager(GameDataDomains.Currency, _currencyManager) ||
            !TryAddDataManager(GameDataDomains.SlimeStatus, _slimeManager) ||
            !TryAddDataManager(GameDataDomains.Upgrade, _upgradeManager))
        {
            _dataManagers.Clear();
            _dataDomains.Clear();
            return false;
        }

        if (_offlineRewardManager == null)
        {
            Debug.LogError("게임 매니저의 오프라인 보상 매니저 참조가 비어 있습니다.", this);
            return false;
        }

        return true;
    }

    private bool TryAddDataManager(
        GameDataDomainDefinition domain,
        IGameDataDomainManager manager)
    {
        // 인터페이스로 비교하면 UnityEngine.Object의 파괴된 객체 null 판정을
        // 우회하므로 실제 C# null과 Unity 가짜 null을 모두 확인한다.
        if (manager == null ||
            (manager is UnityEngine.Object managerObject && managerObject == null))
        {
            Debug.LogError(
                $"게임 매니저의 {domain.DisplayName} 데이터 매니저 참조가 비어 있습니다.",
                this);
            return false;
        }

        _dataDomains.Add(domain);
        _dataManagers.Add(manager);
        return true;
    }

    // 불러오기가 실패가 아니라 멈추면 아무도 신고하지 않는다.
    //
    // 각 매니저는 읽기에 실패했을 때만 신고한다. 응답이 아예 오지 않으면 실패도
    // 아니어서 AllDataInitialized가 영영 발화하지 않고, 커튼이 걷히지 않은 채
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
    // 그냥 두면 AllDataInitialized가 영영 발화하지 않아 화면이 멈추고,
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

    private void OnDataInitialized()
    {
        TryInvokeAllInitialized();
    }

    private void TryInvokeAllInitialized()
    {
        if (_isAllInitialized) return;

        foreach (IGameDataDomainManager manager in _dataManagers)
        {
            if (!manager.IsInitialized) return;
        }

        if (!HasConsistentStoredSaveData()) return;

        _isAllInitialized = true;

        // 후반 단계에서 예외가 나면 AllDataInitialized가 끝까지 발화하지 못해 로딩이 멈춘다.
        // _isAllInitialized가 이미 true라 타임아웃 감시는 무력화되므로, 여기서 즉시
        // 로드 실패를 신고해 로그인 화면으로 돌려보낸다. 저장은 이미 읽은 뒤라 종류는
        // InitializationFailed다. Unreadable로 신고하면 멀쩡한 진행도에 초기화를 권하게 된다.
        try
        {
            InitializeTutorialProgress();
            _offlineRewardManager.Grant();
            AllDataInitialized?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"초기화 마무리 단계에서 예외가 발생했습니다: {e}");
            SaveDataLoadGuard.Report(
                ESaveLoadFailure.InitializationFailed,
                $"초기화 마무리 단계 예외 : {e.Message}");
        }
    }

    // 등록된 저장 문서는 함께 만들어지고 함께 지워진다. 튜토리얼을 마칠 때 전부
    // 저장하고, 초기화와 계정 삭제도 한 배치로 지운다.
    //
    // 그래서 일부만 없는 상태는 신규 계정이 아니라 결손이다. 기본값으로 출발하면
    // 남은 도메인은 복원되고 없는 도메인만 초기화된 채 시작하며, 다음 저장이 그
    // 손실을 확정한다. 도메인별 가드는 자기 안만 보므로 여기서 교차로 확인한다.
    private bool HasConsistentStoredSaveData()
    {
        bool expected = _dataManagers[0].HasStoredSaveData;
        bool isConsistent = true;
        for (int i = 1; i < _dataManagers.Count; i++)
        {
            isConsistent &= _dataManagers[i].HasStoredSaveData == expected;
        }

        if (isConsistent) return true;

        var details = new StringBuilder();
        for (int i = 0; i < _dataManagers.Count; i++)
        {
            if (i > 0) details.Append(", ");
            details.Append(_dataDomains[i].DisplayName);
            details.Append(' ');
            details.Append(Describe(_dataManagers[i].HasStoredSaveData));
        }

        SaveDataLoadGuard.Report(
            ESaveLoadFailure.Unreadable,
            $"저장 문서가 일부만 있습니다. : {details}");
        return false;
    }

    private static string Describe(bool hasStoredSaveData)
    {
        return hasStoredSaveData ? "있음" : "없음";
    }

    private void InitializeTutorialProgress()
    {
        bool hasExistingProgress = false;
        foreach (IGameDataDomainManager manager in _dataManagers)
        {
            hasExistingProgress |= manager.HasExistingProgress;
        }

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
            order: (int)_slimeManager.HigherGradeSpawnUnlockGrade,
            completeByDefault: _slimeManager.IsHigherGradeSpawnUnlocked,
            completeStoredIncomplete: false);
        // 뽑기는 이번에 추가된 기능이라 이미 Lv.7을 넘긴 플레이어도 안내를 받아야
        // 한다. 해금 여부로 완료 처리하면 지금 플레이 중인 사람 전원이 건너뛴다.
        TutorialProgress.Register(
            TutorialIds.Gacha,
            order: (int)UnlockGrades.Gacha,
            completeByDefault: false,
            completeStoredIncomplete: false);
        // 도감 마일스톤 안내도 이번에 추가된 기능이라 이미 10종·12종을 넘긴
        // 플레이어에게 한 번은 보여 준다. 순서는 도감 수를 그대로 쓴다. 앞 순서인
        // 해금 등급과 같은 축이고, 도감 10종은 최고 Lv.10을 넘긴 뒤에만 닿는다.
        TutorialProgress.Register(
            TutorialIds.CollectionAutoMerge,
            order: NormalCollectionRules.AutoMergeCount,
            completeByDefault: false,
            completeStoredIncomplete: false);
        TutorialProgress.Register(
            TutorialIds.CollectionTicketCollect,
            order: NormalCollectionRules.TicketBulkCollectCount,
            completeByDefault: false,
            completeStoredIncomplete: false);
        TutorialProgress.Register(
            TutorialIds.CollectionOfflineTicket,
            order: NormalCollectionRules.OfflineTicketRewardCount,
            completeByDefault: false,
            completeStoredIncomplete: false);
        // 상점은 이번에 추가된 안내라 이미 Lv.9를 넘긴 플레이어도 한 번은 본다.
        TutorialProgress.Register(
            TutorialIds.Shop,
            order: (int)UnlockGrades.Shop,
            completeByDefault: false,
            completeStoredIncomplete: false);
        GameplaySaveGate.SetSavingEnabled(
            TutorialProgress.IsCompleted(TutorialIds.Main));

        // 계정 문서에 기록하기 전에 이 기기에서 마친 튜토리얼을 옮긴다.
        // 이미 올라가 있으면 저장하지 않으므로 매 진입마다 불러도 된다.
        TutorialProgress.UploadCompletedToCloud();
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

        var saveTasks = new UniTask[_dataManagers.Count];
        for (int i = 0; i < _dataManagers.Count; i++)
        {
            saveTasks[i] = _dataManagers[i].SaveCurrentAsync();
        }

        await UniTask.WhenAll(saveTasks);

        TutorialProgress.MarkCompleted(TutorialIds.Main);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!_isAllInitialized || GameplaySaveGate.IsResetting) return;

        if (pauseStatus)
        {
            FlushAllDomainsForAppLifecycle();
        }
        else
        {
            _offlineRewardManager.GrantAfterResync().Forget();
        }
    }

    // 종료 경로에서도 세 도메인 flush를 보장한다. Android처럼 종료 시
    // OnApplicationPause(true)가 오지 않을 수 있어, pause와 동일한 가드로
    // 같은 단일 진입점을 호출해 Currency·Upgrade flush 누락을 막는다.
    private void OnApplicationQuit()
    {
        if (!_isAllInitialized || GameplaySaveGate.IsResetting) return;

        FlushAllDomainsForAppLifecycle();
    }

    // pause(true)와 quit이 공유하는 앱 수명 flush 단일 진입점.
    //
    // 등록된 세 도메인(Currency·SlimeStatus·Upgrade)을 _dataManagers 순회로
    // 균일하게 처리한다. 새 도메인도 TryBindDataManagers에 등록되기만 하면
    // 자동으로 포함된다.
    //
    // 오프라인 보상 예외: 받지 않은 보상이 대기 중이면(HasPendingOfflineReward)
    // Currency의 SaveCurrentAsync만 건너뛴다. SaveCurrentAsync가 LastSaveTime을
    // 현재 시각으로 갱신하는데, 보상 대기 중에 갱신하면 다음 실행의 누적 기준
    // 시각이 틀어지기 때문이다. FlushPendingSave는 LastSaveTime과 무관하게
    // 미뤄 둔 쓰기만 밀어내므로 예외와 상관없이 모든 도메인에서 항상 호출한다.
    private void FlushAllDomainsForAppLifecycle()
    {
        for (int i = 0; i < _dataManagers.Count; i++)
        {
            IGameDataDomainManager manager = _dataManagers[i];

            bool skipSave =
                ReferenceEquals(manager, _currencyManager) && HasPendingOfflineReward;

            // 저장은 최신 상태를 큐에 넣고, flush는 간격을 기다리다 프로세스가
            // 멈추면 클라우드에 못 올라가는 미뤄 둔 쓰기를 지금 내보낸다.
            if (!skipSave)
            {
                manager.SaveCurrentAsync().Forget();
            }
            manager.FlushPendingSave();
        }
    }

    private bool HasPendingOfflineReward => _offlineRewardManager.HasPending;

    // 보상 팝업이 화면을 잡는 동안에는 플레이를 멈춘다. 판단은 보상 쪽이 하고
    // 실제로 끄고 켜는 것은 여기서 한다. IsGameplayActive는 초기화·진행도
    // 리셋과도 얽혀 있어 주인이 하나여야 한다.
    private void OnOfflineRewardBlockChanged(bool isBlocked)
    {
        IsGameplayActive = !isBlocked;
    }
}
