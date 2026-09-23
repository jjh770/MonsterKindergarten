public enum EBackgroundTheme
{
    Ground,
    Sky,
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
        return theme == EBackgroundTheme.Ground ||
               theme == EBackgroundTheme.Sky;
    }
}
