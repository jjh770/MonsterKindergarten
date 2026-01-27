using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeData", menuName = "Data/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    [SerializeField] private double _baseCost = 10;
    [SerializeField] private double _growthRate = 1.5;
    [SerializeField] private int _maxLevel = 0; // 0이면 무제한

    public int MaxLevel => _maxLevel;

    public double GetCost(int level)
    {
        return _baseCost * Math.Pow(_growthRate, level - 1);
    }

    public bool IsMaxLevel(int level)
    {
        return _maxLevel > 0 && level >= _maxLevel;
    }
}
