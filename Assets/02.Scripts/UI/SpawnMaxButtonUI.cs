using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnMaxButtonUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;
    [SerializeField] private TextMeshProUGUI _spawnMaxPoint;

    private void Start()
    {
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnMaxChanged += UpdateMaxText;
            UpdateMaxText(SpawnManager.Instance.MaxActiveCount);
        }
        _button.onClick.AddListener(IncreaseMaxCount);
    }

    private void OnDestroy()
    {
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnSpawnMaxChanged -= UpdateMaxText;
        }
    }

    private void IncreaseMaxCount()
    {
        SpawnManager.Instance.IncreaseMaxCount();
    }

    private void UpdateMaxText(int MaxCount)
    {
        if (_spawnMaxText == null) return;
        _spawnMaxText.text = $"<sprite=9>{MaxCount.ToString()} -> {(MaxCount + 1).ToString()}";
    }
}
