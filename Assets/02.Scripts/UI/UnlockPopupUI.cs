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
    [SerializeField] private AudioClip _unlockSound;

    private CanvasGroup _canvasGroup;
    private Sequence _sequence;
    private Tween _glowScaleTween;
    private Tween _glowRotateTween;

    public event System.Action<ESlimeGrade> PresentationCompleted;

    // 연출이 진행 중일 때만 PresentationCompleted가 발화한다.
    // 대기 여부를 판단하는 쪽에서 이 값을 먼저 확인해야 한다.
    public bool IsPresenting => _sequence != null;

    private void Awake()
    {
        _canvasGroup = _popupPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = _popupPanel.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        SlimeManager.OnHighestGradeChanged += ShowPopup;
        _popupPanel.SetActive(false);
    }

    private void OnDisable()
    {
        CleanupTweens();
    }

    private void OnDestroy()
    {
        SlimeManager.OnHighestGradeChanged -= ShowPopup;
        CleanupTweens();
    }

    private void ShowPopup(ESlimeGrade grade)
    {
        CleanupTweens();
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        if (AudioManager.Instance != null && _unlockSound != null)
        {
            AudioManager.Instance.PlaySFX(_unlockSound);
        }

        if (_gradeText != null)
        {
            _gradeText.text = $"Lv.{(int)grade} 해금!";
        }

        if (_gradeImage != null)
        {
            _gradeImage.sprite = SlimeManager.Instance.Get(grade)?.SpecData.Sprite;
        }

        if (_whiteGlowImage != null)
        {
            _whiteGlowImage.SetScaleToZero();
            _glowScaleTween = _whiteGlowImage.transform.DOScale(Vector3.one, 1f);
            _glowRotateTween = _whiteGlowImage.transform.DORotate(
                new Vector3(0, 0, 360),
                3f,
                RotateMode.LocalAxisAdd);
        }

        // 페이드 인 -> 대기 -> 페이드 아웃
        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
        _sequence.AppendInterval(_displayDuration);
        _sequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _sequence.OnComplete(() =>
        {
            _sequence = null;
            CleanupGlowTweens();
            _popupPanel.SetActive(false);
            _whiteGlowImage?.SetScaleToZero();
            PresentationCompleted?.Invoke(grade);
        });
    }

    private void CleanupTweens()
    {
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
