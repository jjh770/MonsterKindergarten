using TMPro;
using DG.Tweening;
using UnityEngine;

public class PointUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _pointText;

    private ESlimeGrade _highestGrade = ESlimeGrade.Grade1;
    private bool _isInitialized;
    private bool _isCountUpPlaying;
    private Tween _countUpTween;

    private void Start()
    {
        GameManager.OnAllDataInitialized += OnAllDataInitialized;
        CurrencyManager.Instance.OnDataChanged += OnPointChanged;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;
        PointCountUpEvents.OnRequested += PlayPointCountUp;

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
        PointCountUpEvents.OnRequested -= PlayPointCountUp;
        _countUpTween?.Kill();
    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;
        _highestGrade = SlimeManager.Instance.HighestGrade;

        if (GameManager.Instance.TryGetCurrentOfflineReward(out OfflineRewardResult result))
        {
            UpdateUI((double)result.PointBeforeReward);
        }
        else
        {
            UpdateUI();
        }
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        _highestGrade = grade;
        UpdateUI();
    }

    private void OnPointChanged(ECurrencyType type, Currency point)
    {
        if (!_isInitialized || _isCountUpPlaying) return;
        if (GameManager.Instance.TryGetCurrentOfflineReward(out _)) return;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!_isInitialized) return;

        UpdateUI((double)CurrencyManager.Instance.Point);
    }

    private void UpdateUI(double point)
    {
        if (!_isInitialized) return;

        if (_pointText != null)
        {
            _pointText.text = $"<sprite={(int)_highestGrade}>{(Currency)point}";
        }
    }

    private void PlayPointCountUp(PointCountUpRequest request)
    {
        _countUpTween?.Kill();
        _isCountUpPlaying = true;

        double startPoint = (double)request.StartPoint;
        double targetPoint = (double)request.TargetPoint;
        float progress = 0f;

        UpdateUI(startPoint);

        _countUpTween = DOTween.To(
                () => progress,
                value =>
                {
                    progress = value;
                    UpdateUI(startPoint + (targetPoint - startPoint) * value);
                },
                1f,
                Mathf.Max(0.01f, request.Duration))
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                _isCountUpPlaying = false;
                UpdateUI();
            });
    }
}
