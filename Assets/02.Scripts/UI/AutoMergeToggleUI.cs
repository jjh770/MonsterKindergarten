using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AutoMergeToggleUI : MonoBehaviour
{
    [SerializeField] private BooleanToggleButtonView _view;
    [SerializeField] private RadialProgressView _progressView;
    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;
    [SerializeField] private GameObject _progressTrack;
    [SerializeField] private Image _progressFill;
    [SerializeField] private TextMeshProUGUI _stateLabel;
    [SerializeField] private Color _onColor = Color.white;
    [SerializeField] private Color _offColor = new(1f, 1f, 1f, 0.55f);

    public RectTransform ButtonTarget => _view != null ? _view.ButtonTarget : null;
    public event Action<bool> StateChanged;

    private void Awake()
    {
        if (_view == null)
        {
            _view = _contentRoot != null
                ? _contentRoot.GetComponent<BooleanToggleButtonView>()
                : GetComponentInChildren<BooleanToggleButtonView>(true);
        }

        if (_progressView == null)
        {
            _progressView = GetComponent<RadialProgressView>();
        }

        if (_view == null || _progressView == null)
        {
            Debug.LogError("자동 합성 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        if (_button != null && _icon != null && _onSprite != null &&
            _offSprite != null && _stateLabel != null)
        {
            _view.Configure(_button, _icon, _onSprite, _offSprite, _stateLabel,
                _onColor, _offColor);
        }
    }

    private void Start()
    {
        if (!enabled) return;

        _view.Clicked += OnButtonClicked;
        SlimeManager.OnDataInitialized += Refresh;
        SlimeManager.OnNormalCollectionCountChanged += OnCollectionCountChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.Clicked -= OnButtonClicked;
        }

        SlimeManager.OnDataInitialized -= Refresh;
        SlimeManager.OnNormalCollectionCountChanged -= OnCollectionCountChanged;
    }

    private void Update()
    {
        if (AutoMergeManager.Instance != null)
        {
            _progressView.SetProgress(AutoMergeManager.Instance.Progress01);
        }
    }

    private void OnButtonClicked()
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        if (slimeManager == null) return;

        bool isEnabled = !slimeManager.IsAutoMergeEnabled;
        if (!slimeManager.SetAutoMergeEnabled(isEnabled)) return;

        Refresh();
        StateChanged?.Invoke(isEnabled);
    }

    private void OnCollectionCountChanged(int count)
    {
        Refresh();
    }

    private void Refresh()
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        if (slimeManager == null) return;

        _view.SetState(slimeManager.IsAutoMergeEnabled);
    }
}
