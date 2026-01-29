using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameTextUI;
    [SerializeField] private TextMeshProUGUI _descriptionTextUI;
    [SerializeField] private TextMeshProUGUI _levelTextUI;
    [SerializeField] private TextMeshProUGUI _costTextUI;
    [SerializeField] private Button _upgradeButton;

    private Upgrade _upgrade;
    public void Refresh(Upgrade upgrade)
    {
        _upgrade = upgrade;

        _nameTextUI.text = upgrade.SpecData.Name;
        _descriptionTextUI.text = upgrade.SpecData.Description;
        _levelTextUI.text = upgrade.Level.ToString("N1");
        _costTextUI.text = upgrade.Cost.ToString();

        bool canLevelUp = UpgradeManager_Domain.Instance.CanLevelUp(upgrade.SpecData.Type);
        _costTextUI.color = canLevelUp ? Color.white : Color.red;
        _upgradeButton.interactable = canLevelUp;
    }

    public void LevelUp()
    {
        if (_upgrade == null) return;

        if (UpgradeManager_Domain.Instance.CanLevelUp(_upgrade.SpecData.Type))
        {
            // 이펙트~
        }
    }
}
