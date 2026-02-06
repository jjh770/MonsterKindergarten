using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

public class SpawnIntervalButtonUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _costText;

    private ESlimeGrade _highestGrade;
    private bool _isInitialized;

    private void Start()
    {
        _button.onClick.AddListener(OnClickUpgrade);

        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        UpgradeManager.OnUpgraded += OnUpgraded;
        SpawnManager.Instance.OnSpawnIntervalChanged += OnIntervalChanged;
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
        SpawnManager.Instance.OnSpawnIntervalChanged -= OnIntervalChanged;
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

    private void OnHighestLevelChanged(ESlimeGrade grade)
    {
        _highestGrade = grade;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!_isInitialized) return;

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
