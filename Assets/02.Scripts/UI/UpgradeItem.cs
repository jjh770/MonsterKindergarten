using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameTextUI;
    [SerializeField] private TextMeshProUGUI _descriptionTextUI;
    [SerializeField] private TextMeshProUGUI _levelTextUI;
    [SerializeField] private TextMeshProUGUI _costTextUI;
    [SerializeField] private TextMeshProUGUI _statTextUI;
    [SerializeField] private Image _slimeImage;
    [SerializeField] private Sprite _lockedSprite;
    [SerializeField] private Button _upgradeButton;

    private Upgrade _upgrade;
    private Sprite _unlockedSprite;

    private void Awake()
    {
        _upgradeButton.onClick.RemoveAllListeners();
        _upgradeButton.onClick.AddListener(LevelUp);
    }

    public void SetSprite(Sprite sprite)
    {
        _unlockedSprite = sprite;
        if (_slimeImage != null && sprite != null)
            _slimeImage.sprite = sprite;
    }

    public void Refresh(Upgrade upgrade, bool isUnlocked = true)
    {
        _upgrade = upgrade;

        if (isUnlocked)
        {
            _nameTextUI.text = upgrade.SpecData.Name;
            _descriptionTextUI.text = upgrade.SpecData.Description;
            _levelTextUI.text = $"Lv.{upgrade.Level.ToString("N0")}";
            _costTextUI.text = $"Cost:{upgrade.Cost.ToString()}";
            _statTextUI.text = upgrade.IsMaxLevel
                ? $"{upgrade.Point} (MAX)"
                : $"{upgrade.Point} -> {upgrade.NextPoint}";

            if (_slimeImage != null && _unlockedSprite != null)
                _slimeImage.sprite = _unlockedSprite;

            // 외부에서는 Get함수만 접근 가능하게 Interface
            bool canLevelUp = UpgradeManager_Domain.Instance.CanLevelUp(upgrade.SpecData);
            _costTextUI.color = canLevelUp ? Color.black : Color.red;
            _upgradeButton.interactable = canLevelUp;
        }
        else
        {
            _nameTextUI.text = "??";
            _descriptionTextUI.text = "???";
            _levelTextUI.text = "";
            _costTextUI.text = "";
            _statTextUI.text = "";

            if (_slimeImage != null && _lockedSprite != null)
                _slimeImage.sprite = _lockedSprite;

            _upgradeButton.interactable = false;
        }
    }

    public void LevelUp()
    {
        if (_upgrade == null) return;

        if (UpgradeManager_Domain.Instance.CanLevelUp(_upgrade.SpecData))
        {
            UpgradeManager_Domain.Instance.TryLevelUp(_upgrade.SpecData);
        }
    }
}
