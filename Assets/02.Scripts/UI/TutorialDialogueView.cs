using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DialoguePlacement
{
    Bottom,
    Top,
    // 지금 비추는 구멍 바로 위(구멍이 위쪽에 있으면 바로 아래)에 놓는다. 구멍이 없으면 아래쪽과 같다.
    NearSpotlight,
}

[RequireComponent(typeof(Image))]
public sealed class TutorialDialogueView : MonoBehaviour
{
    [SerializeField] private RectTransform _dialoguePanel;
    [SerializeField] private TextMeshProUGUI _dialogueText;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Vector2 _bottomPanelPosition = new Vector2(0f, 270f);
    [SerializeField] private Vector2 _topPanelPosition = new Vector2(0f, -270f);
    [Tooltip("NearSpotlight일 때 구멍과 안내창 사이에 남길 간격입니다. 구멍 테두리의 발광이 닿지 않을 만큼 둡니다.")]
    [SerializeField, Min(0f)] private float _spotlightGap = 56f;
    [Tooltip("안내창이 화면 위아래 끝에서 떨어져야 하는 거리입니다.")]
    [SerializeField, Min(0f)] private float _screenMargin = 24f;

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
        DialoguePlacement placement = DialoguePlacement.Bottom,
        Rect? spotlightWorldRect = null)
    {
        SetPlacement(placement, spotlightWorldRect);
        Color backgroundColor = _backgroundColor;
        backgroundColor.a = dimBackground ? _backgroundColor.a : 0f;
        _backgroundImage.color = backgroundColor;
        _dialogueText.text = $"<size=60><color=#875026><b>{speaker}</b></color></size>\n{message}";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _nextButton.Select();
    }

    private void SetPlacement(DialoguePlacement placement, Rect? spotlightWorldRect)
    {
        if (placement == DialoguePlacement.NearSpotlight && spotlightWorldRect.HasValue)
        {
            PlaceNearSpotlight(spotlightWorldRect.Value);
            return;
        }

        bool placeAtTop = placement == DialoguePlacement.Top;
        float anchorY = placeAtTop ? 1f : 0f;
        _dialoguePanel.anchorMin = new Vector2(0f, anchorY);
        _dialoguePanel.anchorMax = new Vector2(1f, anchorY);
        _dialoguePanel.anchoredPosition = placeAtTop
            ? _topPanelPosition
            : _bottomPanelPosition;
    }

    // 구멍이 화면 아래쪽에 있으면 구멍 위에, 위쪽에 있으면 구멍 아래에 놓는다. 어느 쪽이든 안내창이
    // 구멍을 가리지 않고, 화면 밖으로 나가지 않도록 위아래 끝에서 잘라 준다.
    private void PlaceNearSpotlight(Rect spotlightWorldRect)
    {
        var parent = (RectTransform)_dialoguePanel.parent;
        Vector3 holeMin = parent.InverseTransformPoint(
            new Vector3(spotlightWorldRect.xMin, spotlightWorldRect.yMin, 0f));
        Vector3 holeMax = parent.InverseTransformPoint(
            new Vector3(spotlightWorldRect.xMax, spotlightWorldRect.yMax, 0f));

        Rect parentRect = parent.rect;
        float height = _dialoguePanel.rect.height;
        float pivotY = _dialoguePanel.pivot.y;
        bool placeAbove = (holeMin.y + holeMax.y) * 0.5f <= parentRect.center.y;

        float bottomEdge = placeAbove
            ? holeMax.y + _spotlightGap
            : holeMin.y - _spotlightGap - height;
        bottomEdge = Mathf.Clamp(
            bottomEdge,
            parentRect.yMin + _screenMargin,
            parentRect.yMax - _screenMargin - height);

        _dialoguePanel.anchorMin = new Vector2(0f, 0f);
        _dialoguePanel.anchorMax = new Vector2(1f, 0f);
        _dialoguePanel.anchoredPosition = new Vector2(
            _bottomPanelPosition.x,
            bottomEdge - parentRect.yMin + height * pivotY);
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
