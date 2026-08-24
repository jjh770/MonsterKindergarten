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

    private bool _isCentered;
    private bool _canPurchase;

    public EUpgradeType UpgradeType => _upgradeType;
    public event Action<SystemUpgradeItemUI> Pressed;

    private void Awake()
    {
        _button?.onClick.AddListener(OnClickUpgrade);
    }

    private void OnDestroy()
    {
        _button?.onClick.RemoveListener(OnClickUpgrade);
    }

    public void Bind(EUpgradeType upgradeType)
    {
        _upgradeType = upgradeType;

        if (_nameText != null)
        {
            _nameText.text = GetName(upgradeType);
        }
    }

    public void SetCentered(bool isCentered)
    {
        _isCentered = isCentered;
        RefreshInteractable();
    }

    public void Refresh(string valueText, string costText, bool isDisabled)
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
        RefreshInteractable();
    }

    private void OnClickUpgrade()
    {
        Pressed?.Invoke(this);
    }

    private void RefreshInteractable()
    {
        if (_button != null)
        {
            _button.interactable = _isCentered && _canPurchase;
        }
    }

    private static string GetName(EUpgradeType upgradeType)
    {
        return upgradeType switch
        {
            EUpgradeType.SpawnTimeSub => "스폰 시간 단축",
            EUpgradeType.MaxCountAdd => "최대 슬라임 수",
            EUpgradeType.HigherGradeSpawnWeightAdd => "상위 슬라임 등장 확률",
            _ => upgradeType.ToString(),
        };
    }
}
