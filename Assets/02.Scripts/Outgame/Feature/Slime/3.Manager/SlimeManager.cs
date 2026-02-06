using System;
using System.Collections.Generic;
using UnityEngine;

// [슬라임 스테이터스 매니저 + 슬라임 매니저 통합] -> 현재 슬라임 현황 저장, 로드 + 슬라임 규칙, 행동 정의
// 슬라임이 가지고 있는 규칙을 통해 다른 매니저들과 새로운 규칙을 정의하거나
// 슬라임이 할 수 있는 다양한 기능들을 정의해놓은 슬라임 매니저

public class SlimeManager : MonoBehaviour
{
    public static SlimeManager Instance { get; private set; }

    [SerializeField] private SlimeSpecTable _specTable;
    private List<Slime> _slimes = new();


    private ISlimeStatusRepository _statusRepository;
    private SlimeStatus _status;
    public SlimeStatus Status => _status;

    private ESlimeGrade _highestGrade;
    public ESlimeGrade HighestGrade => _highestGrade;

    public static event Action OnDataInitialized;
    public static event Action<ESlimeGrade> OnHighestGradeChanged;

    private void Awake()
    {
        Instance = this;

        foreach (var specData in _specTable.slimeSpecs)
        {
            _slimes.Add(new Slime(specData));
        }
        _statusRepository = new MockSlimeStatusRepostiroy();
    }

    public void Start()
    {
        SlimeStatusSaveData saveData = _statusRepository.Load();
        _status = new SlimeStatus(saveData.HighestGrade, saveData.ActiveSlimes);

        OnDataInitialized?.Invoke();
    }

    public Slime Get(ESlimeGrade grade)
    {
        return _slimes.Find(s => s.SpecData.Grade == grade);
    }


    public void SetSlime(Dictionary<ESlimeGrade, int> currentSlimes)
    {
        //_statu
    }

    public bool CanMerge(Slime slime1, Slime slime2)
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;

        return slime1.CanMerge(slime2) && slime1.SpecData.Grade < maxGrade;
    }

    // 합성 후 최고 등급 갱신 시도
    public bool TryUpdateHighestLevel(ESlimeGrade newGrade)
    {
        // 판별: 새 등급이 현재 최고 등급보다 높은가?
        if (newGrade <= _status.HighestGrade) return false;

        _status.UpdateHighestGrade(newGrade);
        OnHighestGradeChanged?.Invoke(newGrade);
        _highestGrade = newGrade;
        return true;
    }

    public bool IsMaxLevelUnlocked()
    {
        ESlimeGrade maxGrade = _slimes[^1].SpecData.Grade;
        return _status.HighestGrade >= maxGrade;
    }
}
