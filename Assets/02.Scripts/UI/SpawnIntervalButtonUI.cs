using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

public class SpawnIntervalButtonUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _costText;

    private int _levelIndex = 0;

    private void Start()
    {
        _button.onClick.AddListener(OnClickUpgrade);

        UpgradeManager.OnUpgraded += OnUpgraded;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged += OnIntervalChanged;
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
            SpawnManager.Instance.OnSpawnIntervalChanged -= OnIntervalChanged;
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
        var upgrade = UpgradeManager.Instance.Get(EUpgradeType.SpawnTimeSub, ESlimeGrade.None);
        if (upgrade == null) return;
        bool isMax = upgrade.IsMaxLevel;
        _button.interactable = !isMax;
    }

    private void OnClickUpgrade()
    {
        if (UpgradeManager.Instance == null) return;

        if (!UpgradeManager.Instance.TryLevelUp(EUpgradeType.SpawnTimeSub, ESlimeGrade.None))
        {
            var upgrade = UpgradeManager.Instance.Get(EUpgradeType.SpawnTimeSub, ESlimeGrade.None);
            if (upgrade != null)
            {
                NotEnoughPointPopupUI.Instance?.Show((double)upgrade.Cost);
            }
        }
    }

    private void OnUpgraded(EUpgradeType type, ESlimeGrade grade)
    {
        if (type == EUpgradeType.SpawnTimeSub)
        {
            UpdateUI();
        }
    }

    private void OnIntervalChanged(float interval, float minInterval)
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

        var upgrade = UpgradeManager.Instance.Get(EUpgradeType.SpawnTimeSub, ESlimeGrade.None);
        if (upgrade == null) return;

        bool isMax = upgrade.IsMaxLevel;
        float interval = SpawnManager.Instance.SpawnInterval;
        float minInterval = SpawnManager.Instance.MinSpawnInterval;

        if (isMax || interval <= minInterval)
        {
            isMax = true;
        }

        if (_spawnIntervalText != null)
        {
            if (isMax)
            {
                _spawnIntervalText.text = $"<sprite=8>MAX";
            }
            else
            {
                _spawnIntervalText.text = $"<sprite=8>{interval:F1} -> {(interval - 0.1f):F1}";
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
