using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CollectionBookEntryUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform _visualRoot;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField, Min(0f)] private float _selectedLift = 18f;
    [SerializeField] private float _collapsedNumberY = -85f;
    [SerializeField] private float _selectedNumberY = -22f;
    [SerializeField, Min(0f)] private float _selectionDuration = 0.18f;

    private static readonly Color LockedIconColor =
        new(0.08f, 0.08f, 0.1f, 0.92f);
    private static readonly Color NormalBackgroundColor =
        new(1f, 1f, 1f, 1f);
    private static readonly Color SelectedBackgroundColor =
        new(1f, 0.83f, 0.35f, 1f);

    private Action _clicked;
    private Sequence _selectionTween;
    private Color _boundIconColor = Color.white;
    private bool _hasSelectionState;
    private bool _isSelected;

    public RectTransform RectTransform => transform as RectTransform;

    private void OnDestroy()
    {
        _selectionTween?.Kill();
    }

    public void Bind(
        ESlimeGrade grade,
        SlimeSpecData specData,
        bool isRegistered,
        Action clicked)
    {
        _clicked = clicked;
        _icon.sprite = specData?.Sprite;
        _boundIconColor = isRegistered ? Color.white : LockedIconColor;
        if (_isSelected)
        {
            _icon.color = _boundIconColor;
        }

        _numberText.text = isRegistered
            ? $"No.{(int)grade:00}"
            : "No.??";
    }

    public void SetSelected(bool selected)
    {
        if (!_hasSelectionState)
        {
            _hasSelectionState = true;
            _isSelected = selected;
            ApplySelectionImmediately(selected);
            return;
        }

        if (_isSelected == selected) return;

        _isSelected = selected;
        PlaySelection(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _clicked?.Invoke();
        }
    }

    private void ApplySelectionImmediately(bool selected)
    {
        _selectionTween?.Kill();
        _visualRoot.anchoredPosition = selected
            ? Vector2.up * _selectedLift
            : Vector2.zero;
        SetNumberY(selected ? _selectedNumberY : _collapsedNumberY);
        _background.color = selected
            ? SelectedBackgroundColor
            : NormalBackgroundColor;
        _icon.gameObject.SetActive(selected);
        _icon.transform.localScale = selected
            ? Vector3.one * 1.3f
            : Vector3.one * 0.85f;
        _icon.color = selected
            ? _boundIconColor
            : WithAlpha(_boundIconColor, 0f);
    }

    private void PlaySelection(bool selected)
    {
        _selectionTween?.Kill();

        if (selected)
        {
            _icon.gameObject.SetActive(true);
            _icon.transform.localScale = Vector3.one * 0.85f;
            _icon.color = WithAlpha(_boundIconColor, 0f);
        }

        _selectionTween = DOTween.Sequence()
            .Join(_visualRoot.DOAnchorPosY(
                selected ? _selectedLift : 0f,
                _selectionDuration))
            .Join(_numberText.rectTransform.DOAnchorPosY(
                selected ? _selectedNumberY : _collapsedNumberY,
                _selectionDuration))
            .Join(_background.DOColor(
                selected ? SelectedBackgroundColor : NormalBackgroundColor,
                _selectionDuration))
            .Join(_icon.transform.DOScale(
                selected ? 1.3f : 0.85f,
                _selectionDuration))
            .Join(_icon.DOColor(
                selected
                    ? _boundIconColor
                    : WithAlpha(_boundIconColor, 0f),
                _selectionDuration))
            .OnComplete(() =>
            {
                _selectionTween = null;
                if (!_isSelected)
                {
                    _icon.gameObject.SetActive(false);
                }
            });
    }

    private void SetNumberY(float y)
    {
        Vector2 position = _numberText.rectTransform.anchoredPosition;
        position.y = y;
        _numberText.rectTransform.anchoredPosition = position;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
