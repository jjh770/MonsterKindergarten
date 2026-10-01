// 최고 해금 등급으로 열리는 기능들의 경계를 한곳에 모은다.
// 새 해금 조건을 추가할 때는 호출부에 등급을 직접 쓰지 말고 여기에 상수를 만든 뒤
// SlimeManager의 IsXxxUnlocked 질의를 통해 사용한다.
public static class UnlockGrades
{
    // 기획서 §7.1 - 장식장 + 도감
    public const ESlimeGrade DisplayRoom = ESlimeGrade.Grade3;

    // 기획서 §11.1 - 가챠권 드랍 + 가챠 시스템 (Phase 4에서 사용 예정)
    public const ESlimeGrade Gacha = ESlimeGrade.Grade7;

    // 11레벨 달성 시 배경 테마 선택 해금
    public const ESlimeGrade BackgroundTheme = ESlimeGrade.Grade11;

    // 9레벨 달성 시 왼쪽 서랍(상점)의 손잡이가 나타난다. 상점의 물건(놀이터 오브젝트와 배경)은
    // 장식장과 배경 테마가 열린 뒤에 쓸 수 있고, 가장 싼 물건도 모으는 데 한참 걸려서
    // 포인트가 모일 즈음에 알려 주려고 이 등급에 둔다.
    public const ESlimeGrade Shop = ESlimeGrade.Grade9;

    // 상위 슬라임 등장(Lv.5)은 여기 두지 않는다.
    // SpawnWeightTable의 _spawnCaps에서 파생되는 값이므로 상수로 복제하면
    // 에셋을 조정할 때 조용히 어긋난다. SlimeManager.IsHigherGradeSpawnUnlocked를 쓴다.
}
