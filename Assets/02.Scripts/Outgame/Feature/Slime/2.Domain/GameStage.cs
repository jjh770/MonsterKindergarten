public enum EGameStage
{
    Ground,
    Sky,
}

public static class GameStageRules
{
    // 하늘 스테이지가 시작되는 등급. 경계는 이 상수로만 정한다.
    private const ESlimeGrade SkyEntryGrade = ESlimeGrade.Grade11;

    public static EGameStage GetStage(ESlimeGrade grade)
    {
        return grade >= SkyEntryGrade
            ? EGameStage.Sky
            : EGameStage.Ground;
    }

    public static bool IsSkyUnlocked(ESlimeGrade highestGrade)
    {
        return highestGrade >= SkyEntryGrade;
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
        return grade == SkyEntryGrade;
    }

    public static bool IsValid(EGameStage stage)
    {
        return stage == EGameStage.Ground || stage == EGameStage.Sky;
    }
}
