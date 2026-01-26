using TMPro;
using UnityEngine;

public class PointUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _pointText;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPointChanged += UpdatePointText;
            UpdatePointText(GameManager.Instance.Point);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPointChanged -= UpdatePointText;
        }
    }

    private void UpdatePointText(int point)
    {
        if (_pointText != null)
        {
            _pointText.text = point.ToString();
        }
    }
}
