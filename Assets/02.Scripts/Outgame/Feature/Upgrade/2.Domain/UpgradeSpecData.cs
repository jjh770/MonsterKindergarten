using System;

// 업그레이드 효과의 계산 방식
public enum EPointFormula
{
    Linear,  // 선형 공식 : BasePoint + Level * PointMultiplier
    Fixed,   // 고정값 공식 : 레벨과 무관하게 항상 BasePoint
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
    public double PointMultiplier;
    public EPointFormula PointFormula;
    public string Name;
    public string Description;
}
