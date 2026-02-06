using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public static event Action OnAllDataInitialized;

    private bool _isUpgradeInitialized;
    private bool _isSlimeInitialized;
    private bool _isCurrencyInitialized;
    private bool _isAllInitialized;

    // TODO : 데이터 초기화 고려사항
    // 1. 이렇게 전체 데이터를 이벤트 구독해서 확인하는 방법도 있지만
    // 2. GameManager에서 모든 매니저의 데이터를 초기화하라고 시키는 방법도 있음. (이러면 GameManager에서 순차적으로 진행하기 때문에 살짝 느릴 수 있다.)
    // 3. 아예 로딩씬에서 데이터를 모두 초기화하고 게임 씬으로 넘어가는 방법도 있다.
    public bool IsAllDataInitialized => _isAllInitialized;

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
    }

    private void Start()
    {
        UpgradeManager.OnDataInitialized += OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized += OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized += OnCurrencyDataInitialized;
    }

    private void OnDestroy()
    {
        UpgradeManager.OnDataInitialized -= OnUpgradeDataInitialized;
        SlimeManager.OnDataInitialized -= OnSlimeDataInitialized;
        CurrencyManager.Instance.OnDataInitialized -= OnCurrencyDataInitialized;
    }

    private void OnUpgradeDataInitialized()
    {
        _isUpgradeInitialized = true;
        TryInvokeAllInitialized();
    }

    private void OnSlimeDataInitialized()
    {
        _isSlimeInitialized = true;
        TryInvokeAllInitialized();
    }

    private void OnCurrencyDataInitialized()
    {
        _isCurrencyInitialized = true;
        TryInvokeAllInitialized();
    }

    private void TryInvokeAllInitialized()
    {
        if (_isAllInitialized) return;

        if (_isUpgradeInitialized && _isSlimeInitialized && _isCurrencyInitialized)
        {
            _isAllInitialized = true;
            OnAllDataInitialized?.Invoke();
        }
    }
}
