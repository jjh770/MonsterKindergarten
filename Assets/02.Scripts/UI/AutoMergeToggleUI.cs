using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AutoMergeToggleUI : MonoBehaviour
{
    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private Sprite _onSprite;
    [SerializeField] private Sprite _offSprite;
    [SerializeField] private GameObject _progressTrack;
    [SerializeField] private Image _progressFill;
    [SerializeField] private TextMeshProUGUI _stateLabel;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private Color _onColor = Color.white;
    [SerializeField] private Color _offColor = new(1f, 1f, 1f, 0.55f);

    private bool _isVisible = true;

    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;
    public event Action<bool> StateChanged;

    private void Awake()
    {
        if (_progressFill != null)
        {
            _progressFill.raycastTarget = false;
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Radial360;
            _progressFill.fillOrigin = (int)Image.Origin360.Top;
            _progressFill.fillClockwise = true;
            _progressFill.fillAmount = 0f;
        }

        ApplyVisibility(false);
    }

    private void Start()
    {
        _button?.onClick.AddListener(OnButtonClicked);
        SlimeManager.OnDataInitialized += Refresh;
        SlimeManager.OnNormalCollectionCountChanged += OnCollectionCountChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        _button?.onClick.RemoveListener(OnButtonClicked);
        SlimeManager.OnDataInitialized -= Refresh;
        SlimeManager.OnNormalCollectionCountChanged -= OnCollectionCountChanged;
    }

    private void Update()
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        ApplyVisibility(
            GameplayGate.IsMainStageReady &&
            _panelSwitcher != null &&
            _panelSwitcher.IsAreaVisible &&
            slimeManager != null &&
            slimeManager.IsAutoMergeUnlocked);

        if (_progressFill != null && AutoMergeManager.Instance != null)
        {
            _progressFill.fillAmount = AutoMergeManager.Instance.Progress01;
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
        if (slimeManager == null || _stateLabel == null) return;

        bool isEnabled = slimeManager.IsAutoMergeEnabled;
        if (_icon != null)
        {
            _icon.sprite = isEnabled ? _onSprite : _offSprite;
        }

        _stateLabel.text = isEnabled ? "ON" : "OFF";
        _stateLabel.color = isEnabled ? _onColor : _offColor;
    }

    private void ApplyVisibility(bool isVisible)
    {
        if (_contentRoot == null || _isVisible == isVisible) return;

        _isVisible = isVisible;
        _progressTrack?.SetActive(isVisible);
        _progressFill?.gameObject.SetActive(isVisible);
        _contentRoot.SetActive(isVisible);
    }
}
