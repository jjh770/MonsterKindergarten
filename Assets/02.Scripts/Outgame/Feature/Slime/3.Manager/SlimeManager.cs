using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SlimeManager : MonoBehaviour
{
    public static SlimeManager Instance { get; private set; }

    [SerializeField] private SlimeSpecTable _specTable;
    private List<Slime> _slimes = new();

    private ISlimeStatusRepository _statusRepository;
    private SlimeStatus _status;
    public SlimeStatus Status => _status;

    public static event Action OnDataInitialized;
    public static event Action<ESlimeGrade> OnHighestGradeChanged;

    private void Awake()
    {
        Instance = this;

        foreach (var specData in _specTable.slimeSpecs)
        {
            _slimes.Add(new Slime(specData));
        }
        _statusRepository = new FirebaseSlimeStatusRepository();
    }

    private void Start()
    {
        _ = InitAsync();
    }

    private async UniTaskVoid InitAsync()
    {
        SlimeStatusSaveData saveData = await _statusRepository.Load();
        _status = new SlimeStatus(saveData.GetHighestGrade(), saveData.GetActiveSlimesDict());

        OnDataInitialized?.Invoke();
    }

    public Slime Get(ESlimeGrade grade)
    {
        return _slimes.Find(s => s.SpecData.Grade == grade);
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

    // 슬라임 스폰 시 호출
    public void AddSlime(ESlimeGrade grade)
    {
        _status.AddSlime(grade);
        Save();
    }

    // 슬라임 디스폰 시 호출
    public void RemoveSlime(ESlimeGrade grade)
    {
        _status.RemoveSlime(grade);
        Save();
    }

    // 머지 시 호출 (두 슬라임 제거 + 새 슬라임 추가)
    public void MergeSlime(ESlimeGrade fromGrade, ESlimeGrade toGrade)
    {
        _status.RemoveSlime(fromGrade); // keeper 기존 등급 제거
        _status.RemoveSlime(fromGrade); // removed 슬라임 제거
        _status.AddSlime(toGrade);      // 새 등급 추가
        Save();
    }

    private void Save()
    {
        var saveData = new SlimeStatusSaveData
        {
            HighestGrade = (int)_status.HighestGrade,
            ActiveSlimes = new List<SlimeEntry>()
        };

        foreach (var pair in _status.ActiveSlimes)
        {
            saveData.ActiveSlimes.Add(new SlimeEntry(pair.Key, pair.Value));
        }

        _statusRepository.Save(saveData).Forget();
    }
}
