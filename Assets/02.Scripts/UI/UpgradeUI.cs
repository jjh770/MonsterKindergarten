using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeUI : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private RectTransform _panelTarget;
    [SerializeField] private Button _uiButton;
    [SerializeField] private GameObject _doNotTouchPanel;
    [SerializeField] private float _moveX = 720f;
    [SerializeField] private float _movingDuration = 0.5f;
    private bool _isOpened = false;
    private bool _isToggleInputEnabled = true;
    private Tween _moveTween;

    public RectTransform ToggleTarget => _uiButton?.transform as RectTransform;
    public RectTransform PanelTarget => _panelTarget;
    public event System.Action Opened;
    public event System.Action Closed;

    private void Start()
    {
        _uiButton.onClick.AddListener(ViewUI);
        _doNotTouchPanel.SetActive(false);
    }

    private void OnDisable()
    {
        _moveTween?.Kill(complete: true);
        _moveTween = null;
    }

    private void ViewUI()
    {
        if (!_isToggleInputEnabled) return;

        SetOpened(!_isOpened);
    }

    public bool TryClose()
    {
        if (!_isOpened) return false;

        SetOpened(false);
        return true;
    }

    private void SetOpened(bool isOpened)
    {
        if (_isOpened == isOpened) return;

        _isOpened = isOpened;

        _doNotTouchPanel.SetActive(_isOpened);
        if (_isOpened)
        {
            MovePanel(_moveX);
            Opened?.Invoke();
        }
        else
        {
            MovePanel(0f);
            Closed?.Invoke();
        }
    }

    private void MovePanel(float targetX)
    {
        _moveTween?.Kill();
        _moveTween = _rectTransform
            .DOLocalMoveX(targetX, _movingDuration)
            .OnComplete(() => _moveTween = null);
    }

    public void SetToggleInputEnabled(bool isEnabled)
    {
        _isToggleInputEnabled = isEnabled;
    }

}
