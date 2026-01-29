using System;
using UnityEngine;

public enum UpgradeType
{
    SpawnInterval,
    SpawnMax
}

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private UpgradeData _spawnIntervalData;
    [SerializeField] private UpgradeData _spawnMaxData;

    private int _spawnIntervalLevel = 1;
    private int _spawnMaxLevel = 1;

    public event Action<UpgradeType, int, double> OnUpgraded; // type, newLevel, cost

    public int SpawnIntervalLevel => _spawnIntervalLevel;
    public int SpawnMaxLevel => _spawnMaxLevel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public double GetCost(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.SpawnInterval => _spawnIntervalData.GetCost(_spawnIntervalLevel),
            UpgradeType.SpawnMax => _spawnMaxData.GetCost(_spawnMaxLevel),
            _ => 0
        };
    }

    public double GetNextCost(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.SpawnInterval => _spawnIntervalData.GetCost(_spawnIntervalLevel + 1),
            UpgradeType.SpawnMax => _spawnMaxData.GetCost(_spawnMaxLevel + 1),
            _ => 0
        };
    }

    public bool IsMaxLevel(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.SpawnInterval => _spawnIntervalData.IsMaxLevel(_spawnIntervalLevel) ||
                                         SpawnManager.Instance.SpawnInterval <= SpawnManager.Instance.MinSpawnInterval,
            UpgradeType.SpawnMax => _spawnMaxData.IsMaxLevel(_spawnMaxLevel),
            _ => false
        };
    }

    public bool CanUpgrade(UpgradeType type)
    {
        if (IsMaxLevel(type)) return false;

        double cost = GetCost(type);
        return CurrencyManager.Instance.Point >= cost;
    }

    public bool TryUpgrade(UpgradeType type)
    {
        if (!CanUpgrade(type)) return false;

        double cost = GetCost(type);
        CurrencyManager.Instance.TrySpend(ECurrencyType.Point, cost);

        switch (type)
        {
            case UpgradeType.SpawnInterval:
                SpawnManager.Instance.DecreaseInterval();
                _spawnIntervalLevel++;
                break;
            case UpgradeType.SpawnMax:
                SpawnManager.Instance.IncreaseMaxCount();
                _spawnMaxLevel++;
                break;
        }

        OnUpgraded?.Invoke(type, GetLevel(type), cost);
        return true;
    }

    public int GetLevel(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.SpawnInterval => _spawnIntervalLevel,
            UpgradeType.SpawnMax => _spawnMaxLevel,
            _ => 0
        };
    }
}
