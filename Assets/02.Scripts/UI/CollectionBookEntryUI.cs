using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CollectionBookEntryUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private GameObject _availableBadge;

    private static readonly Color LockedIconColor =
        new(0.08f, 0.08f, 0.1f, 0.92f);
    private static readonly Color NormalBackgroundColor =
        new(0.78f, 0.68f, 0.51f, 1f);
    private static readonly Color SelectedBackgroundColor =
        new(1f, 0.83f, 0.35f, 1f);

    private Tween _revealTween;
    private Action _clicked;

    public RectTransform RectTransform => transform as RectTransform;

    private void Awake()
    {
        _button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        _revealTween?.Kill();
        _button?.onClick.RemoveListener(OnClicked);
    }

    public void Bind(
        ESlimeGrade grade,
        SlimeSpecData specData,
        bool isRegistered,
        bool canRegister,
        Action clicked)
    {
        _revealTween?.Kill();
        _revealTween = null;
        _icon.transform.localScale = Vector3.one;
        _clicked = clicked;
        _icon.sprite = specData?.Sprite;
        _icon.color = isRegistered ? Color.white : LockedIconColor;
        _numberText.text = isRegistered
            ? $"No.{(int)grade:00}"
            : "No.??";
        _nameText.text = isRegistered
            ? specData?.Name ?? string.Empty
            : "??? 슬라임";
        _availableBadge.SetActive(!isRegistered && canRegister);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        _background.color = selected
            ? SelectedBackgroundColor
            : NormalBackgroundColor;
    }

    public void PlayReveal()
    {
        _revealTween?.Kill();
        _icon.color = LockedIconColor;
        _icon.transform.localScale = Vector3.one * 0.85f;
        _revealTween = DOTween.Sequence()
            .Join(_icon.DOColor(Color.white, 0.35f))
            .Join(_icon.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .OnComplete(() => _revealTween = null);
    }

    private void OnClicked()
    {
        _clicked?.Invoke();
    }
}
