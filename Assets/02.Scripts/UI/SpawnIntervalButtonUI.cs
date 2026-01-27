using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnIntervalButtonUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _spawnIntervalPoint;

    private void Start()
    {
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged += UpdateIntervalText;
            UpdateIntervalText(SpawnManager.Instance.SpawnInterval, SpawnManager.Instance.MinSpawnInterval);
        }
        _button.onClick.AddListener(DecreaseInterval);
    }

    private void OnDestroy()
    {
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnIntervalChanged -= UpdateIntervalText;
        }
    }

    private void DecreaseInterval()
    {
        SpawnManager.Instance.DecreaseInterval();
    }

    private void UpdateIntervalText(float interval, float minInterval)
    {

        if (_spawnIntervalText == null) return;
        if (interval <= minInterval)
        {
            _spawnIntervalText.text = $"<sprite=9>MAX";
            return;
        }
        _spawnIntervalText.text = $"<sprite=9>{interval.ToString("F1")} -> {(interval - 0.1f).ToString("F1")}";
    }
}
