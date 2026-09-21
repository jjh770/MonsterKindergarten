using System.Collections.Generic;
using System.Text;

// 학자 안내와 기존 보조 팝업이 같은 계산 결과를 보여 주도록 텍스트 조립을 한곳에 둔다.
public static class GameplayInfoTextBuilder
{
    public static string BuildSpawnProbabilityText(
        IReadOnlyList<SpawnProbability> probabilities,
        int higherGradeSpawnLevel,
        bool includeTitle = true,
        bool includeCloseHint = false)
    {
        var builder = new StringBuilder();
        if (includeTitle)
        {
            builder.Append("현재 자연 등장 확률\n");
        }

        if (higherGradeSpawnLevel >= 0)
        {
            builder.Append("상위 슬라임 등장 확률 Lv.")
                .Append(higherGradeSpawnLevel)
                .Append("\n\n");
        }

        if (probabilities != null)
        {
            foreach (SpawnProbability probability in probabilities)
            {
                builder.Append("<sprite name=\"")
                    .Append(((int)probability.Grade).ToString("00"))
                    .Append("\"> Lv.")
                    .Append((int)probability.Grade)
                    .Append("   ")
                    .Append((probability.Probability * 100f).ToString("F1"))
                    .Append("%\n");
            }
        }

        if (includeCloseHint)
        {
            builder.Append("\n눌러서 닫기");
        }

        return builder.ToString();
    }

    public static string BuildSystemUpgradeText(
        bool includeTitle = true,
        bool includeCloseHint = false)
    {
        var builder = new StringBuilder();
        if (includeTitle)
        {
            builder.Append("시스템 업그레이드 현황\n");
        }

        if (UpgradeManager.Instance == null) return builder.ToString();

        List<Upgrade> upgrades = UpgradeManager.Instance.GetSystemUpgrades();
        upgrades.Sort((left, right) =>
            ((int)left.SpecData.Type).CompareTo((int)right.SpecData.Type));

        bool hasVisibleUpgrade = false;
        foreach (Upgrade upgrade in upgrades)
        {
            EUpgradeType type = upgrade.SpecData.Type;
            if (!SystemUpgradeVisibility.IsShown(type)) continue;

            if (hasVisibleUpgrade)
            {
                builder.Append("\n\n");
            }

            builder.Append(SystemUpgradeNames.Get(type))
                .Append("  ")
                .Append(upgrade.IsMaxLevel
                    ? "MAX"
                    : $"Lv.{upgrade.Level}/{upgrade.SpecData.MaxLevel}")
                .Append('\n')
                .Append("<size=92%>")
                .Append(BuildEffect(type, upgrade))
                .Append("</size>");

            hasVisibleUpgrade = true;
        }

        if (includeCloseHint)
        {
            builder.Append("\n\n<size=80%>눌러서 닫기</size>");
        }

        return builder.ToString();
    }

    private static string BuildEffect(EUpgradeType type, Upgrade upgrade)
    {
        SpawnManager spawnManager = SpawnManager.Instance;
        switch (type)
        {
            case EUpgradeType.SpawnTimeSub:
                return spawnManager == null
                    ? string.Empty
                    : $"생성 간격 {spawnManager.SpawnInterval:F1}초 (최소 {spawnManager.MinSpawnInterval:F1}초)";
            case EUpgradeType.MaxCountAdd:
                return spawnManager == null
                    ? string.Empty
                    : $"최대 {spawnManager.MaxActiveCount}마리";
            case EUpgradeType.HigherGradeSpawnWeightAdd:
                return spawnManager == null
                    ? string.Empty
                    : $"자연 등장 최고 Lv.{GetHighestSpawnGrade(spawnManager)}";
            case EUpgradeType.AutoMergeTimeSub:
                return AutoMergeManager.Instance == null
                    ? string.Empty
                    : $"주기 {AutoMergeManager.Instance.Interval:F1}초, 한 번에 " +
                      $"{AutoMergeManager.GetPairCountForLevel(upgrade.Level)}쌍";
            default:
                return string.Empty;
        }
    }

    private static int GetHighestSpawnGrade(SpawnManager spawnManager)
    {
        int highest = 0;
        foreach (SpawnProbability probability in spawnManager.GetCurrentSpawnProbabilities())
        {
            if (probability.Probability > 0d && (int)probability.Grade > highest)
            {
                highest = (int)probability.Grade;
            }
        }

        return highest;
    }
}
