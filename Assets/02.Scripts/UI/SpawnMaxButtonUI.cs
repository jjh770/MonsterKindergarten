using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

public class SpawnMaxButtonUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;
    [SerializeField] private TextMeshProUGUI _costText;

    private ESlimeGrade _highestGrade;
    private bool _isInitialized;

    private void Start()
    {
        _button.onClick.AddListener(OnClickUpgrade);

        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        UpgradeManager.OnUpgraded += OnUpgraded;
        SpawnManager.Instance.OnSpawnMaxChanged += OnMaxChanged;
        CurrencyManager.Instance.OnDataChanged += OnPointChanged;
        SlimeManager.OnHighestGradeChanged += OnHighestLevelChanged;

        if (GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        UpgradeManager.OnUpgraded -= OnUpgraded;
        SpawnManager.Instance.OnSpawnMaxChanged -= OnMaxChanged;
        CurrencyManager.Instance.OnDataChanged -= OnPointChanged;
        SlimeManager.OnHighestGradeChanged -= OnHighestLevelChanged;
    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        _highestGrade = SlimeManager.Instance.Status.HighestGrade;
        UpdateUI();
    }

    private void OnPointChanged(ECurrencyType type, Currency point)
    {
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (UpgradeManager.Instance == null) return;
        var upgrade = UpgradeManager.Instance.Get(EUpgradeType.MaxCountAdd, ESlimeGrade.None);
        if (upgrade == null) return;
        bool isMax = upgrade.IsMaxLevel;
        _button.interactable = !isMax;
    }

    private void OnClickUpgrade()
    {
        if (UpgradeManager.Instance == null) return;

        if (!UpgradeManager.Instance.TryLevelUp(EUpgradeType.MaxCountAdd, ESlimeGrade.None))
        {
            var upgrade = UpgradeManager.Instance.Get(EUpgradeType.MaxCountAdd, ESlimeGrade.None);
            if (upgrade != null)
            {
                NotEnoughPointPopupUI.Instance?.Show();
            }
        }
    }

    private void OnUpgraded(EUpgradeType type, ESlimeGrade grade)
    {
        if (type == EUpgradeType.MaxCountAdd)
        {
            UpdateUI();
        }
    }

    private void OnMaxChanged(int maxCount)
    {
        UpdateUI();
    }

    private void OnHighestLevelChanged(ESlimeGrade grade)
    {
        _highestGrade = grade;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!_isInitialized) return;
        if (UpgradeManager.Instance == null || SpawnManager.Instance == null) return;

        var upgrade = UpgradeManager.Instance.Get(EUpgradeType.MaxCountAdd, ESlimeGrade.None);
        if (upgrade == null) return;

        bool isMax = upgrade.IsMaxLevel;
        int maxCount = SpawnManager.Instance.MaxActiveCount;

        if (_spawnMaxText != null)
        {
            if (isMax)
            {
                _spawnMaxText.text = $"<sprite=9>MAX";
            }
            else
            {
                _spawnMaxText.text = $"<sprite=9>{maxCount} -> {maxCount + 1}";
            }
        }

        if (_costText != null)
        {
            if (isMax)
            {
                _costText.text = $"<sprite={(int)_highestGrade}>MAX";
            }
            else
            {
                double cost = (double)upgrade.Cost;
                _costText.text = $"<sprite={(int)_highestGrade}>{cost.ToFormattedString()}";
            }
        }

        _button.interactable = !isMax;
    }
}
