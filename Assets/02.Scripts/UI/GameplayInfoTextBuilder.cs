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
        UpgradeManager upgradeManager,
        SlimeManager slimeManager,
        SpawnManager spawnManager,
        bool includeTitle = true,
        bool includeCloseHint = false)
    {
        var builder = new StringBuilder();
        if (includeTitle)
        {
            builder.Append("시스템 업그레이드 현황\n");
        }

        if (upgradeManager == null) return builder.ToString();

        List<Upgrade> upgrades = upgradeManager.GetSystemUpgrades();
        upgrades.Sort((left, right) =>
            ((int)left.SpecData.Type).CompareTo((int)right.SpecData.Type));

        bool hasVisibleUpgrade = false;
        foreach (Upgrade upgrade in upgrades)
        {
            EUpgradeType type = upgrade.SpecData.Type;
            if (!SystemUpgradeVisibility.IsShown(type, slimeManager)) continue;

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
                .Append(BuildEffect(type, upgrade, spawnManager))
                .Append("</size>");

            hasVisibleUpgrade = true;
        }

        if (includeCloseHint)
        {
            builder.Append("\n\n<size=80%>눌러서 닫기</size>");
        }

        return builder.ToString();
    }

    // 뽑기 후보가 이 수를 넘으면 한 줄에 두 등급씩 두 열로 보여 준다. 스무 줄을 한 열에 놓으면
    // 상세 텍스트 영역을 넘어 뒤로 버튼 밑으로 흘러내린다.
    private const int GachaProbabilityColumnRows = 10;

    // 두 열의 시작 위치(텍스트 영역 폭 기준). 오른쪽 열의 끝이 영역 밖으로 나가면 줄이 꺾이므로
    // 폭에 여유를 두고 잡는다.
    private const string GachaLeftColumnPosition = "4%";
    private const string GachaRightColumnPosition = "52%";

    public static string BuildNormalGachaProbabilityText(
        ESlimeGrade highestGrade,
        bool includeTitle = true)
    {
        var builder = new StringBuilder();
        if (includeTitle)
        {
            builder.Append("현재 슬라임 뽑기 확률\n");
        }

        builder.Append("현재 최고 등급 Lv.")
            .Append((int)highestGrade)
            .Append(" 기준\n\n");

        List<NormalGachaProbability> probabilities =
            NormalGachaPool.GetProbabilities(highestGrade);
        if (probabilities.Count <= GachaProbabilityColumnRows)
        {
            foreach (NormalGachaProbability probability in probabilities)
            {
                AppendGachaRow(builder, probability);
                builder.Append('\n');
            }

            return builder.ToString();
        }

        // 왼쪽 열은 앞의 열 등급, 오른쪽 열은 나머지다. 줄마다 왼쪽 정렬로 바꾸고 위치 태그로
        // 두 열을 맞춘다. 가운데 정렬 그대로면 오른쪽 열이 없는 줄만 가운데로 쏠린다.
        for (int row = 0; row < GachaProbabilityColumnRows; row++)
        {
            builder.Append("<align=left><pos=")
                .Append(GachaLeftColumnPosition)
                .Append('>');
            AppendGachaRow(builder, probabilities[row]);

            int rightIndex = row + GachaProbabilityColumnRows;
            if (rightIndex < probabilities.Count)
            {
                builder.Append("<pos=")
                    .Append(GachaRightColumnPosition)
                    .Append('>');
                AppendGachaRow(builder, probabilities[rightIndex]);
            }

            builder.Append("</align>\n");
        }

        return builder.ToString();
    }

    private static void AppendGachaRow(
        StringBuilder builder,
        NormalGachaProbability probability)
    {
        builder.Append("<sprite name=\"")
            .Append(((int)probability.Grade).ToString("00"))
            .Append("\"> Lv.")
            .Append((int)probability.Grade)
            .Append("   ")
            .Append((probability.Probability * 100d).ToString("F1"))
            .Append('%');
    }

    private static string BuildEffect(
        EUpgradeType type,
        Upgrade upgrade,
        SpawnManager spawnManager)
    {
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
            case EUpgradeType.AutoMergePairAdd:
                return $"버튼을 누를 때마다 한 번에 " +
                       $"{AutoMergeManager.GetPairCountForLevel(upgrade.Level)}쌍";
            case EUpgradeType.AllSlimePointPercentAdd:
                return $"모든 포인트 획득량 +{upgrade.Point:0.#}%";
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
