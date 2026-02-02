using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

public class SpawnMaxButtonUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;
    [SerializeField] private TextMeshProUGUI _costText;

    private int _levelIndex = 0;

    private void Start()
    {
        _button.onClick.AddListener(OnClickUpgrade);

        UpgradeManager.OnUpgraded += OnUpgraded;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnMaxChanged += OnMaxChanged;
        }

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged += OnPointChanged;
        }

        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.OnHighestLevelChanged += OnHighestLevelChanged;
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
        UpgradeManager.OnUpgraded -= OnUpgraded;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnMaxChanged -= OnMaxChanged;
        }

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged -= OnPointChanged;
        }

        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.OnHighestLevelChanged -= OnHighestLevelChanged;
        }
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
                NotEnoughPointPopupUI.Instance?.Show((double)upgrade.Cost);
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

    private void OnHighestLevelChanged(int level)
    {
        _levelIndex = level - 1;
        UpdateUI();
    }

    private void UpdateUI()
    {
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
                _costText.text = $"<sprite={_levelIndex}>MAX";
            }
            else
            {
                double cost = (double)upgrade.Cost;
                _costText.text = $"<sprite={_levelIndex}>{cost.ToFormattedString()}";
            }
        }

        _button.interactable = !isMax;
    }
}
