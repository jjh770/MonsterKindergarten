// 시스템 업그레이드를 지금 화면에 보여 줘도 되는지 정한다.
//
// 캐러셀과 학자 안내가 이 판정을 각자 갖고 있었고, 안내 쪽에 튜토리얼 조건이
// 빠져 캐러셀에 없는 업그레이드가 안내에만 먼저 나왔다. 조건이 한 번 늘 때마다
// 두 곳을 같이 고쳐야 하는 구조라 한곳으로 모은다.
public static class SystemUpgradeVisibility
{
    public static bool IsShown(EUpgradeType type)
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        return type switch
        {
            // 상위 등장 확률은 안내 튜토리얼이 카드를 처음 보여 준다. 그 전에 먼저
            // 보이면 튜토리얼이 가리킬 대상이 이미 알려진 뒤가 된다.
            EUpgradeType.HigherGradeSpawnWeightAdd =>
                slimeManager != null &&
                slimeManager.IsHigherGradeSpawnUnlocked &&
                (TutorialProgress.IsCompleted(TutorialIds.HigherGradeSpawn) ||
                 TutorialManager.IsActive(TutorialIds.HigherGradeSpawn)),
            EUpgradeType.AutoMergePairAdd =>
                slimeManager != null && slimeManager.IsAutoMergeUnlocked,
            _ => true,
        };
    }
}
