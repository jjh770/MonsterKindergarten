using DG.Tweening;
using UnityEngine;

public sealed class MainEndingUI : MonoBehaviour
{
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private GameObject _doNotTouchPanel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _popupRectTransform;
    [SerializeField] private UnityEngine.UI.Button _continueButton;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.3f;

    private Sequence _sequence;
    private bool _isPresenting;

    private void Awake()
    {
        if (_popupPanel == null ||
            _doNotTouchPanel == null ||
            _canvasGroup == null ||
            _popupRectTransform == null ||
            _continueButton == null ||
            _clicker == null ||
            _gameExitManager == null)
        {
            Debug.LogError("메인 엔딩 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _popupPanel.SetActive(false);
        _doNotTouchPanel.SetActive(false);
        _continueButton.onClick.AddListener(Complete);
    }

    private void Start()
    {
        if (!enabled) return;

        SlimeManager.OnNormalCollectionCountChanged += OnCollectionCountChanged;
        TutorialManager.Finished += TryShow;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated += TryShow;
        }

        TryShow();
    }

    private void OnDestroy()
    {
        SlimeManager.OnNormalCollectionCountChanged -= OnCollectionCountChanged;
        TutorialManager.Finished -= TryShow;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameplayActivated -= TryShow;
        }

        _continueButton?.onClick.RemoveListener(Complete);
        _gameExitManager?.UnregisterBackHandler(this);
        _clicker?.ReleaseMode(this);
        _sequence?.Kill();
    }

    private void OnCollectionCountChanged(int count)
    {
        if (count >= NormalCollectionRules.MainEndingCount)
        {
            TryShow();
        }
    }

    private void TryShow()
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        if (_isPresenting || slimeManager == null) return;
        if (slimeManager.NormalCollectionCount < NormalCollectionRules.MainEndingCount) return;
        if (slimeManager.IsMainEndingSeen) return;
        if (TutorialManager.IsRunning) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsGameplayActive) return;

        _isPresenting = true;
        transform.SetAsLastSibling();
        _doNotTouchPanel.SetActive(true);
        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;
        _continueButton.interactable = true;
        _clicker.PushMode(
            this,
            ClickerInputMode.Blocked,
            ClickerInputPriority.Modal);
        _gameExitManager.RegisterBackHandler(this, TryComplete);

        _sequence?.Kill();
        _popupRectTransform.localScale = Vector3.one;
        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(1f, _fadeDuration));
        _sequence.Join(_popupRectTransform.DOPunchScale(
            Vector3.one * 0.08f,
            0.45f,
            6,
            0.5f));
        _sequence.OnComplete(() => _sequence = null);
    }

    private void Complete()
    {
        TryComplete();
    }

    private bool TryComplete()
    {
        if (!_isPresenting) return false;

        _continueButton.interactable = false;
        SlimeManager.Instance?.TryMarkMainEndingSeen();
        _gameExitManager.UnregisterBackHandler(this);
        _clicker.ReleaseMode(this);

        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(0f, _fadeDuration));
        _sequence.OnComplete(() =>
        {
            _sequence = null;
            _popupPanel.SetActive(false);
            _doNotTouchPanel.SetActive(false);
            _continueButton.interactable = true;
            _isPresenting = false;
        });
        return true;
    }
}
