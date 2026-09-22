// 시스템 업그레이드의 화면 표시 이름.
//
// 캐러셀 카드와 학자 안내의 업그레이드 현황이 같은 이름을 쓴다. 카드 클래스에 두면
// 안내 텍스트가 이름 하나 때문에 카드 컴포넌트를 참조하게 되므로 따로 둔다.
// 화면에 보이는 문구라 도메인이 아니라 UI에 둔다.
public static class SystemUpgradeNames
{
    public static string Get(EUpgradeType upgradeType)
    {
        return upgradeType switch
        {
            EUpgradeType.SpawnTimeSub => "스폰 시간 단축",
            EUpgradeType.MaxCountAdd => "최대 슬라임 수",
            EUpgradeType.HigherGradeSpawnWeightAdd => "상위 슬라임 등장 확률",
            EUpgradeType.AutoMergeTimeSub => "자동 합성 시간 단축",
            _ => upgradeType.ToString(),
        };
    }
}
