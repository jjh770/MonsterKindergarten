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
    public event Action AdDoubleRequested;

    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private GameObject _doNotTouchPanel;
    [SerializeField] private RectTransform _popupRectTransform;
    [SerializeField] private TextMeshProUGUI _elapsedTimeText;
    [SerializeField] private TextMeshProUGUI _rewardText;
    [SerializeField] private Button _confirmButton;
    [Tooltip("광고를 보고 포인트를 두 배로 받는 버튼입니다. 광고를 볼 수 있을 때만 켭니다.")]
    [SerializeField] private Button _adDoubleButton;
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

    [Header("Open Motion")]
    [Tooltip("팝업 본체가 열리고 닫힐 때의 크기 연출입니다. 비우면 예전처럼 살짝 부풀기만 합니다.")]
    [SerializeField] private PopupMotion _popupMotion;
    [Tooltip("코인이 다 도착한 뒤 본체가 움츠러들어 사라지는 데 걸리는 시간입니다. PopupMotion의 Close Duration과 맞춥니다.")]
    [SerializeField, Min(0.05f)] private float _popupMotionCloseSeconds = 0.25f;
    [Tooltip("위에서부터 차례로 나타날 내용입니다(제목, 시간, 보상, 버튼). 각 항목에 CanvasGroup이 있어야 투명도가 움직입니다.")]
    [SerializeField] private RectTransform[] _revealItems;
    [SerializeField, Min(0f)] private float _revealStartDelay = 0.15f;
    [SerializeField, Min(0f)] private float _revealInterval = 0.1f;
    [SerializeField, Min(0.05f)] private float _revealDuration = 0.35f;
    [SerializeField, Range(0.3f, 1f)] private float _revealStartScale = 0.6f;

    [Header("Fly Path")]
    [Tooltip("코인이 목표로 날아갈 때 옆으로 휘는 정도입니다. 이동 거리에 대한 비율이고 0이면 직선입니다.")]
    [SerializeField, Range(0f, 0.6f)] private float _flyArc = 0.3f;

    private Sequence _currentSequence;
    private readonly List<RectTransform> _flyingVisuals = new();
    private Vector3[] _revealBaseScales;

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

        CaptureRevealScales();
        _popupPanel.SetActive(false);
        _doNotTouchPanel?.SetActive(false);
        _confirmButton?.onClick.AddListener(OnConfirmClicked);
        _adDoubleButton?.onClick.AddListener(OnAdDoubleClicked);
        _adDoubleButton?.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _confirmButton?.onClick.RemoveListener(OnConfirmClicked);
        _adDoubleButton?.onClick.RemoveListener(OnAdDoubleClicked);
        _currentSequence?.Kill();
        ClearFlyingVisuals();
    }

    public void Show(TimeSpan elapsedTime, Currency reward, int ticketReward)
    {
        if (_popupPanel == null || _canvasGroup == null) return;

        if (_elapsedTimeText != null)
        {
            _elapsedTimeText.text = $"자리를 비운 시간 : {FormatElapsedTime(elapsedTime)}";
        }

        if (_rewardText != null)
        {
            string pointLine = $"모아 둔 포인트 : {CurrencyIcon.Point}{reward}";
            _rewardText.text = ticketReward > 0
                ? $"{pointLine}\n모아 둔 뽑기권 : {CurrencyIcon.GachaTicket}+{ticketReward}"
                : pointLine;
        }

        _currentSequence?.Kill();
        ClearFlyingVisuals();

        _doNotTouchPanel?.SetActive(true);
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;
        PlaySound(EAudioSfx.OfflineRewardOpen);

        if (_adDoubleButton != null)
        {
            _adDoubleButton.interactable = true;
        }

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

        if (_popupMotion != null)
        {
            // 본체는 작은 크기에서 튀어 오르며 나타난다.
            _currentSequence.Join(_popupMotion.PlayOpen());
        }
        else if (_popupRectTransform != null)
        {
            _currentSequence.Join(
                _popupRectTransform.DOPunchScale(
                    Vector3.one * 0.08f,
                    _punchDuration,
                    6,
                    0.5f));
        }

        PlayReveal();
    }

    private void CaptureRevealScales()
    {
        if (_revealItems == null) return;

        _revealBaseScales = new Vector3[_revealItems.Length];
        for (int i = 0; i < _revealItems.Length; i++)
        {
            _revealBaseScales[i] = _revealItems[i] != null
                ? _revealItems[i].localScale
                : Vector3.one;
        }
    }

    // 내용이 한꺼번에 나오지 않고 읽는 순서대로 하나씩 튀어 오른다.
    private void PlayReveal()
    {
        if (_revealItems == null || _revealBaseScales == null) return;

        for (int i = 0; i < _revealItems.Length; i++)
        {
            RectTransform item = _revealItems[i];
            if (item == null) continue;

            item.DOKill();
            item.localScale = _revealBaseScales[i] * _revealStartScale;

            CanvasGroup itemGroup = item.GetComponent<CanvasGroup>();
            float delay = _revealStartDelay + i * _revealInterval;

            _currentSequence.Insert(
                delay,
                item.DOScale(_revealBaseScales[i], _revealDuration).SetEase(Ease.OutBack));

            if (itemGroup != null)
            {
                itemGroup.DOKill();
                itemGroup.alpha = 0f;
                _currentSequence.Insert(
                    delay,
                    itemGroup.DOFade(1f, _revealDuration * 0.6f));
            }
        }
    }

    // 받기를 일찍 눌러 등장이 끝나기 전에 끊겨도 내용이 반쯤 투명하게 남지 않게 한다.
    private void ShowRevealItemsAtOnce()
    {
        if (_revealItems == null || _revealBaseScales == null) return;

        for (int i = 0; i < _revealItems.Length; i++)
        {
            RectTransform item = _revealItems[i];
            if (item == null) continue;

            item.DOKill();
            item.localScale = _revealBaseScales[i];

            CanvasGroup itemGroup = item.GetComponent<CanvasGroup>();
            if (itemGroup != null)
            {
                itemGroup.DOKill();
                itemGroup.alpha = 1f;
            }
        }
    }

    private void OnAdDoubleClicked()
    {
        AdDoubleRequested?.Invoke();
    }

    public void SetAdDoubleVisible(bool isVisible)
    {
        _adDoubleButton?.gameObject.SetActive(isVisible);
    }

    // 광고를 보는 동안 두 버튼을 모두 잠근다.
    public void SetBusy(bool isBusy)
    {
        if (_confirmButton != null)
        {
            _confirmButton.interactable = !isBusy;
        }

        if (_adDoubleButton != null)
        {
            _adDoubleButton.interactable = !isBusy;
        }
    }

    // 광고를 보고 두 배로 받게 됐을 때 보상 문구를 바뀐 값으로 고친다.
    public void ShowDoubledReward(Currency reward, int ticketReward)
    {
        if (_rewardText == null) return;

        string pointLine = $"모아 둔 포인트 : {CurrencyIcon.Point}{reward} (2배)";
        _rewardText.text = ticketReward > 0
            ? $"{pointLine}\n모아 둔 뽑기권 : {CurrencyIcon.GachaTicket}+{ticketReward}"
            : pointLine;
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
        ShowRevealItemsAtOnce();
        SetBusy(true);

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

        // 모든 보상 이미지가 도착한 뒤에 팝업을 닫는다. 코인이 날아가는 동안에는 본체가 그대로 있어
        // 코인이 어디서 나와 어디로 가는지 보이고, 다 도착하면 움츠러들며 사라진다.
        if (_popupMotion != null)
        {
            _currentSequence.AppendCallback(() => _popupMotion.PlayClose());
            _currentSequence.AppendInterval(_popupMotionCloseSeconds);
        }
        else
        {
            _currentSequence.Append(_canvasGroup.DOFade(0f, _fadeDuration));
        }

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
            Vector3 flyControl = GetFlyControlPoint(scatterPosition, target.position);

            float flyDuration = _flyDuration * UnityEngine.Random.Range(0.85f, 1.15f);
            float startDelay = i * Mathf.Max(0f, _flySpawnInterval);

            Sequence flyingSequence = DOTween.Sequence();
            flyingSequence.Append(
                flyingVisual.DOMove(scatterPosition, _scatterDuration)
                    .SetEase(Ease.OutQuad));
            // 직선이 아니라 옆으로 휘어 날아가 도착하는 모습이 살아 있다.
            Vector3 flyStart = scatterPosition;
            Vector3 flyEnd = target.position;
            flyingSequence.Append(
                DOVirtual.Float(0f, 1f, flyDuration, t =>
                {
                    float u = 1f - t;
                    flyingVisual.position =
                        u * u * flyStart + 2f * u * t * flyControl + t * t * flyEnd;
                }).SetEase(Ease.InQuad));
            flyingSequence.Insert(
                _scatterDuration + flyDuration * 0.9f,
                flyingCanvasGroup.DOFade(0f, flyDuration * 0.1f));

            _currentSequence.Insert(startDelay, flyingSequence);
        }
    }

    // 시작점과 목표 사이의 가운데에서 수직 방향으로 비켜선 점. 코인마다 좌우가 달라 한 줄로 쏠리지 않는다.
    private Vector3 GetFlyControlPoint(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        Vector3 perpendicular = new Vector3(-delta.y, delta.x, 0f);
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float bend = _flyArc * UnityEngine.Random.Range(0.6f, 1.2f);
        return (from + to) * 0.5f + perpendicular * (bend * side);
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
        _popupMotion?.PlayClose();
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
        SetBusy(false);
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
