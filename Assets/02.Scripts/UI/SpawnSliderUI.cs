using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnSliderUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;
    private void Update()
    {
        if (SpawnManager.Instance == null) return;

        if (_slider != null)
        {
            _slider.value = SpawnManager.Instance.SpawnProgress;
        }

        if (_spawnIntervalText != null)
        {
            _spawnIntervalText.text = SpawnManager.Instance.RemainingTime.ToString("F1");
        }

        if (_spawnMaxText != null)
        {
            int current = SpawnManager.Instance.GetActiveCount();
            int max = SpawnManager.Instance.MaxActiveCount;
            _spawnMaxText.text = $"[{current}/{max}]";
        }
    }
}
