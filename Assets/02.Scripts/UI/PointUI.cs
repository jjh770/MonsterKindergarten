using TMPro;
using DG.Tweening;
using UnityEngine;

public class PointUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _pointText;
    [SerializeField] private CurrencyManager _currencyManager;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private OfflineRewardManager _offlineRewardManager;

    private bool _isInitialized;
    private bool _isCountUpPlaying;
    private Tween _countUpTween;

    private void Start()
    {
        if (_currencyManager == null || _gameManager == null ||
            _offlineRewardManager == null)
        {
            Debug.LogError("PointUI에 CurrencyManager 참조가 없습니다.", this);
            enabled = false;
            return;
        }

        _gameManager.AllDataInitialized += OnAllDataInitialized;
        _currencyManager.DataChanged += OnPointChanged;
        PointCountUpEvents.OnRequested += PlayPointCountUp;

        // 이미 초기화가 완료된 경우
        if (_gameManager.IsAllDataInitialized)
        {
            OnAllDataInitialized();
        }
    }

    private void OnDestroy()
    {
        _gameManager.AllDataInitialized -= OnAllDataInitialized;
        if (_currencyManager != null)
        {
            _currencyManager.DataChanged -= OnPointChanged;
        }

        PointCountUpEvents.OnRequested -= PlayPointCountUp;
        _countUpTween?.Kill();
    }

    private void OnAllDataInitialized()
    {
        _isInitialized = true;

        if (_offlineRewardManager.TryGetCurrent(out OfflineRewardResult result))
        {
            UpdateUI((double)result.PointBeforeReward);
        }
        else
        {
            UpdateUI();
        }
    }

    private void OnPointChanged(ECurrencyType type, Currency point)
    {
        if (!_isInitialized || _isCountUpPlaying) return;
        if (_offlineRewardManager.TryGetCurrent(out _)) return;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!_isInitialized) return;

        UpdateUI((double)_currencyManager.Point);
    }

    private void UpdateUI(double point)
    {
        if (!_isInitialized) return;

        if (_pointText != null)
        {
            _pointText.text = $"{CurrencyIcon.Point} {(Currency)point}";
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
