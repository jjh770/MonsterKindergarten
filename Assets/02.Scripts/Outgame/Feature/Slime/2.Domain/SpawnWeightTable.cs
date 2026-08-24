using System;
using UnityEngine;

// 자연 스폰의 등급 상한과 등급별 가중치를 담는 밸런스 테이블.
// 최고 해금 등급이 오를수록 더 높은 등급이 자연 스폰되며,
// 상한과 가중치는 코드가 아닌 에셋에서 조정한다.
[CreateAssetMenu(fileName = "SpawnWeightTable", menuName = "DataTable/Spawn Weight Table")]
public class SpawnWeightTable : ScriptableObject
{
    [Serializable]
    public class SpawnCapEntry
    {
        [Tooltip("최고 해금 등급이 이 값 이상일 때 적용된다.")]
        public ESlimeGrade FromHighestGrade;

        [Tooltip("그때의 자연 스폰 상한 등급.")]
        public ESlimeGrade SpawnCap;
    }

    [Serializable]
    public class SpawnWeightEntry
    {
        public ESlimeGrade Grade;

        [Min(0)]
        public int BaseWeight;
    }

    [SerializeField] private SpawnCapEntry[] _spawnCaps;
    [SerializeField] private SpawnWeightEntry[] _baseWeights;

    // 최고 해금 등급에 해당하는 자연 스폰 상한을 구한다.
    // 배열 순서에 의존하지 않도록 조건을 만족하는 항목 중 가장 높은 상한을 고른다.
    public ESlimeGrade GetSpawnCap(ESlimeGrade highestGrade)
    {
        ESlimeGrade cap = ESlimeGrade.Grade1;

        if (_spawnCaps == null) return cap;

        foreach (SpawnCapEntry entry in _spawnCaps)
        {
            if (entry == null) continue;
            if (entry.FromHighestGrade > highestGrade) continue;
            if (entry.SpawnCap > cap) cap = entry.SpawnCap;
        }

        return cap;
    }

    public int GetBaseWeight(ESlimeGrade grade)
    {
        if (_baseWeights == null) return 0;

        foreach (SpawnWeightEntry entry in _baseWeights)
        {
            if (entry != null && entry.Grade == grade)
            {
                return Mathf.Max(0, entry.BaseWeight);
            }
        }

        return 0;
    }

    // 상한 이하 등급을 가중치로 추첨한다.
    // 테이블이 비었거나 가중치 합이 0이면 Grade1을 반환해 스폰이 멈추지 않게 한다.
    public ESlimeGrade PickSpawnGrade(ESlimeGrade highestGrade)
    {
        int cap = (int)GetSpawnCap(highestGrade);
        int total = 0;

        for (int grade = (int)ESlimeGrade.Grade1; grade <= cap; ++grade)
        {
            total += GetBaseWeight((ESlimeGrade)grade);
        }

        if (total <= 0) return ESlimeGrade.Grade1;

        int roll = UnityEngine.Random.Range(0, total);

        for (int grade = (int)ESlimeGrade.Grade1; grade <= cap; ++grade)
        {
            roll -= GetBaseWeight((ESlimeGrade)grade);
            if (roll < 0) return (ESlimeGrade)grade;
        }

        return ESlimeGrade.Grade1;
    }
}
