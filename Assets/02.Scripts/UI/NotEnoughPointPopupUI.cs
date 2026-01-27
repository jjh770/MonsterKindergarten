using DG.Tweening;
using UnityEngine;

public class NotEnoughPointPopupUI : MonoBehaviour
{
    public static NotEnoughPointPopupUI Instance { get; private set; }

    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private float _displayDuration = 1.5f;
    [SerializeField] private float _fadeInDuration = 0.2f;
    [SerializeField] private float _fadeOutDuration = 0.2f;

    private CanvasGroup _canvasGroup;
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

        _canvasGroup = _popupPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = _popupPanel.AddComponent<CanvasGroup>();
        }
        _popupPanel.SetActive(false);
    }

    public void Show(double requiredCost)
    {
        // 이전 애니메이션 취소
        _currentSequence?.Kill();

        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        _currentSequence = DOTween.Sequence();
        _currentSequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
        _currentSequence.Join(_popupPanel.transform.DOPunchPosition(Vector3.one * 15f, 1f, 10, 1));
        _currentSequence.AppendInterval(_displayDuration);
        _currentSequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _currentSequence.OnComplete(() => _popupPanel.SetActive(false));
    }
}
