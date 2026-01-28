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

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgraded += OnUpgraded;
        }

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
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgraded -= OnUpgraded;
        }

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

    private void OnPointChanged(ECurrencyType type, double point)
    {
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (UpgradeManager.Instance == null) return;
        bool isMax = UpgradeManager.Instance.IsMaxLevel(UpgradeType.SpawnInterval);
        _button.interactable = !isMax;
    }

    private void OnClickUpgrade()
    {
        if (UpgradeManager.Instance == null) return;

        if (!UpgradeManager.Instance.TryUpgrade(UpgradeType.SpawnInterval))
        {
            double cost = UpgradeManager.Instance.GetCost(UpgradeType.SpawnInterval);
            NotEnoughPointPopupUI.Instance?.Show(cost);
        }
    }

    private void OnUpgraded(UpgradeType type, int level, double cost)
    {
        if (type == UpgradeType.SpawnInterval)
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

        bool isMax = UpgradeManager.Instance.IsMaxLevel(UpgradeType.SpawnInterval);
        float interval = SpawnManager.Instance.SpawnInterval;
        float minInterval = SpawnManager.Instance.MinSpawnInterval;

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
                double cost = UpgradeManager.Instance.GetCost(UpgradeType.SpawnInterval);
                _costText.text = $"<sprite={_levelIndex}>{cost.ToForamttedString()}";
            }
        }

        _button.interactable = !isMax;
    }
}
