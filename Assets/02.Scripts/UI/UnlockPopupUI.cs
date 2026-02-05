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

    private void ShowPopup(ESlimeGrade grade)
    {
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        if (AudioManager.Instance != null && _unlockSound != null)
        {
            AudioManager.Instance.PlaySFX(_unlockSound);
        }

        if (_gradeText != null)
        {
            _gradeText.text = $"Lv.{grade} 해금!";
        }

        if (_gradeImage != null)
        {
            //TODO
            //_gradeImage.sprite = _monsterLevelData.GetSprite(grade);
        }

        _whiteGlowImage.transform.DOScale(Vector3.one, 1f);
        _whiteGlowImage.transform.DORotate(new Vector3(0, 0, 360), 3f, RotateMode.LocalAxisAdd);

        // 페이드 인 -> 대기 -> 페이드 아웃
        Sequence sequence = DOTween.Sequence();
        sequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
        sequence.AppendInterval(_displayDuration);
        sequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        sequence.OnComplete(() =>
        {
            _popupPanel.SetActive(false);
            _whiteGlowImage.SetScaleToZero();
        });
    }
}
