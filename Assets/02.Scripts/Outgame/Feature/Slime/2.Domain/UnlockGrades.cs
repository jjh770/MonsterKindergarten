// 최고 해금 등급으로 열리는 기능들의 경계를 한곳에 모은다.
// 새 해금 조건을 추가할 때는 호출부에 등급을 직접 쓰지 말고 여기에 상수를 만든 뒤
// SlimeManager의 IsXxxUnlocked 질의를 통해 사용한다.
public static class UnlockGrades
{
    // 기획서 §7.1 - 장식장 + 도감
    public const ESlimeGrade DisplayRoom = ESlimeGrade.Grade3;

    // 기획서 §11.1 - 가챠권 드랍 + 가챠 시스템 (Phase 4에서 사용 예정)
    public const ESlimeGrade Gacha = ESlimeGrade.Grade7;

    // 기획서 §6 - 하늘 스테이지
    public const ESlimeGrade SkyStage = ESlimeGrade.Grade11;

    // 상위 슬라임 등장(Lv.5)은 여기 두지 않는다.
    // SpawnWeightTable의 _spawnCaps에서 파생되는 값이므로 상수로 복제하면
    // 에셋을 조정할 때 조용히 어긋난다. SlimeManager.IsHigherGradeSpawnUnlocked를 쓴다.
}
