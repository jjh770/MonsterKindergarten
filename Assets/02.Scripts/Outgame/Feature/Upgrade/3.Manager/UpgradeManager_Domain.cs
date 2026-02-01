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

    private Dictionary<(EUpgradeType, ESlimeGrade), Upgrade> _upgrades = new();

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
            var key = (specData.Type, specData.SlimeGrade);
            if (_upgrades.ContainsKey(key))
            {
                throw new Exception($"이미 같은 타입의 업그레이드 정보를 가지고 있습니다. {specData.Type}, {specData.SlimeGrade}");
            }
            _upgrades.Add(key, new Upgrade(specData));
        }
        OnDataChanged?.Invoke();
    }

    // 업그레이드를 가져오기
    public Upgrade Get(UpgradeSpecData specData) =>
        _upgrades.TryGetValue((specData.Type, specData.SlimeGrade), out var upgrade) ? upgrade : null;
    public Upgrade Get(EUpgradeType type, ESlimeGrade grade) =>
        _upgrades.TryGetValue((type, grade), out var upgrade) ? upgrade : null;
    public List<Upgrade> GetAll() => _upgrades.Values.ToList();

    // 레벨업 가능한지
    public bool CanLevelUp(UpgradeSpecData specData)
    {
        if (!_upgrades.TryGetValue((specData.Type, specData.SlimeGrade), out Upgrade upgrade)) return false;

        if (!upgrade.CanLevelUp()) return false;
        // 문제 : 왜 도메인에서 Currency 관련 유효성 검사를 하지 않는가.?
        // 도메인 단에서 Currency를 가져오는건 도메인끼리 침범하는 문제가 발생함.
        // 도메인끼리 협력해서 유효성 검사를 하는 곳은 매니저 단에서 실행.
        return CurrencyManager.Instance.CanAfford(ECurrencyType.Point, upgrade.Cost);
    }
    // 레벨업 시도
    public bool TryLevelUp(UpgradeSpecData specData)
    {
        if (!_upgrades.TryGetValue((specData.Type, specData.SlimeGrade), out Upgrade upgrade)) return false;

        Currency cost = upgrade.Cost;

        if (!CurrencyManager.Instance.TrySpend(ECurrencyType.Point, cost)) return false;

        if (!upgrade.TryLevelUp())
        {
            // 레벨업 실패 시 포인트 환불
            CurrencyManager.Instance.Add(ECurrencyType.Point, cost);
            return false;
        }
        OnDataChanged?.Invoke();

        return true;
    }
}
