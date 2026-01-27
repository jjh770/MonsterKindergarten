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

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUpgraded += OnUpgraded;
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnMaxChanged += OnMaxChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPointChanged += OnPointChanged;
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
            SpawnManager.Instance.OnSpawnMaxChanged -= OnMaxChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPointChanged -= OnPointChanged;
        }

        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.OnHighestLevelChanged -= OnHighestLevelChanged;
        }
    }

    private void OnPointChanged(double point)
    {
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (UpgradeManager.Instance == null) return;
        bool isMax = UpgradeManager.Instance.IsMaxLevel(UpgradeType.SpawnMax);
        _button.interactable = !isMax;
    }

    private void OnClickUpgrade()
    {
        if (UpgradeManager.Instance == null) return;

        if (!UpgradeManager.Instance.TryUpgrade(UpgradeType.SpawnMax))
        {
            double cost = UpgradeManager.Instance.GetCost(UpgradeType.SpawnMax);
            NotEnoughPointPopupUI.Instance?.Show(cost);
        }
    }

    private void OnUpgraded(UpgradeType type, int level, double cost)
    {
        if (type == UpgradeType.SpawnMax)
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

        bool isMax = UpgradeManager.Instance.IsMaxLevel(UpgradeType.SpawnMax);
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
                double cost = UpgradeManager.Instance.GetCost(UpgradeType.SpawnMax);
                _costText.text = $"<sprite={_levelIndex}>{cost.ToForamttedString()}";
            }
        }

        _button.interactable = !isMax;
    }
}
