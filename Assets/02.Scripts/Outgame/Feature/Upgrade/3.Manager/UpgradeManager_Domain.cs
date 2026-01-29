using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class UpgradeManager_Domain : MonoBehaviour
{
    public static UpgradeManager_Domain Instance { get; private set; }

    // 이벤트는 도메인이 아닌 매니저가 가져야함.
    public static event Action OnDataChanged;
    [SerializeField] private UpgradeSpecTableSO _specTable;

    private Dictionary<EUpgradeType, Upgrade> _upgrades = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        foreach (var specData in _specTable.Datas)
        {
            if (_upgrades.ContainsKey(specData.Type))
            {
                throw new Exception($"이미 같은 타입의 업그레이드 정보를 가지고 있습니다. {specData.Type}");
            }
            _upgrades.Add(specData.Type, new Upgrade(specData));
        }
        OnDataChanged?.Invoke();
    }

    // 업그레이드를 가져오기
    public Upgrade Get(EUpgradeType type) => _upgrades[type] ?? null;
    public List<Upgrade> GetAll() => _upgrades.Values.ToList();

    // 레벨업 가능한지
    public bool CanLevelUp(EUpgradeType type)
    {
        if (!_upgrades.TryGetValue(type, out Upgrade upgrade))
        {
            return false;
        }
        return CurrencyManager.Instance.CanAfford(ECurrencyType.Point, upgrade.Cost);
    }
    // 레벨업 시도
    public bool TryLevelUp(EUpgradeType type)
    {
        if (!_upgrades.TryGetValue(type, out Upgrade upgrade)) return false;

        Currency cost = upgrade.Cost;

        if (!CurrencyManager.Instance.TrySpend(ECurrencyType.Point, cost)) return false;

        if (!upgrade.TryLevelUp())
        {
            // 레벨업 실패 시 포인트 환불
            CurrencyManager.Instance.Add(ECurrencyType.Point, cost);
            return false;
        }
        OnDataChanged?.Invoke();

        return false;
    }
}
