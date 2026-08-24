using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DialoguePlacement
{
    Bottom,
    Top,
}

[RequireComponent(typeof(Image))]
public sealed class TutorialDialogueView : MonoBehaviour
{
    [SerializeField] private RectTransform _dialoguePanel;
    [SerializeField] private TextMeshProUGUI _dialogueText;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Vector2 _bottomPanelPosition = new Vector2(0f, 270f);
    [SerializeField] private Vector2 _topPanelPosition = new Vector2(0f, -270f);

    public event Action NextRequested;

    private Image _backgroundImage;
    private Color _backgroundColor;

    private void Awake()
    {
        _backgroundImage = GetComponent<Image>();
        _backgroundColor = _backgroundImage.color;

        if (_dialoguePanel == null || _dialogueText == null || _nextButton == null)
        {
            Debug.LogError("대화창 프리팹의 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _nextButton.onClick.AddListener(OnNextButtonClicked);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveListener(OnNextButtonClicked);
        }
    }

    public void Show(
        string speaker,
        string message,
        bool dimBackground = true,
        DialoguePlacement placement = DialoguePlacement.Bottom)
    {
        SetPlacement(placement);
        Color backgroundColor = _backgroundColor;
        backgroundColor.a = dimBackground ? _backgroundColor.a : 0f;
        _backgroundImage.color = backgroundColor;
        _dialogueText.text = $"<color=#FFD12E><b>{speaker}</b></color>\n{message}";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _nextButton.Select();
    }

    private void SetPlacement(DialoguePlacement placement)
    {
        bool placeAtTop = placement == DialoguePlacement.Top;
        float anchorY = placeAtTop ? 1f : 0f;
        _dialoguePanel.anchorMin = new Vector2(0f, anchorY);
        _dialoguePanel.anchorMax = new Vector2(1f, anchorY);
        _dialoguePanel.anchoredPosition = placeAtTop
            ? _topPanelPosition
            : _bottomPanelPosition;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnNextButtonClicked()
    {
        NextRequested?.Invoke();
    }
}
