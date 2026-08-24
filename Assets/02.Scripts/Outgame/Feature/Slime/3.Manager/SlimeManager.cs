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

        SlimeStatusSaveData saveData = await _statusRepository.Load();
        _status = new SlimeStatus(
            saveData.GetHighestGrade(),
            saveData.GetActiveSlimesDict(),
            (EGameStage)saveData.CurrentStage,
            saveData.SkyIntroCompleted);

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
    public void AddSlime(ESlimeGrade grade)
    {
        _status.AddSlime(grade);
        Save();
    }

    // 머지 시 호출 (두 슬라임 제거 + 새 슬라임 추가)
    // 디스폰 시의 카운트 차감도 이 메서드가 함께 처리한다.
    // 단독 디스폰 경로를 추가할 경우 SlimeSpawner.Despawn과의 회계 분담부터 재설계할 것.
    public void MergeSlime(ESlimeGrade fromGrade, ESlimeGrade toGrade)
    {
        _status.RemoveSlime(fromGrade); // keeper 기존 등급 제거
        _status.RemoveSlime(fromGrade); // removed 슬라임 제거
        _status.AddSlime(toGrade);      // 새 등급 추가
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

        var saveData = new SlimeStatusSaveData
        {
            SchemaVersion = SaveSchema.SlimeCurrentVersion,
            HighestGrade = (int)_status.HighestGrade,
            ActiveSlimes = new List<SlimeEntry>(),
            CurrentStage = (int)_status.CurrentStage,
            SkyIntroCompleted = _status.SkyIntroCompleted,
        };

        foreach (var pair in _status.ActiveSlimes)
        {
            saveData.ActiveSlimes.Add(new SlimeEntry(pair.Key, pair.Value));
        }

        return _statusRepository.Save(saveData);
    }
}
