using System.Collections.Generic;
using UnityEngine;

public class UpgradePanel : MonoBehaviour
{
    [SerializeField] private UpgradeItem _itemPrefab;
    [SerializeField] private Transform _content;

    private List<UpgradeItem> _items = new();
    private bool _isInitialized;

    private void Start()
    {
        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        CurrencyManager.Instance.OnDataChanged += RefreshCurrency;
        UpgradeManager.OnDataChanged += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        // 이미 초기화가 완료된 경우
        if (GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        CurrencyManager.Instance.OnDataChanged -= RefreshCurrency;
        UpgradeManager.OnDataChanged -= Refresh;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        CreateItems();
        Refresh();
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        Refresh();
    }

    private void CreateItems()
    {
        var upgrades = UpgradeManager.Instance.GetSlimeUpgrades();

        foreach (var upgrade in upgrades)
        {
            var item = Instantiate(_itemPrefab, _content);
            item.SetSprite(SlimeManager.Instance.Get(upgrade.SpecData.SlimeGrade)?.SpecData.Sprite);
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
        if (!_isInitialized) return;

        var upgrades = UpgradeManager.Instance.GetSlimeUpgrades();
        ESlimeGrade highestGrade = SlimeManager.Instance.Status.HighestGrade;

        for (int i = 0; i < _items.Count; ++i)
        {
            bool isUnlocked = upgrades[i].SpecData.SlimeGrade <= highestGrade || upgrades[i].SpecData.SlimeGrade == ESlimeGrade.None;
            _items[i].Refresh(upgrades[i], isUnlocked);
        }
    }
}
