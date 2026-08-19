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

    public RectTransform ToggleTarget => _uiButton?.transform as RectTransform;
    public RectTransform PanelTarget => _panelTarget;
    public event System.Action Opened;
    public event System.Action Closed;

    private void Start()
    {
        _uiButton.onClick.AddListener(ViewUI);
        _doNotTouchPanel.SetActive(false);
        AddButtonOutline();
    }

    private void AddButtonOutline()
    {
        if (_uiButton == null || _uiButton.targetGraphic == null) return;

        Outline outline = _uiButton.targetGraphic.GetComponent<Outline>();
        if (outline == null)
        {
            outline = _uiButton.targetGraphic.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;
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
            _rectTransform.DOLocalMoveX(_moveX, _movingDuration);
            Opened?.Invoke();
        }
        else
        {
            _rectTransform.DOLocalMoveX(0, _movingDuration);
            Closed?.Invoke();
        }
    }

    public void SetToggleInputEnabled(bool isEnabled)
    {
        _isToggleInputEnabled = isEnabled;
    }

}
