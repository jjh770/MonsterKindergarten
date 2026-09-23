public enum EBackgroundTheme
{
    // 저장 데이터에는 정수로 남으므로 기존 값은 바꾸지 않고 새 테마는 뒤에 추가한다.
    Ground = 0,
    Sky = 1,
}
public static class BackgroundThemeRules
{
    public static bool IsUnlocked(ESlimeGrade highestGrade)
    {
        return highestGrade >= UnlockGrades.BackgroundTheme;
    }

    public static bool IsUnlockMerge(ESlimeGrade fromGrade, ESlimeGrade toGrade)
    {
        return fromGrade < UnlockGrades.BackgroundTheme &&
               toGrade >= UnlockGrades.BackgroundTheme;
    }

    public static bool IsUnlockGrade(ESlimeGrade grade)
    {
        return grade == UnlockGrades.BackgroundTheme;
    }

    public static bool IsValid(EBackgroundTheme theme)
    {
        return System.Enum.IsDefined(typeof(EBackgroundTheme), theme);
    }
}
