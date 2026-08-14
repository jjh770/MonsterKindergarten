using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeUI : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private Button _uiButton;
    [SerializeField] private GameObject _doNotTouchPanel;
    [SerializeField] private float _moveX = 720f;
    [SerializeField] private float _movingDuration = 0.5f;
    private bool _isOpened = false;

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
        _isOpened = !_isOpened;

        _doNotTouchPanel.SetActive(_isOpened);
        if (_isOpened)
        {
            _rectTransform.DOLocalMoveX(_moveX, _movingDuration);
        }
        else
        {
            _rectTransform.DOLocalMoveX(0, _movingDuration);
        }
    }

}
