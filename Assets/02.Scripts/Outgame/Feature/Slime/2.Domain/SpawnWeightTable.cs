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

        [Min(0)]
        public int MaxUpgradeWeight;
    }

    [Serializable]
    public class UpgradeTierEntry
    {
        [Tooltip("업그레이드 레벨이 이 값 이상일 때 적용된다.")]
        [Min(0)]
        public int FromUpgradeLevel;

        [Tooltip("그때의 자연 스폰 상한 등급.")]
        public ESlimeGrade SpawnCap;

        [Tooltip("이 구간에서 레벨 하나당 오르는 가중치 진행도. 전 구간 합이 1이 되게 채운다.")]
        [Min(0f)]
        public float ProgressPerLevel;
    }

    [SerializeField] private SpawnCapEntry[] _spawnCaps;
    [SerializeField] private UpgradeTierEntry[] _upgradeTiers;
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

    public ESlimeGrade GetSpawnCap(
        ESlimeGrade highestGrade,
        int upgradeLevel)
    {
        ESlimeGrade progressionCap = GetSpawnCap(highestGrade);
        ESlimeGrade upgradeCap = GetUpgradeSpawnCap(upgradeLevel);
        return progressionCap < upgradeCap ? progressionCap : upgradeCap;
    }

    // 다음 레벨이 요구하는 상한에 진행도가 못 미치면 그 구간은 아직 잠긴 것으로 본다.
    public bool IsUpgradeTierLocked(
        ESlimeGrade highestGrade,
        int currentUpgradeLevel)
    {
        return GetSpawnCap(highestGrade) <
               GetUpgradeSpawnCap(currentUpgradeLevel + 1);
    }

    // 다음 레벨 구간을 열기 위해 필요한 최고 해금 등급.
    public ESlimeGrade GetRequiredHighestGradeForTier(int currentUpgradeLevel)
    {
        return GetRequiredHighestGrade(
            GetUpgradeSpawnCap(currentUpgradeLevel + 1));
    }

    public ESlimeGrade GetRequiredHighestGrade(ESlimeGrade requiredSpawnCap)
    {
        ESlimeGrade requiredHighestGrade = ESlimeGrade.Grade1;
        bool found = false;

        if (_spawnCaps == null) return requiredHighestGrade;

        foreach (SpawnCapEntry entry in _spawnCaps)
        {
            if (entry == null || entry.SpawnCap < requiredSpawnCap) continue;
            if (found && entry.FromHighestGrade >= requiredHighestGrade) continue;

            requiredHighestGrade = entry.FromHighestGrade;
            found = true;
        }

        return requiredHighestGrade;
    }

    // 업그레이드 레벨이 도달한 구간의 자연 스폰 상한을 구한다.
    // _spawnCaps와 같이 배열 순서에 의존하지 않는다.
    private ESlimeGrade GetUpgradeSpawnCap(int upgradeLevel)
    {
        ESlimeGrade cap = ESlimeGrade.Grade1;

        if (_upgradeTiers == null) return cap;

        foreach (UpgradeTierEntry tier in _upgradeTiers)
        {
            if (tier == null) continue;
            if (tier.FromUpgradeLevel > upgradeLevel) continue;
            if (tier.SpawnCap > cap) cap = tier.SpawnCap;
        }

        return cap;
    }

    // 해당 레벨에서 자연 스폰 상한이 한 단계 올라가는지 판정한다.
    public bool IsSpawnCapRaisedAt(int upgradeLevel)
    {
        return upgradeLevel > 0 &&
               GetUpgradeSpawnCap(upgradeLevel) >
               GetUpgradeSpawnCap(upgradeLevel - 1);
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

    public double GetEffectiveWeight(ESlimeGrade grade, int upgradeLevel)
    {
        if (_baseWeights == null) return 0;

        foreach (SpawnWeightEntry entry in _baseWeights)
        {
            if (entry == null || entry.Grade != grade) continue;

            int baseWeight = Mathf.Max(0, entry.BaseWeight);
            int maxWeight = entry.MaxUpgradeWeight > 0
                ? entry.MaxUpgradeWeight
                : baseWeight;
            double progress = GetUpgradeProgress(upgradeLevel);
            return baseWeight + (maxWeight - baseWeight) * progress;
        }

        return 0;
    }

    // 상한 이하 등급을 가중치로 추첨한다.
    // 테이블이 비었거나 가중치 합이 0이면 Grade1을 반환해 스폰이 멈추지 않게 한다.
    public ESlimeGrade PickSpawnGrade(
        ESlimeGrade highestGrade,
        int upgradeLevel = 0)
    {
        int cap = (int)GetSpawnCap(highestGrade, upgradeLevel);
        double total = 0;

        for (int grade = (int)ESlimeGrade.Grade1; grade <= cap; ++grade)
        {
            total += GetEffectiveWeight((ESlimeGrade)grade, upgradeLevel);
        }

        if (total <= 0) return ESlimeGrade.Grade1;

        double roll = UnityEngine.Random.value * total;

        for (int grade = (int)ESlimeGrade.Grade1; grade <= cap; ++grade)
        {
            roll -= GetEffectiveWeight((ESlimeGrade)grade, upgradeLevel);
            if (roll < 0) return (ESlimeGrade)grade;
        }

        return ESlimeGrade.Grade1;
    }

    // 구간별 레벨 수 x 레벨당 진행도를 합산한다.
    // 레벨 단위로 순회하지 않으므로 저장 데이터의 레벨이 최대치를 넘어도 안전하다.
    private double GetUpgradeProgress(int upgradeLevel)
    {
        if (_upgradeTiers == null || upgradeLevel <= 0) return 0;

        double progress = 0;

        foreach (UpgradeTierEntry tier in _upgradeTiers)
        {
            if (tier == null) continue;

            int tierStart = Mathf.Max(1, tier.FromUpgradeLevel);
            int tierEnd = upgradeLevel;

            // 뒤에서 시작하는 구간이 있으면 그 직전 레벨까지만 이 구간으로 센다.
            foreach (UpgradeTierEntry next in _upgradeTiers)
            {
                if (next == null ||
                    next.FromUpgradeLevel <= tier.FromUpgradeLevel)
                {
                    continue;
                }

                tierEnd = Mathf.Min(tierEnd, next.FromUpgradeLevel - 1);
            }

            int levelCount = tierEnd - tierStart + 1;
            if (levelCount > 0)
            {
                progress += levelCount * (double)tier.ProgressPerLevel;
            }
        }

        return Math.Min(1, Math.Max(0, progress));
    }
}
