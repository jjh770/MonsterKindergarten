using DG.Tweening;
using UnityEngine;

public class NotEnoughPointPopupUI : MonoBehaviour
{
    public static NotEnoughPointPopupUI Instance { get; private set; }

    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private float _fadeInDuration = 0.2f;
    [SerializeField] private float _punchDuration = 0.5f;
    [SerializeField] private float _displayDuration = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.2f;
    [SerializeField] private AudioClip _notEnoughSound;
    [SerializeField] private RectTransform _popupRectTransform;
    [SerializeField] private CanvasGroup _canvasGroup;

    private Sequence _currentSequence;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (_popupPanel == null || _canvasGroup == null)
        {
            Debug.LogError("포인트 부족 팝업의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _popupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        _currentSequence?.Kill();
        _currentSequence = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Show()
    {
        // 이전 애니메이션 취소
        _currentSequence?.Kill();

        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        if (AudioManager.Instance != null && _notEnoughSound != null)
        {
            AudioManager.Instance.PlaySFX(_notEnoughSound);
        }

        _currentSequence = DOTween.Sequence();
        _currentSequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
        _currentSequence.Join(_popupRectTransform.DOPunchPosition(Vector3.one * 15f, _punchDuration, 10, 1));
        _currentSequence.AppendInterval(_displayDuration);
        _currentSequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _currentSequence.OnComplete(() =>
        {
            _currentSequence = null;
            _popupPanel.SetActive(false);
        });
    }
}
