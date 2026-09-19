using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

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

    [Tooltip("포인트가 모자랄 때 가격 줄의 불투명도입니다. 강화 카드와 같게 맞춥니다.")]
    [SerializeField, Range(0f, 1f)] private float _unaffordableCostAlpha = 0.4f;

    private Upgrade _upgrade;
    private Sprite _unlockedSprite;
    private Color _defaultCostTextColor;

    private void Awake()
    {
        _defaultCostTextColor = _costTextUI.color;
        _upgradeButton.onClick.AddListener(LevelUp);
    }

    private void OnDestroy()
    {
        _upgradeButton.onClick.RemoveListener(LevelUp);
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
            _nameTextUI.text = SlimeManager.Instance.GetName(upgrade.SpecData.SlimeGrade);
            _descriptionTextUI.text = GetDescription(upgrade.SpecData.Type);
            _levelTextUI.text = $"Lv.{upgrade.Level.ToString("N0")}";
            // 강화 카드와 같은 표기를 쓴다. 영어 라벨 대신 재화 아이콘이 가격임을 알린다.
            _costTextUI.text = upgrade.IsMaxLevel
                ? $"{CurrencyIcon.Point}MAX"
                : $"{CurrencyIcon.Point}{((double)upgrade.Cost).ToFormattedString()}";
            _statTextUI.text = upgrade.IsMaxLevel
                ? $"{upgrade.Point.ToFormattedString()} (MAX)"
                : $"{upgrade.Point.ToFormattedString()} → {upgrade.NextPoint.ToFormattedString()}";

            if (_slimeImage != null && _unlockedSprite != null)
                _slimeImage.sprite = _unlockedSprite;

            // 외부에서는 Get함수만 접근 가능하게 Interface
            bool canLevelUp = UpgradeManager.Instance.CanLevelUp(upgrade.SpecData);
            bool isInsufficientCost = !upgrade.IsMaxLevel && !canLevelUp;
            // 빨간색 대신 강화 카드처럼 흐리게 한다. 버튼은 아래에서 따로 막는다.
            _costTextUI.color = _defaultCostTextColor;
            _costTextUI.alpha = isInsufficientCost ? _unaffordableCostAlpha : 1f;
            _upgradeButton.interactable = canLevelUp;
        }
        else
        {
            _nameTextUI.text = "?? 슬라임";
            _descriptionTextUI.text = "???";
            _levelTextUI.text = "";
            _costTextUI.text = "";
            _statTextUI.text = "";

            if (_slimeImage != null && _lockedSprite != null)
                _slimeImage.sprite = _lockedSprite;

            _upgradeButton.interactable = false;
        }
    }

    private static string GetDescription(EUpgradeType type)
    {
        return type switch
        {
            EUpgradeType.ManualPointPlusAdd or
            EUpgradeType.ManualPointPercentAdd => "클릭 획득량 증가",
            EUpgradeType.AutoPointPlusAdd or
            EUpgradeType.AutoPointPercentAdd => "자동 획득량 증가",
            _ => string.Empty,
        };
    }

    private void LevelUp()
    {
        if (_upgrade == null) return;

        if (UpgradeManager.Instance.CanLevelUp(_upgrade.SpecData))
        {
            UpgradeManager.Instance.TryLevelUp(_upgrade.SpecData.Type, _upgrade.SpecData.SlimeGrade);
        }
    }
}
