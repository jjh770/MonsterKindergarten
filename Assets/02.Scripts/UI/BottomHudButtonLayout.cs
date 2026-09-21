using UnityEngine;
using TMPro;

// 하단 기능 버튼들의 세이프 에어리어 대응 위치만 담당한다.
// 버튼의 기능, 해금, 패널 선택 상태는 다루지 않는다.
public sealed class BottomHudButtonLayout : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _systemUpgradePanel;
    [SerializeField] private RectTransform[] _leftButtons;
    [SerializeField] private RectTransform _rightButton;
    [SerializeField, Min(0f)] private float _buttonMargin = 20f;
    [SerializeField] private Vector2 _labelSize = new(160f, 40f);
    [SerializeField, Min(1f)] private float _labelFontSize = 32f;

    private Vector2 _systemUpgradeStartPosition;
    private bool _isInitialized;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _systemUpgradeStartPosition = _systemUpgradePanel.anchoredPosition;
        _isInitialized = true;
    }

    private void Start()
    {
        RefreshLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isInitialized)
        {
            RefreshLayout();
        }
    }

    public void RefreshLayout()
    {
        if (!_isInitialized) return;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null) return;

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(canvasRect);
        float panelTop = _systemUpgradeStartPosition.y +
                         _systemUpgradePanel.rect.height *
                         (1f - _systemUpgradePanel.pivot.y);
        float buttonY = panelTop + _buttonMargin;
        float nextButtonX = insets.Left + _buttonMargin;

        foreach (RectTransform button in _leftButtons)
        {
            if (button == null) continue;

            SetBottomLeft(button, new Vector2(nextButtonX, buttonY));
            NormalizeLabels(button);
            nextButtonX += button.rect.width;
        }

        SetBottomRight(
            _rightButton,
            new Vector2(-(insets.Right + _buttonMargin), buttonY));
        NormalizeLabels(_rightButton);
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _canvas != null &&
                             _systemUpgradePanel != null &&
                             _leftButtons != null &&
                             _leftButtons.Length > 0 &&
                             _rightButton != null;
        if (!hasReferences)
        {
            Debug.LogError("하단 기능 버튼 배치의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    private static void SetBottomLeft(RectTransform target, Vector2 bottomLeft)
    {
        if (target == null) return;

        Rect rect = target.rect;
        target.anchoredPosition = bottomLeft + new Vector2(
            rect.width * target.pivot.x,
            rect.height * target.pivot.y);
    }

    private static void SetBottomRight(RectTransform target, Vector2 bottomRight)
    {
        if (target == null) return;

        Rect rect = target.rect;
        target.anchoredPosition = bottomRight + new Vector2(
            -rect.width * (1f - target.pivot.x),
            rect.height * target.pivot.y);
    }

    private void NormalizeLabels(RectTransform button)
    {
        if (button == null) return;

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null || label.name != "NameLabel") continue;

            RectTransform labelRect = label.transform as RectTransform;
            if (labelRect != null)
            {
                labelRect.sizeDelta = _labelSize;
            }

            label.fontSize = _labelFontSize;
        }
    }
}
