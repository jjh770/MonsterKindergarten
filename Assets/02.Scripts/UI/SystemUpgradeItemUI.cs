using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemUpgradeItemUI : MonoBehaviour
{
    [SerializeField] private EUpgradeType _upgradeType;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _valueText;
    [SerializeField] private TextMeshProUGUI _costText;

    public EUpgradeType UpgradeType => _upgradeType;
    public event Action<EUpgradeType> UpgradeRequested;

    private void Awake()
    {
        _button?.onClick.AddListener(OnClickUpgrade);
    }

    private void OnDestroy()
    {
        _button?.onClick.RemoveListener(OnClickUpgrade);
    }

    public void Refresh(string valueText, string costText, bool isMax)
    {
        if (_valueText != null)
        {
            _valueText.text = valueText;
        }

        if (_costText != null)
        {
            _costText.text = costText;
        }

        if (_button != null)
        {
            _button.interactable = !isMax;
        }
    }

    private void OnClickUpgrade()
    {
        UpgradeRequested?.Invoke(_upgradeType);
    }
}
