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
