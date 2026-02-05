using System.Collections.Generic;
using UnityEngine;

public class UpgradePanel : MonoBehaviour
{
    [SerializeField] private UpgradeItem _itemPrefab;
    [SerializeField] private Transform _content;
    [SerializeField] private MonsterLevelData _monsterLevelData;

    private List<UpgradeItem> _items = new();

    private void Start()
    {
        CurrencyManager.Instance.OnDataChanged += RefreshCurrency;
        CurrencyManager.Instance.OnDataInitialized += Refresh;
        UpgradeManager.OnDataChanged += Refresh;
        UpgradeManager.OnDataInitialized += OnDataInitialized;
        if (SlimeSpawner.Instance != null)
            SlimeSpawner.OnHighestLevelChanged += OnHighestLevelChanged;
    }
    private void OnDestroy()
    {
        CurrencyManager.Instance.OnDataChanged -= RefreshCurrency;
        CurrencyManager.Instance.OnDataInitialized -= Refresh;
        UpgradeManager.OnDataChanged -= Refresh;
        UpgradeManager.OnDataInitialized -= OnDataInitialized;
        if (SlimeSpawner.Instance != null)
            SlimeSpawner.OnHighestLevelChanged -= OnHighestLevelChanged;
    }

    private void OnHighestLevelChanged(ESlimeGrade grade)
    {
        Refresh();
    }

    private void OnDataInitialized()
    {
        CreateItems();
        Refresh();
    }

    private void CreateItems()
    {
        var upgrades = UpgradeManager.Instance.GetSlimeUpgrades();

        foreach (var upgrade in upgrades)
        {
            var item = Instantiate(_itemPrefab, _content);
            int slimeLevel = (int)upgrade.SpecData.SlimeGrade;
            item.SetSprite(_monsterLevelData.GetSprite(slimeLevel));
            item.Refresh(upgrade);
            _items.Add(item);
        }
    }

    private void RefreshCurrency(ECurrencyType type, Currency currency)
    {
        Refresh();
    }

    private void Refresh()
    {
        var upgrades = UpgradeManager.Instance.GetSlimeUpgrades();

        ESlimeGrade highestLevel = SlimeSpawner.Instance != null ? SlimeSpawner.Instance.HighestGrade : ESlimeGrade.Grade1;

        for (int i = 0; i < _items.Count; ++i)
        {
            bool isUnlocked = upgrades[i].SpecData.SlimeGrade <= highestLevel || upgrades[i].SpecData.SlimeGrade == ESlimeGrade.None;
            _items[i].Refresh(upgrades[i], isUnlocked);
        }
    }
}
