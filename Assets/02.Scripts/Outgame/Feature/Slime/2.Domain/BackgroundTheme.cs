public enum EBackgroundTheme
{
    // 저장 데이터에는 정수로 남으므로 기존 값은 바꾸지 않고 새 테마는 뒤에 추가한다.
    Ground = 0,
    Sky = 1,
    Space = 2,
}
public static class BackgroundThemeRules
{
    public static bool IsUnlocked(ESlimeGrade highestGrade)
    {
        return highestGrade >= UnlockGrades.Shop;
    }

    public static bool IsValid(EBackgroundTheme theme)
    {
        return System.Enum.IsDefined(typeof(EBackgroundTheme), theme);
    }

    // 처음부터 제공하는 기본 테마. 하늘과 이후 테마는 상점에서 산 뒤 소유 목록에 들어간다.
    //
    // 저장에 넣지 않는 이유는 기존 계정 때문이다. 소유 목록은 v10에서 생긴
    // 필드라 이전 문서에서는 비어 있는데, 기본 테마까지 목록으로 관리하면
    // 지금 플레이 중인 모두가 시작 배경을 잃는다.
    public static bool IsFree(EBackgroundTheme theme)
    {
        return theme == EBackgroundTheme.Ground;
    }
}
