using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

public class UnlockPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private TextMeshProUGUI _gradeText;
    [SerializeField] private Image _gradeImage;
    [SerializeField] private Image _whiteGlowImage;
    [SerializeField] private float _displayDuration = 2f;
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.3f;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private Sprite _ticketSprite;
    [Tooltip("후반 등급 해금 보상의 티켓을 지갑에 넣는 곳입니다.")]
    [SerializeField] private GachaTicketField _ticketField;
    [Tooltip("해금 보상의 티켓을 상단 바로 날려 보내는 공용 비행 연출입니다.")]
    [SerializeField] private UiIconFlight _iconFlight;
    [Tooltip("해금 보상의 티켓이 날아가 앉을 상단 바의 티켓 아이콘입니다.")]
    [SerializeField] private RectTransform _ticketIcon;
    [Tooltip("해금 보상이 2장 이상일 때 티켓 그림 옆에 띄우는 X2, X3 표시입니다.")]
    [SerializeField] private TextMeshProUGUI _rewardCountText;

    private Sequence _sequence;
    private Tween _glowScaleTween;
    private Tween _glowRotateTween;
    private bool _isHolding;
    private int _shownGradeRewardCount;

    public event System.Action<ESlimeGrade> PresentationCompleted;

    // 연출이 진행 중일 때만 PresentationCompleted가 발화한다.
    // 대기 여부를 판단하는 쪽에서 이 값을 먼저 확인해야 한다.
    public bool IsPresenting => _sequence != null || _isHolding;

    // 티켓 연출의 그림 자리와 그림. 튜토리얼이 이 자리에서 상단 바로 티켓을 날려 보낸다.
    public RectTransform TicketAnchor => _gradeImage != null ? _gradeImage.rectTransform : null;
    public Sprite TicketSprite => _ticketSprite;

    private void Awake()
    {
        if (_popupPanel == null || _canvasGroup == null ||
            _slimeManager == null)
        {
            Debug.LogError("해금 팝업의 필수 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        _slimeManager.HighestGradeChanged += ShowPopup;
        _popupPanel.SetActive(false);
    }

    private void OnDisable()
    {
        CleanupTweens();
    }

    private void OnDestroy()
    {
        _slimeManager.HighestGradeChanged -= ShowPopup;
        CleanupTweens();
    }

    private void ShowPopup(ESlimeGrade grade)
    {
        ClaimInterruptedGradeReward();
        CleanupTweens();
        SetRewardCount(0);
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock);

        // 등급 숫자만으로는 무엇이 열렸는지 알 수 없다. 이름과 그림이 같은 스펙에서
        // 나오므로 한 번만 찾아 둘 다 쓴다.
        SlimeSpecData specData = _slimeManager.Get(grade)?.SpecData;

        if (_gradeText != null)
        {
            _gradeText.text = $"{specData?.Name ?? grade.ToString()} 해금!";
        }

        if (_gradeImage != null)
        {
            _gradeImage.sprite = specData?.Sprite;
        }

        StartGlow();

        // 페이드 인 -> 대기 -> 페이드 아웃
        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
        _sequence.AppendInterval(_displayDuration);
        _sequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _sequence.OnComplete(() =>
        {
            int rewardCount = GradeReachTicketRewards.GetTicketCount(grade);
            if (rewardCount > 0 && _ticketField != null)
            {
                ShowGradeReward(grade, rewardCount);
                return;
            }

            FinishPopup(grade);
        });
    }

    // 후반 등급을 처음 만들면 슬라임 해금 뒤에 같은 팝업으로 보상 티켓을 보여 준다. 장수는
    // 이미 저장에 들어가 있고(GradeReachTicketRewards), 여기서는 보여 준 뒤 티켓을 상단 바로
    // 날려 보내 지갑에 넣는다. PresentationCompleted는 보상까지 끝난 뒤에 보내서, 해금 팝업을
    // 기다리는 튜토리얼이 보상과 겹쳐 뜨지 않게 한다.
    private void ShowGradeReward(ESlimeGrade grade, int count)
    {
        _shownGradeRewardCount = count;
        _canvasGroup.alpha = 0f;

        AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock);

        if (_gradeText != null)
        {
            _gradeText.text = UiMessages.GradeReachReward;
        }

        if (_gradeImage != null)
        {
            _gradeImage.sprite = _ticketSprite;
        }

        SetRewardCount(count);
        StartGlow();

        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
        _sequence.AppendInterval(_displayDuration);
        // 출발 자리를 먼저 읽어야 하므로 날려 보낸 뒤에 팝업을 닫는다.
        _sequence.AppendCallback(() =>
        {
            _shownGradeRewardCount = 0;
            if (_iconFlight == null)
            {
                _ticketField.ClaimGradeReward(count);
                return;
            }

            _iconFlight.Play(
                TicketAnchor,
                _ticketSprite,
                _ticketIcon,
                () => _ticketField.ClaimGradeReward(count));
        });
        _sequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _sequence.OnComplete(() =>
        {
            SetRewardCount(0);
            FinishPopup(grade);
        });
    }

    private void FinishPopup(ESlimeGrade grade)
    {
        _sequence = null;
        CleanupGlowTweens();
        _popupPanel.SetActive(false);
        _whiteGlowImage?.SetScaleToZero();
        PresentationCompleted?.Invoke(grade);
    }

    // 보상을 보여 주던 중에 다음 해금이나 튜토리얼 티켓이 팝업을 가져가면 날리는 연출 없이
    // 바로 넣는다. 그냥 끊으면 그 몫은 이번 세션에서 잡힌 채로 남는다.
    private void ClaimInterruptedGradeReward()
    {
        if (_shownGradeRewardCount <= 0) return;

        int count = _shownGradeRewardCount;
        _shownGradeRewardCount = 0;
        _ticketField?.ClaimGradeReward(count);
    }

    // 1장이면 티켓 그림만, 2장 이상이면 그림 옆에 X2처럼 붙인다. 0은 표시를 끈다.
    private void SetRewardCount(int count)
    {
        if (_rewardCountText == null) return;

        _rewardCountText.gameObject.SetActive(count > 1);
        _rewardCountText.text = $"X{count}";
    }

    // 뽑기 튜토리얼이 티켓 한 장을 줄 때 쓴다. 슬라임 해금과 같은 연출이지만 스스로 사라지지
    // 않는다. 대화가 넘어갈 때까지 떠 있어야 해서 ReleaseHold가 불릴 때까지 붙든다.
    // PresentationCompleted는 슬라임 등급을 싣고 오므로 이 경로에서는 보내지 않는다.
    public void ShowTicketHold(string title)
    {
        ClaimInterruptedGradeReward();
        CleanupTweens();
        SetRewardCount(0);
        _isHolding = true;
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock);

        if (_gradeText != null)
        {
            _gradeText.text = title;
        }

        if (_gradeImage != null)
        {
            _gradeImage.sprite = _ticketSprite;
        }

        StartGlow();
        _canvasGroup.DOFade(1f, _fadeInDuration).SetId(this);
    }

    public void ReleaseHold()
    {
        if (!_isHolding) return;

        CleanupTweens();
        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _sequence.OnComplete(() =>
        {
            _sequence = null;
            CleanupGlowTweens();
            _popupPanel.SetActive(false);
            _whiteGlowImage?.SetScaleToZero();
        });
    }

    private void StartGlow()
    {
        if (_whiteGlowImage == null) return;

        _whiteGlowImage.SetScaleToZero();
        _glowScaleTween = _whiteGlowImage.transform.DOScale(Vector3.one, 1f);
        _glowRotateTween = _whiteGlowImage.transform.DORotate(
            new Vector3(0, 0, 360),
            3f,
            RotateMode.LocalAxisAdd);
    }

    private void CleanupTweens()
    {
        _isHolding = false;
        DOTween.Kill(this);
        _sequence?.Kill();
        _sequence = null;
        CleanupGlowTweens();
    }

    private void CleanupGlowTweens()
    {
        _glowScaleTween?.Kill();
        _glowRotateTween?.Kill();
        _glowScaleTween = null;
        _glowRotateTween = null;
    }
}
