public static class PointCalculator
{
    public static double Calculate(double basePoint, ESlimeGrade grade, EClickType clickType)
    {
        double flatBonus = GetFlatBonus(grade, clickType);
        double percentBonus = GetPercentBonus(grade, clickType);

        return (basePoint + flatBonus) * (1 + percentBonus);
    }

    private static double GetFlatBonus(ESlimeGrade grade, EClickType clickType)
    {
        var type = clickType == EClickType.Manual
            ? EUpgradeType.ManualPointPlusAdd
            : EUpgradeType.AutoPointPlusAdd;

        return GetUpgradePoint(type, grade);
    }

    private static double GetPercentBonus(ESlimeGrade grade, EClickType clickType)
    {
        var type = clickType == EClickType.Manual
            ? EUpgradeType.ManualPointPercentAdd
            : EUpgradeType.AutoPointPercentAdd;

        return GetUpgradePoint(type, grade);
    }

    private static double GetUpgradePoint(EUpgradeType type, ESlimeGrade grade)
    {
        if (UpgradeManager_Domain.Instance == null) return 0;

        var upgrade = UpgradeManager_Domain.Instance.Get(type, grade);
        return upgrade?.Point ?? 0;
    }
}
