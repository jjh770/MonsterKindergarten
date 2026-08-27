using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OfflineRewardPopupUI : MonoBehaviour
{
    public event Action ConfirmRequested;
    public event Action PresentationCompleted;

    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private GameObject _doNotTouchPanel;
    [SerializeField] private RectTransform _popupRectTransform;
    [SerializeField] private TextMeshProUGUI _elapsedTimeText;
    [SerializeField] private TextMeshProUGUI _rewardText;
    [SerializeField] private Button _confirmButton;
    [Header("Reward Fly Effect")]
    [SerializeField] private RectTransform _rewardFlyVisual;
    [SerializeField] private Sprite[] _rewardFlySprites;
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private float _flySpawnInterval = 0.04f;
    [SerializeField] private float _scatterDuration = 0.18f;
    [SerializeField] private Vector2 _scatterDistance = new Vector2(140f, 90f);
    [SerializeField] private float _flyDuration = 0.65f;
    [SerializeField] private float _targetPunchScale = 0.12f;
    [SerializeField] private float _fadeDuration = 0.2f;
    [SerializeField] private float _punchDuration = 0.35f;
    [Header("Audio")]
    [SerializeField] private AudioClip _popupOpenSound;
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private AudioClip _arrivalSound;
    [SerializeField] private CanvasGroup _canvasGroup;

    private Sequence _currentSequence;
    private readonly List<RectTransform> _flyingVisuals = new();

    private void Awake()
    {
        if (_popupPanel == null || _canvasGroup == null)
        {
            Debug.LogError("오프라인 보상 팝업의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _popupPanel.SetActive(false);
        _doNotTouchPanel?.SetActive(false);
        _confirmButton?.onClick.AddListener(OnConfirmClicked);
    }

    private void OnDestroy()
    {
        _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
        _currentSequence?.Kill();
        ClearFlyingVisuals();
    }

    public void Show(TimeSpan elapsedTime, Currency reward)
    {
        if (_popupPanel == null || _canvasGroup == null) return;

        if (_elapsedTimeText != null)
        {
            _elapsedTimeText.text = FormatElapsedTime(elapsedTime);
        }

        if (_rewardText != null)
        {
            _rewardText.text = $"{reward} Point";
        }

        _currentSequence?.Kill();
        ClearFlyingVisuals();

        _doNotTouchPanel?.SetActive(true);
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;
        PlaySound(_popupOpenSound);

        if (_confirmButton != null)
        {
            _confirmButton.interactable = true;
        }

        if (_popupRectTransform != null)
        {
            _popupRectTransform.localScale = Vector3.one;
        }

        _currentSequence = DOTween.Sequence();
        _currentSequence.Append(_canvasGroup.DOFade(1f, _fadeDuration));

        if (_popupRectTransform != null)
        {
            _currentSequence.Join(
                _popupRectTransform.DOPunchScale(
                    Vector3.one * 0.08f,
                    _punchDuration,
                    6,
                    0.5f));
        }
    }

    private void OnConfirmClicked()
    {
        ConfirmRequested?.Invoke();
    }

    public float PlayCollect(TimeSpan elapsedTime)
    {
        if (_popupPanel == null || !_popupPanel.activeSelf)
        {
            PresentationCompleted?.Invoke();
            return 0f;
        }

        PlaySound(_collectSound);
        _currentSequence?.Kill();

        if (_confirmButton != null)
        {
            _confirmButton.interactable = false;
        }

        if (_rewardFlyVisual == null || _pointTarget == null)
        {
            return FadeOutAndClose();
        }

        return PlayRewardFlyEffect(elapsedTime);
    }

    private float PlayRewardFlyEffect(TimeSpan elapsedTime)
    {
        ClearFlyingVisuals();
        _currentSequence = DOTween.Sequence();
        int visualCount = GetFlyVisualCount(elapsedTime);

        for (int i = 0; i < visualCount; i++)
        {
            RectTransform flyingVisual = Instantiate(_rewardFlyVisual, transform, true);
            flyingVisual.name = $"OfflineRewardFlyingVisual_{i + 1}";
            _flyingVisuals.Add(flyingVisual);

            ApplyRandomSprite(flyingVisual);

            CanvasGroup flyingCanvasGroup = flyingVisual.GetComponent<CanvasGroup>();
            if (flyingCanvasGroup == null)
            {
                flyingCanvasGroup = flyingVisual.gameObject.AddComponent<CanvasGroup>();
            }

            flyingCanvasGroup.alpha = 1f;
            flyingCanvasGroup.interactable = false;
            flyingCanvasGroup.blocksRaycasts = false;

            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;
            Vector3 scatterPosition = flyingVisual.position + new Vector3(
                randomDirection.x * _scatterDistance.x,
                randomDirection.y * _scatterDistance.y,
                0f);

            float flyDuration = _flyDuration * UnityEngine.Random.Range(0.85f, 1.15f);
            float startDelay = i * Mathf.Max(0f, _flySpawnInterval);

            Sequence flyingSequence = DOTween.Sequence();
            flyingSequence.Append(
                flyingVisual.DOMove(scatterPosition, _scatterDuration)
                    .SetEase(Ease.OutQuad));
            flyingSequence.Append(
                flyingVisual.DOMove(_pointTarget.position, flyDuration)
                    .SetEase(Ease.InCubic));
            flyingSequence.Insert(
                _scatterDuration + flyDuration * 0.9f,
                flyingCanvasGroup.DOFade(0f, flyDuration * 0.1f));

            _currentSequence.Insert(startDelay, flyingSequence);
        }

        // 모든 보상 이미지가 도착한 뒤 팝업과 입력 차단 패널을 닫는다.
        _currentSequence.Append(_canvasGroup.DOFade(0f, _fadeDuration));
        float duration = _currentSequence.Duration();
        _currentSequence.OnComplete(() =>
        {
            _currentSequence = null;
            ClearFlyingVisuals();
            ClosePopup();
            _pointTarget.DOPunchScale(
                Vector3.one * _targetPunchScale,
                _punchDuration,
                6,
                0.5f);
        });

        return duration;
    }

    private static int GetFlyVisualCount(TimeSpan elapsedTime)
    {
        double offlineHours = elapsedTime.TotalHours;

        if (offlineHours <= 1d) return 10;
        if (offlineHours <= 4d) return 30;
        return 50;
    }

    private void ApplyRandomSprite(RectTransform flyingVisual)
    {
        if (_rewardFlySprites == null || _rewardFlySprites.Length == 0) return;

        Image flyingImage = flyingVisual.GetComponent<Image>();
        if (flyingImage == null) return;

        Sprite randomSprite = _rewardFlySprites[
            UnityEngine.Random.Range(0, _rewardFlySprites.Length)];

        if (randomSprite != null)
        {
            flyingImage.sprite = randomSprite;
            flyingImage.preserveAspect = true;
        }
    }

    private void ClearFlyingVisuals()
    {
        foreach (RectTransform flyingVisual in _flyingVisuals)
        {
            if (flyingVisual == null) continue;

            flyingVisual.DOKill();
            Destroy(flyingVisual.gameObject);
        }

        _flyingVisuals.Clear();
    }

    private float FadeOutAndClose()
    {
        _currentSequence = DOTween.Sequence();
        _currentSequence.Append(_canvasGroup.DOFade(0f, _fadeDuration));
        float duration = _currentSequence.Duration();
        _currentSequence.OnComplete(() =>
        {
            _currentSequence = null;
            ClosePopup();
        });

        return duration;
    }

    private void ClosePopup()
    {
        PlaySound(_arrivalSound);
        _popupPanel.SetActive(false);
        _doNotTouchPanel?.SetActive(false);

        if (_confirmButton != null)
        {
            _confirmButton.interactable = true;
        }

        PresentationCompleted?.Invoke();
    }

    private static void PlaySound(AudioClip clip)
    {
        if (AudioManager.Instance != null && clip != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }

    private static string FormatElapsedTime(TimeSpan elapsedTime)
    {
        int totalHours = Mathf.FloorToInt((float)elapsedTime.TotalHours);

        if (totalHours > 0)
        {
            return $"{totalHours}시간 {elapsedTime.Minutes}분";
        }

        return $"{Mathf.Max(1, elapsedTime.Minutes)}분";
    }
}
