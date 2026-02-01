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

    // 업그레이드 비용 : 기본 비용 + 증가량^레벨
    public Currency Cost => SpecData.BaseCost + Math.Pow(SpecData.CostMultiplier, Level); // 지수 공식 : 기본 비용 + 증가량 ^ 레벨

    // 레벨 0이면 보너스 없음
    // Linear : 선형 공식 (BasePoint + Level * PointMultiplier)
    // Fixed  : 고정값 공식 (레벨과 무관하게 항상 BasePoint)
    public double Point => Level == 0 ? 0 : CalculatePoint(Level);
    public double NextPoint => IsMaxLevel ? Point : CalculatePoint(Level + 1);
    public bool IsMaxLevel => Level >= SpecData.MaxLevel;

    private double CalculatePoint(int level)
    {
        return SpecData.PointFormula switch
        {
            EPointFormula.Fixed => SpecData.BasePoint,
            _ => SpecData.BasePoint + level * SpecData.PointMultiplier, // Linear
        };
    }

    // 2. 핵심 규칙을 작성한다.
    public Upgrade(UpgradeSpecData specData)
    {
        SpecData = specData;

        if (specData.MaxLevel < 0) throw new System.ArgumentException($"최대 레벨은 0보다 커야합니다. : {specData.MaxLevel}");
        if (specData.BaseCost <= 0) throw new System.ArgumentException($"기본 비용은 0보다 크거나 같아야 합니다. : {specData.BaseCost}");
        if (specData.BasePoint <= 0) throw new System.ArgumentException($"기본 포인트는 0보다 크거나 같아야 합니다. : {specData.BasePoint}");
        if (specData.CostMultiplier <= 0) throw new System.ArgumentException($"비용 증가량은 0보다 크거나 같아야 합니다. : {specData.CostMultiplier}");
        // Fixed 공식은 PointMultiplier를 사용하지 않으므로 검증 생략
        if (specData.PointFormula != EPointFormula.Fixed && specData.PointMultiplier <= 0)
            throw new System.ArgumentException($"포인트 증가량은 0보다 크거나 같아야 합니다. : {specData.PointMultiplier}");
        if (string.IsNullOrEmpty(specData.Name)) throw new System.ArgumentException($"이름은 비어있을 수 없습니다.");
        if (string.IsNullOrEmpty(specData.Description)) throw new System.ArgumentException($"설명은 비어있을 수 없습니다.");
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
