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
    [Tooltip("ECurrencyType 순서대로 포인트, 티켓 이미지를 연결합니다.")]
    [SerializeField] private Sprite[] _rewardFlySprites;
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private RectTransform _ticketTarget;
    [SerializeField] private float _flySpawnInterval = 0.04f;
    [SerializeField] private float _scatterDuration = 0.18f;
    [SerializeField] private Vector2 _scatterDistance = new Vector2(140f, 90f);
    [SerializeField] private float _flyDuration = 0.65f;
    [SerializeField] private float _targetPunchScale = 0.12f;
    [SerializeField] private float _fadeDuration = 0.2f;
    [SerializeField] private float _punchDuration = 0.35f;
    [SerializeField] private CanvasGroup _canvasGroup;

    private Sequence _currentSequence;
    private readonly List<RectTransform> _flyingVisuals = new();

    private void Awake()
    {
        if (_popupPanel == null ||
            _canvasGroup == null ||
            !HasRewardFlyReferences())
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

    public void Show(TimeSpan elapsedTime, Currency reward, int ticketReward)
    {
        if (_popupPanel == null || _canvasGroup == null) return;

        if (_elapsedTimeText != null)
        {
            _elapsedTimeText.text = $"잠시 떠난 시간 : {FormatElapsedTime(elapsedTime)}";
        }

        if (_rewardText != null)
        {
            string pointLine = $"획득한 포인트 : {CurrencyIcon.Point}{reward}";
            _rewardText.text = ticketReward > 0
                ? $"획득한 티켓 : {pointLine}\n{CurrencyIcon.GachaTicket}+{ticketReward}"
                : pointLine;
        }

        _currentSequence?.Kill();
        ClearFlyingVisuals();

        _doNotTouchPanel?.SetActive(true);
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;
        PlaySound(EAudioSfx.OfflineRewardOpen);

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

    public float PlayCollect(TimeSpan elapsedTime, int ticketReward)
    {
        if (_popupPanel == null || !_popupPanel.activeSelf)
        {
            PresentationCompleted?.Invoke();
            return 0f;
        }

        PlaySound(EAudioSfx.OfflineRewardCollect);
        _currentSequence?.Kill();

        if (_confirmButton != null)
        {
            _confirmButton.interactable = false;
        }

        if (!HasRewardFlyReferences())
        {
            return FadeOutAndClose();
        }

        return PlayRewardFlyEffect(elapsedTime, Mathf.Max(0, ticketReward));
    }

    private float PlayRewardFlyEffect(TimeSpan elapsedTime, int ticketReward)
    {
        ClearFlyingVisuals();
        _currentSequence = DOTween.Sequence();
        AddRewardFlyVisuals(
            GetFlyVisualCount(elapsedTime),
            ECurrencyType.Point,
            _pointTarget);
        AddRewardFlyVisuals(
            ticketReward,
            ECurrencyType.GachaTicket,
            _ticketTarget);

        // 모든 보상 이미지가 도착한 뒤 팝업과 입력 차단 패널을 닫는다.
        _currentSequence.Append(_canvasGroup.DOFade(0f, _fadeDuration));
        float duration = _currentSequence.Duration();
        _currentSequence.OnComplete(() =>
        {
            _currentSequence = null;
            ClearFlyingVisuals();
            ClosePopup();
            PunchTarget(_pointTarget);
            if (ticketReward > 0)
            {
                PunchTarget(_ticketTarget);
            }
        });

        return duration;
    }

    private void AddRewardFlyVisuals(
        int visualCount,
        ECurrencyType currencyType,
        RectTransform target)
    {
        for (int i = 0; i < visualCount; i++)
        {
            RectTransform flyingVisual = Instantiate(_rewardFlyVisual, transform, true);
            flyingVisual.name = $"OfflineReward{currencyType}Visual_{i + 1}";
            _flyingVisuals.Add(flyingVisual);

            ApplyRewardSprite(flyingVisual, currencyType);

            CanvasGroup flyingCanvasGroup = flyingVisual.GetComponent<CanvasGroup>();
            if (flyingCanvasGroup == null)
            {
                flyingCanvasGroup = flyingVisual.gameObject.AddComponent<CanvasGroup>();
            }

            flyingCanvasGroup.alpha = 1f;
            flyingCanvasGroup.interactable = false;
            flyingCanvasGroup.blocksRaycasts = false;

            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;
            // _scatterDistance는 비행 이미지 부모(this.transform)의 로컬 단위다.
            // 복제본 자신의 lossyScale에는 원본 이미지의 로컬 스케일(0.5)이 섞여 폭이 절반이 되므로,
            // 부모의 TransformVector로 부모 체인의 회전·스케일(CanvasScaler 포함)만 반영해 world 벡터로 바꾼다.
            Vector3 scatterOffset = transform.TransformVector(new Vector3(
                randomDirection.x * _scatterDistance.x,
                randomDirection.y * _scatterDistance.y,
                0f));
            Vector3 scatterPosition = flyingVisual.position + scatterOffset;

            float flyDuration = _flyDuration * UnityEngine.Random.Range(0.85f, 1.15f);
            float startDelay = i * Mathf.Max(0f, _flySpawnInterval);

            Sequence flyingSequence = DOTween.Sequence();
            flyingSequence.Append(
                flyingVisual.DOMove(scatterPosition, _scatterDuration)
                    .SetEase(Ease.OutQuad));
            flyingSequence.Append(
                flyingVisual.DOMove(target.position, flyDuration)
                    .SetEase(Ease.InCubic));
            flyingSequence.Insert(
                _scatterDuration + flyDuration * 0.9f,
                flyingCanvasGroup.DOFade(0f, flyDuration * 0.1f));

            _currentSequence.Insert(startDelay, flyingSequence);
        }
    }

    private static int GetFlyVisualCount(TimeSpan elapsedTime)
    {
        double offlineHours = elapsedTime.TotalHours;

        if (offlineHours <= 1d) return 10;
        if (offlineHours <= 4d) return 30;
        return 50;
    }

    private void ApplyRewardSprite(
        RectTransform flyingVisual,
        ECurrencyType currencyType)
    {
        Image flyingImage = flyingVisual.GetComponent<Image>();
        if (flyingImage == null) return;

        Sprite rewardSprite = _rewardFlySprites[(int)currencyType];
        if (rewardSprite != null)
        {
            flyingImage.sprite = rewardSprite;
            flyingImage.preserveAspect = true;
        }
    }

    private bool HasRewardFlySprite(ECurrencyType currencyType)
    {
        int index = (int)currencyType;
        return _rewardFlySprites != null &&
               index >= 0 &&
               index < _rewardFlySprites.Length &&
               _rewardFlySprites[index] != null;
    }

    private bool HasRewardFlyReferences()
    {
        return _rewardFlyVisual != null &&
               _pointTarget != null &&
               _ticketTarget != null &&
               HasRewardFlySprite(ECurrencyType.Point) &&
               HasRewardFlySprite(ECurrencyType.GachaTicket);
    }

    private void PunchTarget(RectTransform target)
    {
        target.DOPunchScale(
            Vector3.one * _targetPunchScale,
            _punchDuration,
            6,
            0.5f);
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
        PlaySound(EAudioSfx.OfflineRewardArrival);
        _popupPanel.SetActive(false);
        _doNotTouchPanel?.SetActive(false);

        if (_confirmButton != null)
        {
            _confirmButton.interactable = true;
        }

        PresentationCompleted?.Invoke();
    }

    private void PlaySound(EAudioSfx cue)
    {
        AudioManager.Instance?.PlaySFX(cue);
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
