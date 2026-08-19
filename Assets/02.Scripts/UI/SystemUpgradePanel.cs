using System.Collections.Generic;
using UnityEngine;
using Utility;

public sealed class SystemUpgradePanel : MonoBehaviour
{
    [SerializeField] private SystemUpgradeItemUI[] _items;

    private readonly Dictionary<EUpgradeType, Upgrade> _upgrades = new();
    private ESlimeGrade _highestGrade;
    private bool _isInitialized;

    private void Start()
    {
        foreach (SystemUpgradeItemUI item in _items)
        {
            if (item != null)
            {
                item.UpgradeRequested += OnUpgradeRequested;
            }
        }

        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        UpgradeManager.OnDataChanged += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged += OnSpawnIntervalChanged;
            SpawnManager.Instance.OnSpawnMaxChanged += OnSpawnMaxChanged;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        foreach (SystemUpgradeItemUI item in _items)
        {
            if (item != null)
            {
                item.UpgradeRequested -= OnUpgradeRequested;
            }
        }

        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        UpgradeManager.OnDataChanged -= Refresh;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged -= OnSpawnIntervalChanged;
            SpawnManager.Instance.OnSpawnMaxChanged -= OnSpawnMaxChanged;
        }
    }

    private void OnAllDataInitialized()
    {
        if (UpgradeManager.Instance == null || SlimeManager.Instance == null) return;

        _isInitialized = true;
        _highestGrade = SlimeManager.Instance.Status.HighestGrade;
        CacheSystemUpgrades();
        Refresh();
    }

    private void CacheSystemUpgrades()
    {
        _upgrades.Clear();

        foreach (Upgrade upgrade in UpgradeManager.Instance.GetSystemUpgrades())
        {
            _upgrades[upgrade.SpecData.Type] = upgrade;
        }
    }

    private void OnUpgradeRequested(EUpgradeType type)
    {
        if (!_upgrades.TryGetValue(type, out Upgrade upgrade)) return;

        if (!UpgradeManager.Instance.TryLevelUp(type, ESlimeGrade.None) &&
            !upgrade.IsMaxLevel)
        {
            NotEnoughPointPopupUI.Instance?.Show();
        }
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        _highestGrade = grade;
        Refresh();
    }

    private void OnSpawnIntervalChanged(float interval, float minInterval)
    {
        Refresh();
    }

    private void OnSpawnMaxChanged(int maxCount)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (!_isInitialized || SpawnManager.Instance == null) return;

        foreach (SystemUpgradeItemUI item in _items)
        {
            if (item == null || !_upgrades.TryGetValue(item.UpgradeType, out Upgrade upgrade))
            {
                continue;
            }

            bool isMax = IsMax(upgrade);
            string valueText = BuildValueText(upgrade, isMax);
            string costText = BuildCostText(upgrade, isMax);
            item.Refresh(valueText, costText, isMax);
        }
    }

    private static bool IsMax(Upgrade upgrade)
    {
        if (upgrade.IsMaxLevel) return true;

        return upgrade.SpecData.Type == EUpgradeType.SpawnTimeSub &&
               SpawnManager.Instance.SpawnInterval <= SpawnManager.Instance.MinSpawnInterval;
    }

    private static string BuildValueText(Upgrade upgrade, bool isMax)
    {
        string icon = upgrade.SpecData.SystemIconIndex >= 0
            ? $"<sprite={upgrade.SpecData.SystemIconIndex}>"
            : string.Empty;

        if (isMax)
        {
            return $"{icon}MAX";
        }

        double modifierIncrease = upgrade.NextPoint - upgrade.Point;

        return upgrade.SpecData.Type switch
        {
            EUpgradeType.SpawnTimeSub =>
                $"{icon}{SpawnManager.Instance.SpawnInterval:F1} -> " +
                $"{Mathf.Max(SpawnManager.Instance.MinSpawnInterval, SpawnManager.Instance.SpawnInterval - (float)modifierIncrease):F1}",
            EUpgradeType.MaxCountAdd =>
                $"{icon}{SpawnManager.Instance.MaxActiveCount} -> " +
                $"{SpawnManager.Instance.MaxActiveCount + Mathf.RoundToInt((float)modifierIncrease)}",
            _ => $"{icon}{upgrade.Point:N0} -> {upgrade.NextPoint:N0}"
        };
    }

    private string BuildCostText(Upgrade upgrade, bool isMax)
    {
        if (isMax)
        {
            return $"<sprite={(int)_highestGrade}>MAX";
        }

        double cost = (double)upgrade.Cost;
        return $"<sprite={(int)_highestGrade}>{cost.ToFormattedString()}";
    }
}
