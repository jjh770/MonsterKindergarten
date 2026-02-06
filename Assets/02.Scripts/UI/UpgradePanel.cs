using System.Collections.Generic;
using UnityEngine;

public class UpgradePanel : MonoBehaviour
{
    [SerializeField] private UpgradeItem _itemPrefab;
    [SerializeField] private Transform _content;

    private List<UpgradeItem> _items = new();

    private void Start()
    {
        CurrencyManager.Instance.OnDataChanged += RefreshCurrency;
        CurrencyManager.Instance.OnDataInitialized += Refresh;
        UpgradeManager.OnDataChanged += Refresh;
        UpgradeManager.OnDataInitialized += OnDataInitialized;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;
    }

    private void OnDestroy()
    {
        CurrencyManager.Instance.OnDataChanged -= RefreshCurrency;
        CurrencyManager.Instance.OnDataInitialized -= Refresh;
        UpgradeManager.OnDataChanged -= Refresh;
        UpgradeManager.OnDataInitialized -= OnDataInitialized;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
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
            item.SetSprite(UpgradeManager.Instance.GetSprite(upgrade.SpecData.SlimeGrade));
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

        ESlimeGrade highestGrade = SlimeManager.Instance.Status.HighestGrade;

        for (int i = 0; i < _items.Count; ++i)
        {
            bool isUnlocked = upgrades[i].SpecData.SlimeGrade <= highestGrade || upgrades[i].SpecData.SlimeGrade == ESlimeGrade.None;
            _items[i].Refresh(upgrades[i], isUnlocked);
        }
    }
}
