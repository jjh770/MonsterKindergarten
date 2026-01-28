using TMPro;
using UnityEngine;
using Utility;

public class PointUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _pointText;

    private int _highestLevel = 1;

    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged += OnPointChanged;
        }

        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.OnHighestLevelChanged += OnHighestLevelChanged;
            _highestLevel = SlimeSpawner.Instance.HighestLevel;
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
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
        UpdateUI();
    }

    private void OnHighestLevelChanged(int level)
    {
        _highestLevel = level;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_pointText != null && GameManager.Instance != null)
        {
            int spriteIndex = _highestLevel - 1;
            _pointText.text = $"<sprite={spriteIndex}>{CurrencyManager.Instance.Point.ToForamttedString()}";
        }
    }
}
