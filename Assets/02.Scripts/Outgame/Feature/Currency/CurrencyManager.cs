using System;
using UnityEngine;

// 재화 관리자 -> 오로지 "재화" 만 관리하는 클래스
// 클린 아키텍처에서는 "서비스"라는 이름을 씀.
// 게임에서는 보통 "매니저"라고 표현함.
public class CurrencyManager : MonoBehaviour
{
    // 재화에 대한 CRUD 생성 조회 사용 소모 + 재화에 대한 이벤트 추가
    public static CurrencyManager Instance;

    // 재화 데이터
    private double[] _currencies = new double[(int)ECurrencyType.Count];

    // 재화 조회 +@ (편의를 위해 이정도는 눈감아주자)
    public double Point => Get(ECurrencyType.Point);

    //public double Point { get; private set; }
    public event Action<ECurrencyType, double> OnDataChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    // 재화 조회
    public double Get(ECurrencyType currencyType)
    {
        return _currencies[(int)currencyType];
    }

    // 재화 추가
    public void Add(ECurrencyType type, double amount)
    {
        _currencies[(int)type] += amount;
        OnDataChanged?.Invoke(type, _currencies[(int)type]);
    }

    // 재화 소모
    public bool TrySpend(ECurrencyType type, double amount)
    {
        if (_currencies[(int)type] >= amount)
        {
            _currencies[(int)type] -= amount;
            OnDataChanged?.Invoke(type, _currencies[(int)type]);
            return true;
        }
        return false;
    }

    // 소모 가능한지
    public bool CanAfford(ECurrencyType type, double amount)
    {
        return _currencies[(int)type] >= amount;
    }
}
