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
        CreateItems();
        Refresh();

        CurrencyManager.Instance.OnDataChanged += RefreshCurrency;
        UpgradeManager.OnDataChanged += Refresh;
        if (SlimeSpawner.Instance != null)
            SlimeSpawner.Instance.OnHighestLevelChanged += OnHighestLevelChanged;
    }

    private void OnHighestLevelChanged(int level)
    {
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

        int highestLevel = SlimeSpawner.Instance != null ? SlimeSpawner.Instance.HighestLevel : 1;

        for (int i = 0; i < _items.Count; ++i)
        {
            bool isUnlocked = (int)upgrades[i].SpecData.SlimeGrade <= highestLevel
                              || upgrades[i].SpecData.SlimeGrade == ESlimeGrade.None;
            _items[i].Refresh(upgrades[i], isUnlocked);
        }
    }
}
