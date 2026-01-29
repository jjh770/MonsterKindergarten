using System;

[Serializable]
public class UpgradeSpecData
{
    // 기획 데이터 (변하면 안됨)
    // 1. 기획 테이블의 데이터를 가져오다.
    public EUpgradeType Type;
    public int MaxLevel;
    public double BaseCost;
    public double BasePoint;
    public double CostMultiplier;
    public double PointMultiplier;
    public string Name;
    public string Description;
}
