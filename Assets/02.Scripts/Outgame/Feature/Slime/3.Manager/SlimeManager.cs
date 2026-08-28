using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SlimeManager : MonoBehaviour
{
    public static SlimeManager Instance { get; private set; }

    [SerializeField] private SlimeSpecTable _specTable;
    [SerializeField] private SpawnWeightTable _spawnWeightTable;
    private List<Slime> _slimes = new();

    private IRepository<SlimeStatusSaveData> _statusRepository;
    private SlimeStatus _status;
    public SlimeStatus Status => _status;
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
    public bool IsHigherGradeSpawnUnlocked =>
        _spawnWeightTable != null &&
        _status != null &&
        _status.HighestGrade >=
        _spawnWeightTable.GetRequiredHighestGradeForTier(0);
    public int NormalCollectionCount => _status?.NormalCollectionCount ?? 0;

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

    private async UniTaskVoid InitAsync()
    {
        await UniTask.Yield();

#if UNITY_ANDROID && !UNITY_EDITOR
        _statusRepository = new HybridRepository<SlimeStatusSaveData>(new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId), new FirebaseSlimeStatusRepository());
#else
        _statusRepository = new PlayerPrefsSlimeStatusRepository(AccountManager.Instance.UserId);
#endif

        SlimeStatusSaveData saveData;
        try
        {
            saveData = await _statusRepository.Load();
        }
        catch (UnsupportedSaveVersionException e)
        {
            Debug.LogError(
                $"[SlimeManager] 현재 앱에서 저장 데이터를 불러올 수 없습니다. " +
                $"앱을 업데이트해 주세요. {e.Message}");
            return;
        }

        var activeSlimes = new List<SlimeInstance>();
        var restoredIds = new HashSet<string>();
        foreach (SlimeInstanceSaveData instanceData in saveData.ActiveSlimes)
        {
            if (instanceData == null)
            {
                Debug.LogWarning("비어 있는 슬라임 저장 항목을 건너뜁니다.");
                continue;
            }

            try
            {
                SlimeInstance instance = instanceData.ToDomain();
                if (!restoredIds.Add(instance.InstanceId))
                {
                    Debug.LogWarning(
                        $"중복된 슬라임 개체를 건너뜁니다: {instance.InstanceId}");
                    continue;
                }

                activeSlimes.Add(instance);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning(
                    $"복원할 수 없는 슬라임 개체를 건너뜁니다: {e.Message}");
            }
        }

        var registeredNormalCollection = new List<ESlimeGrade>(
            GetRegisteredNormalCollection(saveData.NormalCollectionRegistered));
        _status = new SlimeStatus(
            saveData.GetHighestGrade(),
            activeSlimes,
            registeredNormalCollection,
            (EGameStage)saveData.CurrentStage,
            saveData.SkyIntroCompleted);

        if (saveData.WasMigrated ||
            _status.NormalCollectionCount > registeredNormalCollection.Count)
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

    public bool IsMaxLevelUnlocked()
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;
        return _status.HighestGrade >= maxGrade;
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

    public bool CanMoveToDisplayRoom(ESlimeGrade grade, bool isSpecial)
    {
        return _status != null &&
               !_status.HasDisplayRoomSlime(grade, isSpecial);
    }

    // keeper의 ID는 유지하고 removed 개체만 저장 상태에서 제거한다.
    public void MergeSlime(
        string keeperId,
        string removedId,
        ESlimeGrade toGrade)
    {
        _status.MergeSlimes(keeperId, removedId, toGrade);
        Save();
    }

    private void Save()
    {
        SaveCurrentAsync().Forget();
    }

    public UniTask SaveCurrentAsync()
    {
        if (!GameplaySaveGate.IsSavingEnabled)
        {
            return UniTask.CompletedTask;
        }

        return _statusRepository.Save(BuildSaveData());
    }

    // 데이터 형식 승격은 튜토리얼 진행 저장 게이트와 무관하게 반영한다.
    private UniTask SaveMigratedAsync()
    {
        return _statusRepository.Save(BuildSaveData());
    }

    private SlimeStatusSaveData BuildSaveData()
    {
        var saveData = new SlimeStatusSaveData
        {
            SchemaVersion = SaveSchema.SlimeCurrentVersion,
            HighestGrade = (int)_status.HighestGrade,
            ActiveSlimes = new List<SlimeInstanceSaveData>(),
            CurrentStage = (int)_status.CurrentStage,
            SkyIntroCompleted = _status.SkyIntroCompleted,
            NormalCollectionRegistered = BuildNormalCollectionSaveData(),
        };

        foreach (SlimeInstance instance in _status.ActiveSlimes)
        {
            saveData.ActiveSlimes.Add(
                SlimeInstanceSaveData.FromDomain(instance));
        }

        return saveData;
    }

    private static IEnumerable<ESlimeGrade> GetRegisteredNormalCollection(
        IReadOnlyList<bool> registered)
    {
        if (registered == null)
        {
            yield break;
        }

        int count = Math.Min(
            registered.Count,
            SlimeStatusSaveData.NormalCollectionSize);
        for (int i = 0; i < count; i++)
        {
            if (registered[i])
            {
                yield return (ESlimeGrade)(
                    (int)ESlimeGrade.Grade1 + i);
            }
        }
    }

    private List<bool> BuildNormalCollectionSaveData()
    {
        List<bool> registered =
            SlimeStatusSaveData.CreateEmptyNormalCollection();
        for (int i = 0; i < registered.Count; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)(
                (int)ESlimeGrade.Grade1 + i);
            registered[i] = _status.IsNormalCollectionRegistered(grade);
        }

        return registered;
    }
}
