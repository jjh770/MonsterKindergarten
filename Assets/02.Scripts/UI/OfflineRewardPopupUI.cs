using System;
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
    [Tooltip("보상 그림이 날아가기 시작하는 자리와 크기입니다. 팝업 안의 보상 그림입니다.")]
    [SerializeField] private RectTransform _rewardFlyVisual;
    [Tooltip("광고 보고 2배 받기 버튼 안의 보상 그림입니다. 두 배로 받을 때는 여기서 날아갑니다.")]
    [SerializeField] private RectTransform _adDoubleFlyVisual;
    [Tooltip("ECurrencyType 순서대로 포인트, 티켓 이미지를 연결합니다.")]
    [SerializeField] private Sprite[] _rewardFlySprites;
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private RectTransform _ticketTarget;
    [Tooltip("상단 바로 날려 보내는 공용 비행 연출입니다.")]
    [SerializeField] private UiIconFlight _iconFlight;
    [SerializeField] private float _fadeDuration = 0.2f;
    [SerializeField] private float _punchDuration = 0.35f;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Open Motion")]
    [Tooltip("팝업 본체가 열리고 닫힐 때의 크기 연출입니다. 비우면 예전처럼 살짝 부풀기만 합니다.")]
    [SerializeField] private PopupMotion _popupMotion;
    [Tooltip("받기를 누른 뒤 본체가 움츠러들어 사라지는 데 걸리는 시간입니다. PopupMotion의 Close Duration과 맞춥니다.")]
    [SerializeField, Min(0.05f)] private float _popupMotionCloseSeconds = 0.25f;
    [Tooltip("위에서부터 차례로 나타날 내용입니다(제목, 시간, 보상, 버튼). 각 항목에 CanvasGroup이 있어야 투명도가 움직입니다.")]
    [SerializeField] private RectTransform[] _revealItems;
    [SerializeField, Min(0f)] private float _revealStartDelay = 0.15f;
    [SerializeField, Min(0f)] private float _revealInterval = 0.1f;
    [SerializeField, Min(0.05f)] private float _revealDuration = 0.35f;
    [SerializeField, Range(0.3f, 1f)] private float _revealStartScale = 0.6f;

    private Sequence _currentSequence;
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

    // 받기를 누르면 보상 그림을 상단 바로 날려 보내고, 그와 동시에 팝업을 닫는다. 날아가는 동안
    // 팝업이 화면을 막을 이유가 없고, 보상은 이미 지급돼 있다. 돌려주는 값은 마지막 코인이 포인트
    // 아이콘에 닿을 때까지의 시간이라, 포인트 숫자가 그 시간에 맞춰 올라간다.
    // 광고로 두 배를 받았으면 눌린 2배 버튼의 그림에서 날아간다.
    public float PlayCollect(TimeSpan elapsedTime, int ticketReward, bool fromAdDouble = false)
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

        RectTransform flyFrom = fromAdDouble && _adDoubleFlyVisual != null
            ? _adDoubleFlyVisual
            : _rewardFlyVisual;
        if (!HasRewardFlyReferences() ||
            !_iconFlight.TryGetLayerSize(flyFrom, null, out Vector2 iconSize))
        {
            return FadeOutAndClose(playArrivalSound: true);
        }

        int pointCount = GetFlyVisualCount(elapsedTime);
        _iconFlight.PlayBundle(
            flyFrom.position,
            _rewardFlySprites[(int)ECurrencyType.Point],
            iconSize,
            _pointTarget,
            pointCount,
            index => PlayArrivalSound(index, EAudioSfx.OfflineRewardArrival),
            null);
        if (ticketReward > 0)
        {
            _iconFlight.PlayBundle(
                flyFrom.position,
                _rewardFlySprites[(int)ECurrencyType.GachaTicket],
                iconSize,
                _ticketTarget,
                ticketReward,
                index => PlayArrivalSound(index, EAudioSfx.TicketCollect),
                null);
        }

        FadeOutAndClose(playArrivalSound: false);
        return _iconFlight.GetBundleDuration(pointCount);
    }

    // 첫 장은 보상의 소리를 내고, 뒤따르는 장은 짧은 소리를 겹치지 않게 흩뿌린다.
    private static void PlayArrivalSound(int index, EAudioSfx firstSound)
    {
        if (AudioManager.Instance == null) return;

        if (index == 0)
        {
            AudioManager.Instance.PlaySFX(firstSound);
            return;
        }

        AudioManager.Instance.PlaySFXRandomPitchWithCooldown(EAudioSfx.TicketCollect, 0.06f);
    }

    private static int GetFlyVisualCount(TimeSpan elapsedTime)
    {
        double offlineHours = elapsedTime.TotalHours;

        if (offlineHours <= 1d) return 10;
        if (offlineHours <= 4d) return 30;
        return 50;
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
               _iconFlight != null &&
               _pointTarget != null &&
               _ticketTarget != null &&
               HasRewardFlySprite(ECurrencyType.Point) &&
               HasRewardFlySprite(ECurrencyType.GachaTicket);
    }

    // 날려 보낼 그림이 없을 때는 닫히는 순간에 도착 소리를 대신 낸다. 날려 보낼 때는 첫 코인이
    // 닿는 순간에 그 소리가 난다.
    private float FadeOutAndClose(bool playArrivalSound)
    {
        _currentSequence = DOTween.Sequence();
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
            ClosePopup(playArrivalSound);
        });

        return duration;
    }

    private void ClosePopup(bool playArrivalSound)
    {
        if (playArrivalSound)
        {
            PlaySound(EAudioSfx.OfflineRewardArrival);
        }

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
