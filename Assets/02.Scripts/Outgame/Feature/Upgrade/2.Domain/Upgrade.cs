// '업그레이드' 라는 게임 콘텐츠의 도메인 클래스
// 도메인이란 핵심 데이터와 규칙을 뜻함.
// 가장 먼저 만들고, 가장 나중에 바뀐다. (게임의 본질이기 때문)
// 핵심 데이터와 규칙을 모두 가지고 있다 -> 응집도가 높다. -> 표현력이 높다. 
using System;

public class Upgrade
{
    // 기획 데이터 (변하면 안됨)
    // 1. 기획 테이블의 데이터를 가져오다.
    // UpgradeSpecData.cs로 이전
    public readonly UpgradeSpecData SpecData;

    // 3. 런타임 데이터 (게임 중간에 바뀌는 데이터)
    public int Level { get; private set; }

    // 업그레이드 비용 : 기본 비용 * 증가량^레벨 * 구간 배율^구간
    //
    // 올림해서 정수로 맞춘다. 획득 포인트는 스펙 값이 모두 정수라 정수로 떨어지는데,
    // 배율이 1.5나 1.4처럼 소수여서 비용에만 소수가 생긴다. 그대로 차감하면 잔액에
    // 소수가 남고 저장 데이터에 그대로 쌓인다.
    // 내림이 아니라 올림인 이유는 플레이어에게 불리해지지 않게 하기 위해서다.
    public Currency Cost
    {
        get
        {
            if (SpecData.LateCostStartLevel > 0 &&
                Level >= SpecData.LateCostStartLevel)
            {
                return Math.Ceiling(
                    SpecData.LateBaseCost *
                    Math.Pow(
                        SpecData.LateCostMultiplier,
                        Level - SpecData.LateCostStartLevel));
            }

            double tierMultiplier = SpecData.CostTierSize > 0 &&
                                    SpecData.CostTierMultiplier > 0
                ? Math.Pow(
                    SpecData.CostTierMultiplier,
                    Level / SpecData.CostTierSize)
                : 1;
            return Math.Ceiling(
                SpecData.BaseCost *
                Math.Pow(SpecData.CostMultiplier, Level) *
                tierMultiplier);
        }
    }

    // 레벨 0이면 보너스 없음
    // Linear : 선형 공식 (BasePoint + Level * PointMultiplier)
    // Fixed  : 고정값 공식 (레벨과 무관하게 항상 BasePoint)
    // Piecewise : 구간 공식 (PointBreakpoints의 점들 사이를 직선으로 이음)
    public double Point => Level == 0 ? 0 : CalculatePoint(Level);
    public double NextPoint => IsMaxLevel ? Point : CalculatePoint(Level + 1);
    public bool IsMaxLevel => Level >= SpecData.MaxLevel;

    private double CalculatePoint(int level)
    {
        return SpecData.PointFormula switch
        {
            EPointFormula.Fixed => SpecData.BasePoint,
            EPointFormula.Piecewise => InterpolatePoint(level),
            _ => SpecData.BasePoint + level * SpecData.PointMultiplier, // Linear
        };
    }

    // 레벨 0의 값은 0이고, 점 사이는 직선이며, 마지막 점 너머는 마지막 값을 유지한다.
    // 이미 올린 레벨이 같거나 더 높은 값을 받도록 점의 값은 줄어들지 않는다.
    private double InterpolatePoint(int level)
    {
        int previousLevel = 0;
        double previousPoint = 0d;
        foreach (PointBreakpoint breakpoint in SpecData.PointBreakpoints)
        {
            if (level <= breakpoint.Level)
            {
                double ratio = (level - previousLevel) /
                               (double)(breakpoint.Level - previousLevel);
                return previousPoint + (breakpoint.Point - previousPoint) * ratio;
            }

            previousLevel = breakpoint.Level;
            previousPoint = breakpoint.Point;
        }

        return previousPoint;
    }

    // 2. 핵심 규칙을 작성한다.
    public Upgrade(UpgradeSpecData specData, int level)
    {
        SpecData = specData;
        Level = level;

        if (specData.MaxLevel < 0) throw new System.ArgumentException($"최대 레벨은 0보다 커야합니다. : {specData.MaxLevel}");
        if (specData.BaseCost <= 0) throw new System.ArgumentException($"기본 비용은 0보다 크거나 같아야 합니다. : {specData.BaseCost}");
        if (specData.BasePoint < 0) throw new System.ArgumentException($"기본 포인트는 0보다 작을 순 없습니다. : {specData.BasePoint}");
        if (specData.CostMultiplier <= 0) throw new System.ArgumentException($"비용 증가량은 0보다 크거나 같아야 합니다. : {specData.CostMultiplier}");
        if (specData.CostTierSize > 0 && specData.CostTierMultiplier <= 0)
            throw new System.ArgumentException($"비용 구간 배율은 0보다 커야 합니다. : {specData.CostTierMultiplier}");
        if (specData.LateCostStartLevel > 0 &&
            (specData.LateCostStartLevel >= specData.MaxLevel ||
             specData.LateBaseCost <= 0 ||
             specData.LateCostMultiplier <= 0))
        {
            throw new System.ArgumentException(
                $"후반 비용 구간 설정이 올바르지 않습니다. : {specData.Type}");
        }
        // Linear만 PointMultiplier를 쓴다. Fixed와 Piecewise는 검증하지 않는다.
        if (specData.PointFormula == EPointFormula.Linear && specData.PointMultiplier <= 0)
            throw new System.ArgumentException($"포인트 증가량은 0보다 크거나 같아야 합니다. : {specData.PointMultiplier}");
        if (specData.PointFormula == EPointFormula.Piecewise)
        {
            ValidateBreakpoints(specData);
        }
    }

    private static void ValidateBreakpoints(UpgradeSpecData specData)
    {
        PointBreakpoint[] points = specData.PointBreakpoints;
        if (points == null || points.Length == 0)
        {
            throw new System.ArgumentException(
                $"구간 공식에는 점이 하나 이상 필요합니다. : {specData.Type}");
        }

        int previousLevel = 0;
        double previousPoint = 0d;
        foreach (PointBreakpoint point in points)
        {
            if (point.Level <= previousLevel || point.Point < previousPoint)
            {
                throw new System.ArgumentException(
                    $"구간 공식의 점은 레벨이 늘고 값이 줄지 않아야 합니다. : {specData.Type}");
            }

            previousLevel = point.Level;
            previousPoint = point.Point;
        }

        if (previousLevel != specData.MaxLevel)
        {
            throw new System.ArgumentException(
                $"구간 공식의 마지막 점은 최대 레벨이어야 합니다. : {specData.Type}");
        }
    }

    public bool CanLevelUp()
    {
        return !IsMaxLevel;
    }

    public bool TryLevelUp()
    {
        if (!CanLevelUp()) return false;

        Level++;
        return true;
    }
}
