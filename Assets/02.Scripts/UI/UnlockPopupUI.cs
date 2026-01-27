using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

public class UnlockPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private Image _levelImage;
    [SerializeField] private Image _whiteGlowImage;
    [SerializeField] private Sprite[] _levelSprites;
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
        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.OnHighestLevelChanged += OnHighestLevelChanged;
        }
        _popupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (SlimeSpawner.Instance != null)
        {
            SlimeSpawner.Instance.OnHighestLevelChanged -= OnHighestLevelChanged;
        }
    }

    private void OnHighestLevelChanged(int level)
    {
        if (level <= 1) return;

        ShowPopup(level);
    }

    private void ShowPopup(int level)
    {
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        if (AudioManager.Instance != null && _unlockSound != null)
        {
            AudioManager.Instance.PlaySFX(_unlockSound);
        }

        if (_levelText != null)
        {
            _levelText.text = $"Lv.{level} 해금!";
        }

        if (_levelImage != null && _levelSprites != null && _levelSprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp(level - 1, 0, _levelSprites.Length - 1);
            _levelImage.sprite = _levelSprites[spriteIndex];
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
            _whiteGlowImage.SetImageScaleToZero();
        });
    }
}
