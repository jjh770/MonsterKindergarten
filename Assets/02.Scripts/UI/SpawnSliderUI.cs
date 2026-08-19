using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnSliderUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;

    private int _displayedRemainingTenths = int.MinValue;
    private int _displayedActiveCount = int.MinValue;
    private int _displayedMaxCount = int.MinValue;

    private void OnEnable()
    {
        _displayedRemainingTenths = int.MinValue;
        _displayedActiveCount = int.MinValue;
        _displayedMaxCount = int.MinValue;
    }

    private void Update()
    {
        if (SpawnManager.Instance == null) return;

        if (_slider != null)
        {
            _slider.value = SpawnManager.Instance.SpawnProgress;
        }

        if (_spawnIntervalText != null)
        {
            int remainingTenths = Mathf.RoundToInt(
                SpawnManager.Instance.RemainingTime * 10f);

            if (_displayedRemainingTenths != remainingTenths)
            {
                _displayedRemainingTenths = remainingTenths;
                _spawnIntervalText.text = (remainingTenths * 0.1f).ToString("F1");
            }
        }

        if (_spawnMaxText != null)
        {
            int current = SpawnManager.Instance.GetActiveCount();
            int max = SpawnManager.Instance.MaxActiveCount;

            if (_displayedActiveCount != current || _displayedMaxCount != max)
            {
                _displayedActiveCount = current;
                _displayedMaxCount = max;
                _spawnMaxText.text = $"[{current}/{max}]";
            }
        }
    }
}
