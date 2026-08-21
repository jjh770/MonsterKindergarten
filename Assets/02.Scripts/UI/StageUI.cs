using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageUI : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _stageButtonTemplate;
    [SerializeField] private Color _transitionColor = new(0.75f, 0.9f, 1f, 1f);

    private Button _stageButton;
    private TextMeshProUGUI _stageButtonArrow;
    private Image _transitionOverlay;
    private EGameStage _displayedStage = EGameStage.Ground;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _stageButton != null
        ? _stageButton.transform as RectTransform
        : null;

    public event Action ButtonClicked;

    private void Awake()
    {
        if (_canvas == null || _stageButtonTemplate == null)
        {
            Debug.LogError("스테이지 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        CreateTransitionOverlay();
        CreateStageButton();
    }

    private void OnDestroy()
    {
        if (_stageButton == null) return;

        _stageButton.onClick.RemoveListener(OnStageButtonClicked);
        _stageButton.transform.DOKill();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_stageButton != null)
        {
            RefreshStageButtonSafeArea();
        }
    }

    public void SetButtonVisible(bool isVisible, bool animated)
    {
        if (_stageButton == null) return;

        _stageButton.gameObject.SetActive(isVisible);
        _stageButton.transform.DOKill();

        if (!isVisible)
        {
            _stageButton.transform.localScale = Vector3.zero;
            return;
        }

        RefreshStageButtonSafeArea();
        UpdateStageButtonVisual();

        if (!animated)
        {
            _stageButton.transform.localScale = Vector3.one;
            return;
        }

        _stageButton.transform.localScale = Vector3.zero;
        _stageButton.transform.DOScale(Vector3.one, 0.35f)
            .SetEase(Ease.OutBack);
    }

    public void SetButtonInteractable(bool isInteractable)
    {
        if (_stageButton == null) return;

        _stageButton.interactable = isInteractable;
    }

    public void SetStage(EGameStage stage)
    {
        _displayedStage = stage;
        UpdateStageButtonVisual();
    }

    // 전환 시작 시 오버레이를 최상단으로 올리고 입력을 막는다.
    public void BeginOverlay()
    {
        if (_transitionOverlay == null) return;

        _transitionOverlay.transform.SetAsLastSibling();
        _transitionOverlay.raycastTarget = true;
        SetOverlayAlpha(0f);
    }

    public void EndOverlay()
    {
        if (_transitionOverlay == null) return;

        _transitionOverlay.raycastTarget = false;
    }

    // 호출부가 전환 시퀀스에 합성할 수 있도록 트윈을 그대로 돌려준다.
    public Tween FadeOverlay(float alpha, float duration)
    {
        return _transitionOverlay != null
            ? _transitionOverlay.DOFade(alpha, duration)
            : null;
    }

    private void OnStageButtonClicked()
    {
        ButtonClicked?.Invoke();
    }

    private void CreateTransitionOverlay()
    {
        GameObject overlayObject = new GameObject(
            "StageTransitionOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        overlayObject.layer = _canvas.gameObject.layer;
        RectTransform overlayRect = (RectTransform)overlayObject.transform;
        overlayRect.SetParent(_canvas.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        _transitionOverlay = overlayObject.GetComponent<Image>();
        _transitionOverlay.color = _transitionColor;
        _transitionOverlay.raycastTarget = false;
        SetOverlayAlpha(0f);
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color color = _transitionOverlay.color;
        color.a = alpha;
        _transitionOverlay.color = color;
    }

    private void CreateStageButton()
    {
        _stageButton = Instantiate(_stageButtonTemplate, _canvas.transform);
        _stageButton.name = "StageMoveButton";
        _stageButton.onClick.AddListener(OnStageButtonClicked);

        RectTransform buttonRect = (RectTransform)_stageButton.transform;
        buttonRect.localRotation = Quaternion.identity;
        buttonRect.localScale = Vector3.zero;
        buttonRect.anchorMin = Vector2.one;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.pivot = Vector2.one;
        buttonRect.sizeDelta = new Vector2(112f, 112f);

        Image buttonImage = _stageButton.targetGraphic as Image;
        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.18f, 0.42f, 0.65f, 0.95f);
        }

        GameObject arrowObject = new GameObject(
            "Arrow",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        arrowObject.layer = _canvas.gameObject.layer;
        RectTransform arrowRect = (RectTransform)arrowObject.transform;
        arrowRect.SetParent(buttonRect, false);
        arrowRect.anchorMin = Vector2.zero;
        arrowRect.anchorMax = Vector2.one;
        arrowRect.offsetMin = Vector2.zero;
        arrowRect.offsetMax = Vector2.zero;

        _stageButtonArrow = arrowObject.GetComponent<TextMeshProUGUI>();
        _stageButtonArrow.alignment = TextAlignmentOptions.Center;
        _stageButtonArrow.fontSize = 56f;
        _stageButtonArrow.color = Color.white;
        _stageButtonArrow.raycastTarget = false;

        RefreshStageButtonSafeArea();
        UpdateStageButtonVisual();
        _stageButton.gameObject.SetActive(false);
    }

    private void UpdateStageButtonVisual()
    {
        if (_stageButtonArrow == null) return;

        _stageButtonArrow.text = _displayedStage == EGameStage.Ground
            ? "↑"
            : "↓";
    }

    private void RefreshStageButtonSafeArea()
    {
        if (_stageButton == null || _canvas == null) return;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        RectTransform buttonRect = _stageButton.transform as RectTransform;
        if (canvasRect == null || buttonRect == null) return;

        float rightInset = GetCanvasInset(
            Screen.width - Screen.safeArea.xMax,
            Screen.width,
            canvasRect.rect.width);
        float topInset = GetCanvasInset(
            Screen.height - Screen.safeArea.yMax,
            Screen.height,
            canvasRect.rect.height);
        buttonRect.anchoredPosition = new Vector2(
            -rightInset - 28f,
            -topInset - 28f);
    }

    private static float GetCanvasInset(
        float pixelInset,
        int screenSize,
        float canvasSize)
    {
        if (screenSize <= 0 || canvasSize <= 0f) return 0f;

        return Mathf.Max(0f, pixelInset / screenSize * canvasSize);
    }
}
