public enum EGameStage
{
    Ground,
    Sky,
}

public static class GameStageRules
{
    public static EGameStage GetStage(ESlimeGrade grade)
    {
        return grade >= ESlimeGrade.Grade11
            ? EGameStage.Sky
            : EGameStage.Ground;
    }

    public static bool IsSkyUnlocked(ESlimeGrade highestGrade)
    {
        return highestGrade >= ESlimeGrade.Grade11;
    }

    public static bool IsValid(EGameStage stage)
    {
        return stage == EGameStage.Ground || stage == EGameStage.Sky;
    }
}
