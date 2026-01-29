using System.Collections.Generic;
using UnityEngine;

public class UpgradePanel : MonoBehaviour
{
    [SerializeField] private List<UpgradeItem> _items;

    private void Start()
    {
        Refresh();

        //CurrencyManager.Instance.OnDataChanged += Refresh;
        UpgradeManager_Domain.OnDataChanged += Refresh;
    }

    private void Refresh()
    {
        var upgrades = UpgradeManager_Domain.Instance.GetAll();

        for (int i = 0; i < _items.Count; ++i)
        {
            _items[i].Refresh(upgrades[i]);
        }
    }
}
