using System;

// 업그레이드 효과의 계산 방식
public enum EPointFormula
{
    Linear,  // 선형 공식 : BasePoint + Level * PointMultiplier
    Fixed,   // 고정값 공식 : 레벨과 무관하게 항상 BasePoint
    // 값이 저장 데이터가 아니라 기획 에셋에 직렬화되므로 기존 값의 번호를 바꾸지 말고 뒤에 추가한다.
    Piecewise, // 구간 공식 : 지정한 (레벨, 값) 점들 사이를 직선으로 잇는다. 레벨 0의 값은 0이다.
}

// 구간 공식의 한 점. Level에서 보너스가 Point가 된다.
[Serializable]
public struct PointBreakpoint
{
    public int Level;
    public double Point;
}

[Serializable]
public class UpgradeSpecData
{
    // 기획 데이터 (변하면 안됨)
    // 1. 기획 테이블의 데이터를 가져오다.
    public EUpgradeType Type;
    public ESlimeGrade SlimeGrade;
    public int MaxLevel;
    public double BaseCost;
    public double BasePoint;
    public double CostMultiplier;
    public int CostTierSize;
    public double CostTierMultiplier = 1;
    public int LateCostStartLevel;
    public double LateBaseCost;
    public double LateCostMultiplier = 1;
    public double PointMultiplier;
    public EPointFormula PointFormula;
    // PointFormula가 Piecewise일 때만 쓴다. 레벨이 오름차순이어야 하고 마지막 점이 MaxLevel이어야 한다.
    public PointBreakpoint[] PointBreakpoints;
    public int SystemIconIndex = -1;
}
