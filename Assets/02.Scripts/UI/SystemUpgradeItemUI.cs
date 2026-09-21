using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemUpgradeItemUI : MonoBehaviour
{
    [SerializeField] private EUpgradeType _upgradeType;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private TextMeshProUGUI _costText;

    [Tooltip("포인트가 모자랄 때 가격 줄의 불투명도입니다.")]
    [SerializeField, Range(0f, 1f)] private float _unaffordableCostAlpha = 0.4f;

    private bool _isCentered;
    private bool _canPurchase;
    private Currency? _purchaseCost;

    public EUpgradeType UpgradeType => _upgradeType;
    public event Action<SystemUpgradeItemUI> Pressed;

    private void Awake()
    {
        _button?.onClick.AddListener(OnClickUpgrade);
    }

    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged += OnCurrencyChanged;
        }

        RefreshAffordability();
    }

    private void OnDestroy()
    {
        _button?.onClick.RemoveListener(OnClickUpgrade);
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged -= OnCurrencyChanged;
        }
    }

    public void Bind(EUpgradeType upgradeType)
    {
        _upgradeType = upgradeType;

        if (_nameText != null)
        {
            _nameText.text = SystemUpgradeNames.Get(upgradeType);
        }
    }

    public void SetCentered(bool isCentered)
    {
        _isCentered = isCentered;
        RefreshInteractable();
    }

    // purchaseCost는 지금 살 수 있는 단계일 때만 넘긴다. 최대 레벨이나 잠김처럼
    // 가격이 아닌 문구를 보여 줄 때는 흐리게 할 이유가 없다.
    public void Refresh(
        string valueText,
        string costText,
        bool isDisabled,
        Currency? purchaseCost = null)
    {
        if (_valueText != null)
        {
            _valueText.text = valueText;
        }

        if (_costText != null)
        {
            _costText.text = costText;
        }

        _canPurchase = !isDisabled;
        _purchaseCost = purchaseCost;
        RefreshInteractable();
        RefreshAffordability();
    }

    private void OnClickUpgrade()
    {
        Pressed?.Invoke(this);
    }

    private void OnCurrencyChanged(ECurrencyType type, Currency amount)
    {
        if (type == ECurrencyType.Point)
        {
            RefreshAffordability();
        }
    }

    // 포인트가 모자라도 버튼은 눌린다. 누르면 부족 안내가 뜨므로 막지 않고
    // 가격 줄만 흐리게 해 미리 알린다. 카드 전체를 흐리면 선택 안 된 카드와 구분이 안 된다.
    //
    // 카루셀을 다시 그리지 않고 여기서 처리한다. 포인트는 클릭과 자동 생산마다 바뀌고,
    // Rebuild는 슬롯 배치까지 다시 잡아 회전 중인 카드를 제자리로 튕긴다.
    private void RefreshAffordability()
    {
        if (_costText == null) return;

        bool isAffordable = !_purchaseCost.HasValue ||
                            CurrencyManager.Instance == null ||
                            CurrencyManager.Instance.CanAfford(
                                ECurrencyType.Point,
                                _purchaseCost.Value);
        float alpha = isAffordable ? 1f : _unaffordableCostAlpha;
        if (!Mathf.Approximately(_costText.alpha, alpha))
        {
            _costText.alpha = alpha;
        }
    }

    private void RefreshInteractable()
    {
        if (_button != null)
        {
            _button.interactable = _isCentered && _canPurchase;
        }
    }
}
