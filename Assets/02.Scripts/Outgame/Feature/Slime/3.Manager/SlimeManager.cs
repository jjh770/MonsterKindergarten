using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SlimeManager : MonoBehaviour
{
    public static SlimeManager Instance { get; private set; }

    [SerializeField] private SlimeSpecTable _specTable;
    [SerializeField] private SpawnWeightTable _spawnWeightTable;
    [SerializeField, Min(1f)] private float _statsSaveIntervalSeconds = 30f;
    private List<Slime> _slimes = new();

    private IRepository<SlimeStatusSaveData> _statusRepository;
    private SlimeStatus _status;
    private NormalSlimeCollectionStats _collectionStats;
    private bool _statsDirty;
    private float _statsSaveTimer;
    // 도메인 객체를 통째로 내주면 저장을 거치지 않고 상태를 바꿀 수 있다.
    // 밖에서는 개체 목록을 읽기만 하므로 그것만 공개한다.
    public IReadOnlyList<SlimeInstance> ActiveSlimes =>
        _status != null ? _status.ActiveSlimes : Array.Empty<SlimeInstance>();
    // 호출부가 SlimeStatus 내부 구조를 거치지 않도록 최고 등급은 매니저가 직접 노출한다.
    public ESlimeGrade HighestGrade => _status.HighestGrade;
    public EGameStage CurrentStage => _status.CurrentStage;
    public bool SkyIntroCompleted => _status.SkyIntroCompleted;
    public bool IsSkyUnlocked =>
        _status != null &&
        GameStageRules.IsSkyUnlocked(_status.HighestGrade);
    public bool HasExistingProgress =>
        _status != null &&
        (_status.HighestGrade > ESlimeGrade.Grade1 || _status.ActiveSlimes.Count > 0);
    public bool IsDisplayRoomUnlocked =>
        _status != null &&
        _status.HighestGrade >= UnlockGrades.DisplayRoom;
    // 저장을 읽기 전에는 기본값인 켜짐으로 답한다.
    public bool IsAutoSpawnEnabled => _status == null || _status.IsAutoSpawnEnabled;
    public bool IsAutoMergeUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.AutoMergeCount;
    public bool IsAutoMergeEnabled => _status?.IsAutoMergeEnabled ?? false;
    public bool IsGachaUnlocked =>
        _status != null &&
        _status.HighestGrade >= UnlockGrades.Gacha;
    public bool IsHigherGradeSpawnUnlocked =>
        _spawnWeightTable != null &&
        _status != null &&
        _status.HighestGrade >=
        _spawnWeightTable.GetRequiredHighestGradeForTier(0);
    public ESlimeGrade HigherGradeSpawnUnlockGrade =>
        _spawnWeightTable != null
            ? _spawnWeightTable.GetRequiredHighestGradeForTier(0)
            : ESlimeGrade.Count;
    public int NormalCollectionCount => _status?.NormalCollectionCount ?? 0;
    public bool IsTicketAutoCollectUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.AutoTicketCollectCount;
    public bool IsOfflineTicketRewardUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.OfflineTicketRewardCount;
    public bool IsHiddenFeverUnlocked =>
        NormalCollectionCount >= NormalCollectionRules.HiddenFeverCount;
    public bool IsMainEndingSeen => _status?.MainEndingSeen ?? false;
    public float SpecialGachaChance => SpecialGachaFever.GetChance(
        _status?.SpecialGachaMissCount ?? 0);
    // 저장된 문서를 읽었는지. 문서가 없어 기본값으로 출발한 경우와 구분한다.
    public bool HasStoredSaveData { get; private set; }

    // 자연 스폰 상한은 최고 해금 등급으로 결정되므로 슬라임 도메인이 판정한다.
    public bool IsHigherGradeSpawnTierLocked(int currentUpgradeLevel)
    {
        return _spawnWeightTable == null ||
               _status == null ||
               _spawnWeightTable.IsUpgradeTierLocked(
                   _status.HighestGrade,
                   currentUpgradeLevel);
    }

    // 다음 레벨에서 자연 스폰 상한이 올라가는지 판정한다.
    public bool IsSpawnCapRaisedAtNextLevel(int currentUpgradeLevel)
    {
        return _spawnWeightTable != null &&
               _spawnWeightTable.IsSpawnCapRaisedAt(currentUpgradeLevel + 1);
    }

    // 다음 스폰 상한 구간을 열기 위해 필요한 최고 해금 등급.
    public ESlimeGrade GetRequiredHighestGradeForSpawnTier(int currentUpgradeLevel)
    {
        return _spawnWeightTable != null
            ? _spawnWeightTable.GetRequiredHighestGradeForTier(currentUpgradeLevel)
            : ESlimeGrade.Grade1;
    }

    public static event Action OnDataInitialized;
    public static event Action<ESlimeGrade> OnHighestGradeChanged;
    public static event Action<ESlimeGrade> OnNormalCollectionRegistered;
    public static event Action<int> OnNormalCollectionCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (var specData in _specTable.slimeSpecs)
        {
            _slimes.Add(new Slime(specData));
        }
    }

    private void Start()
    {
        _ = InitAsync();
    }

    private void Update()
    {
        if (!_statsDirty ||
            _statusRepository == null ||
            !GameplaySaveGate.IsSavingEnabled)
        {
            return;
        }

        _statsSaveTimer += Time.unscaledDeltaTime;
        if (_statsSaveTimer >= _statsSaveIntervalSeconds)
        {
            Save();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _statsDirty)
        {
            Save();
        }

        if (pauseStatus)
        {
            FlushPendingSave();
        }
    }

    private void OnApplicationQuit()
    {
        if (_statsDirty)
        {
            Save();
        }

        FlushPendingSave();
    }

    private async UniTaskVoid InitAsync()
    {
        await UniTask.Yield();

#if UNITY_ANDROID && !UNITY_EDITOR
        _statusRepository = new HybridRepository<SlimeStatusSaveData>(new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId), new FirebaseSlimeStatusRepository());
#else
        _statusRepository = new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId);
#endif

        SaveLoadResult<SlimeStatusSaveData> loadResult = await _statusRepository.Load();
        if (loadResult.IsFailed)
        {
            // 읽지 못한 세션은 초기화하지 않는다. 세션 처리는 SaveDataLoadGuard가 맡는다.
            SaveDataLoadGuard.Report(
                loadResult.Failure,
                $"SlimeStatus : {loadResult.FailureMessage}");
            return;
        }

        HasStoredSaveData = loadResult.IsLoaded;
        SlimeStatusSaveData saveData = loadResult.IsLoaded
            ? loadResult.Data
            : SlimeStatusSaveData.Default;

        // 문서 단위의 검증과 변환은 SlimeStatusSaveMapper가 맡는다.
        // 복원할 수 없으면 세션을 차단하는 것까지는 기존과 같다.
        if (!SlimeStatusSaveMapper.TryRestore(
                saveData,
                DateTime.UtcNow,
                out SlimeStatus restoredStatus,
                out NormalSlimeCollectionStats restoredStats,
                out bool needsMigrationSave,
                out string failureMessage))
        {
            SaveDataLoadGuard.Report(ESaveLoadFailure.Unreadable, failureMessage);
            return;
        }

        _status = restoredStatus;
        _collectionStats = restoredStats;

        if (needsMigrationSave)
        {
            await SaveMigratedAsync();
        }

        OnDataInitialized?.Invoke();
    }

    public Slime Get(ESlimeGrade grade)
    {
        return _slimes.Find(s => s.SpecData.Grade == grade);
    }

    public string GetName(ESlimeGrade grade)
    {
        Slime slime = Get(grade);

        if (slime == null)
        {
            throw new InvalidOperationException($"{grade}에 해당하는 슬라임 스펙이 없습니다.");
        }

        return slime.SpecData.Name;
    }

    public bool CanMerge(Slime slime1, Slime slime2)
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;

        return slime1.CanMerge(slime2) && slime1.SpecData.Grade < maxGrade;
    }

    public bool TryUpdateHighestLevel(ESlimeGrade newGrade)
    {
        if (newGrade <= _status.HighestGrade) return false;

        _status.UpdateHighestGrade(newGrade);
        OnHighestGradeChanged?.Invoke(newGrade);
        Save();
        return true;
    }

    public void UpdateStageProgress(
        EGameStage currentStage,
        bool skyIntroCompleted)
    {
        if (_status.CurrentStage == currentStage &&
            _status.SkyIntroCompleted == skyIntroCompleted)
        {
            return;
        }

        _status.UpdateStageProgress(currentStage, skyIntroCompleted);
        Save();
    }

    public int GetPendingTicketCount(EGameStage stage)
    {
        return _status?.GetPendingTickets(stage) ?? 0;
    }

    public void SetAutoSpawnEnabled(bool isEnabled)
    {
        if (_status.IsAutoSpawnEnabled == isEnabled) return;

        _status.SetAutoSpawnEnabled(isEnabled);
        Save();
    }

    // 가챠권이 떨어졌을 때 호출한다.
    public void AddPendingTicket(EGameStage stage)
    {
        _status.AddPendingTicket(stage);
        Save();
    }

    // 가챠권을 한 장 주웠을 때 호출한다. 저장에 남은 장수가 없으면 false다.
    public bool TryConsumePendingTicket(EGameStage stage)
    {
        if (!_status.TryConsumePendingTicket(stage)) return false;

        Save();
        return true;
    }

    // 슬라임 스폰 시 호출
    public void AddSlime(SlimeInstance instance)
    {
        _status.AddSlime(instance);
        Save();
    }

    // 이동 검증과 저장을 한 경계에서 처리해 UI가 개체를 직접 변경하지 않게 한다.
    public void MoveSlime(string instanceId, ESlimeLocation location)
    {
        ESlimeGrade? registeredGrade = _status.MoveSlime(instanceId, location);
        if (registeredGrade.HasValue)
        {
            _collectionStats.RecordRegistration(
                registeredGrade.Value,
                DateTime.UtcNow);
            MarkStatsDirty();
        }

        Save();
        if (!registeredGrade.HasValue)
        {
            return;
        }

        OnNormalCollectionRegistered?.Invoke(registeredGrade.Value);
        OnNormalCollectionCountChanged?.Invoke(_status.NormalCollectionCount);
    }

    public bool IsNormalCollectionRegistered(ESlimeGrade grade)
    {
        return _status != null &&
               _status.IsNormalCollectionRegistered(grade);
    }

    public bool TryMarkMainEndingSeen()
    {
        if (_status == null || !_status.TryMarkMainEndingSeen()) return false;

        Save();
        return true;
    }

    // Phase 5의 스페셜 결과 판정 지점에서 호출한다. 해금 전에는 상태를 만들지 않고,
    // 스페셜 성공 시 3%로 초기화하며 일반 결과면 최대 10%까지 0.5%p씩 올린다.
    public void RecordSpecialGachaResult(bool wasSpecial)
    {
        if (_status == null || !_status.RecordSpecialGachaResult(wasSpecial)) return;

        Save();
    }

    public bool SetAutoMergeEnabled(bool isEnabled)
    {
        if (_status == null) return false;
        if (_status.IsAutoMergeEnabled == isEnabled) return true;
        if (!_status.SetAutoMergeEnabled(isEnabled)) return false;

        Save();
        return true;
    }

    public NormalSlimeCollectionStatsSnapshot GetNormalCollectionStats(
        ESlimeGrade grade)
    {
        return _collectionStats != null
            ? _collectionStats.Get(grade)
            : default;
    }

    public void RecordNaturalSpawn(ESlimeGrade grade)
    {
        if (_collectionStats == null) return;

        _collectionStats.RecordNaturalSpawn(grade);
        MarkStatsDirty();
    }

    public void RecordProduction(
        ESlimeGrade grade,
        EClickType clickType,
        double point)
    {
        if (_collectionStats == null) return;

        _collectionStats.RecordProduction(grade, clickType, point);
        MarkStatsDirty();
    }

    // 지금 장식장에 전시 중인지. 도감 등록 여부와 다르다.
    //
    // 등록은 한 번 들어가면 꺼내도 남는 영구 기록이고, 이쪽은 현재 상태다.
    // 도감이 전시 중인 개체에만 표식을 붙이는 데 쓴다.
    public bool IsDisplayedInDisplayRoom(ESlimeGrade grade)
    {
        return _status != null &&
               _status.HasDisplayRoomSlime(grade, isSpecial: false);
    }

    public bool CanMoveToDisplayRoom(ESlimeGrade grade, bool isSpecial)
    {
        return _status != null &&
               !_status.HasDisplayRoomSlime(grade, isSpecial);
    }

    // 한 발동의 합성, 통계, 최고 등급을 모두 반영한 뒤 저장은 한 번만 한다.
    public void MergeSlimesBatch(IReadOnlyList<SlimeMergeRequest> requests)
    {
        if (requests == null || requests.Count == 0) return;

        _status.MergeSlimesBatch(requests);

        ESlimeGrade highestCreated = _status.HighestGrade;
        foreach (SlimeMergeRequest request in requests)
        {
            _collectionStats?.RecordMergeCreated(request.ToGrade);
            if (request.ToGrade > highestCreated)
            {
                highestCreated = request.ToGrade;
            }
        }

        MarkStatsDirty();
        bool highestChanged = highestCreated > _status.HighestGrade;
        if (highestChanged)
        {
            _status.UpdateHighestGrade(highestCreated);
        }

        Save();
        if (highestChanged)
        {
            OnHighestGradeChanged?.Invoke(highestCreated);
        }
    }

    private void Save()
    {
        SaveCurrentAsync().Forget();
    }

    // 앱이 내려갈 때 미뤄 둔 클라우드 쓰기를 지금 내보낸다.
    public void FlushPendingSave()
    {
        _statusRepository?.FlushPendingSave();
    }

    public UniTask SaveCurrentAsync()
    {
        if (!GameplaySaveGate.IsSavingEnabled)
        {
            return UniTask.CompletedTask;
        }

        _statsDirty = false;
        _statsSaveTimer = 0f;
        return _statusRepository.Save(BuildSaveData());
    }

    // 데이터 형식 승격은 튜토리얼 진행 저장 게이트와 무관하게 반영한다.
    private UniTask SaveMigratedAsync()
    {
        return _statusRepository.Save(BuildSaveData());
    }

    private SlimeStatusSaveData BuildSaveData()
    {
        return SlimeStatusSaveMapper.Build(_status, _collectionStats);
    }

    private void MarkStatsDirty()
    {
        _statsDirty = true;
    }
}
