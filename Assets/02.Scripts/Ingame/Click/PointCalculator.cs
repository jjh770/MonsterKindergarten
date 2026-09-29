using System;

public static class PointCalculator
{
    public static double Calculate(
        double basePoint,
        ESlimeGrade grade,
        EClickType clickType,
        bool isSpecial = false)
    {
        double flatBonus = GetFlatBonus(grade, clickType);
        double percentBonus = GetPercentBonus(grade, clickType);

        // 포인트는 정수 단위로만 오간다. 배율을 곱하면 소수가 남고, 그대로 두면
        // 지갑에 소수점이 쌓여 저장값이 369734123.20000654처럼 남는다.
        //
        // 버리지 않고 반올림하는 이유는 버림이 매번 1점 미만을 깎아, 한 번에 얻는
        // 값이 작은 낮은 등급일수록 배율이 실제보다 낮게 동작하기 때문이다.
        return Math.Round(
            (basePoint + flatBonus) * (1 + percentBonus) * GetSpecialMultiplier(isSpecial),
            MidpointRounding.AwayFromZero);
    }

    // 특별한 슬라임은 같은 등급의 일반보다 더 번다. 반올림은 배율을 곱한 뒤에 한 번만 한다.
    private static double GetSpecialMultiplier(bool isSpecial)
    {
        return isSpecial ? SpecialSlimeRules.PointMultiplier : 1d;
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

        double gradeBonus = GetUpgradePoint(type, grade);
        double allSlimeBonus = GetUpgradePoint(
            EUpgradeType.AllSlimePointPercentAdd,
            ESlimeGrade.None) * 0.01d;

        return gradeBonus + allSlimeBonus;
    }

    private static double GetUpgradePoint(EUpgradeType type, ESlimeGrade grade)
    {
        if (UpgradeManager.Instance == null) return 0;

        var upgrade = UpgradeManager.Instance.Get(type, grade);
        return upgrade?.Point ?? 0;
    }
}
