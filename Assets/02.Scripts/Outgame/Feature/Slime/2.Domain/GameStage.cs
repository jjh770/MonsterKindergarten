public enum EGameStage
{
    Ground,
    Sky,
}

public enum EBackgroundTheme
{
    Ground,
    Sky,
}

public static class BackgroundThemeRules
{
    public static bool IsUnlocked(ESlimeGrade highestGrade)
    {
        return highestGrade >= UnlockGrades.SkyStage;
    }

    public static bool IsUnlockMerge(ESlimeGrade fromGrade, ESlimeGrade toGrade)
    {
        return fromGrade < UnlockGrades.SkyStage &&
               toGrade >= UnlockGrades.SkyStage;
    }

    public static bool IsUnlockGrade(ESlimeGrade grade)
    {
        return grade == UnlockGrades.SkyStage;
    }

    public static bool IsValid(EBackgroundTheme theme)
    {
        return theme == EBackgroundTheme.Ground ||
               theme == EBackgroundTheme.Sky;
    }
}

// 하늘 스테이지 경계 등급은 UnlockGrades.SkyStage 한곳에서만 정한다.
public static class GameStageRules
{
    public static EGameStage GetStage(ESlimeGrade grade)
    {
        return grade >= UnlockGrades.SkyStage
            ? EGameStage.Sky
            : EGameStage.Ground;
    }

    public static bool IsSkyUnlocked(ESlimeGrade highestGrade)
    {
        return highestGrade >= UnlockGrades.SkyStage;
    }

    // 땅에서 하늘로 처음 넘어가는 합성인지 판정한다.
    public static bool IsSkyEntryMerge(ESlimeGrade fromGrade, ESlimeGrade toGrade)
    {
        return GetStage(fromGrade) == EGameStage.Ground &&
               GetStage(toGrade) == EGameStage.Sky;
    }

    // 하늘 진입 등급인지 판정한다. 해금 팝업 대상 판별에 쓴다.
    public static bool IsSkyEntryGrade(ESlimeGrade grade)
    {
        return grade == UnlockGrades.SkyStage;
    }

    public static bool IsValid(EGameStage stage)
    {
        return stage == EGameStage.Ground || stage == EGameStage.Sky;
    }
}
