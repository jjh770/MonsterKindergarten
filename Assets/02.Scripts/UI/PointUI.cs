using TMPro;
using UnityEngine;

public class PointUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _pointText;

    private ESlimeGrade _highestGrade = ESlimeGrade.Grade1;
    private bool _isInitialized;

    private void Start()
    {
        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        CurrencyManager.Instance.OnDataChanged += OnPointChanged;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        // 이미 초기화가 완료된 경우
        if (GameManager.Instance.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        GameManager.OnAllDataInitialized -= OnAllDataInitialized;
        CurrencyManager.Instance.OnDataChanged -= OnPointChanged;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        _highestGrade = SlimeManager.Instance.Status.HighestGrade;
        UpdateUI();
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        _highestGrade = grade;
        UpdateUI();
    }

    private void OnPointChanged(ECurrencyType type, Currency point)
    {
        if (!_isInitialized) return;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!_isInitialized) return;

        if (_pointText != null)
        {
            _pointText.text = $"<sprite={(int)_highestGrade}>{CurrencyManager.Instance.Point}";
        }
    }
}
