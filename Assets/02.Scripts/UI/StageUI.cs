using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageUI : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _stageButton;
    [SerializeField] private TextMeshProUGUI _stageButtonArrow;
    [SerializeField] private Image _transitionOverlay;

    // Safe Area 안쪽으로 추가 확보할 여백. anchoredPosition은 코드가 계산하므로
    // 씬에서 버튼을 끌어 옮길 수 없고, 이 값으로 조정한다.
    [SerializeField, Min(0f)] private float _buttonMargin = 50f;

    private EGameStage _displayedStage = EGameStage.Ground;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _stageButton != null
        ? _stageButton.transform as RectTransform
        : null;

    public event Action ButtonClicked;

    private void Awake()
    {
        if (_canvas == null ||
            _stageButton == null ||
            _stageButtonArrow == null ||
            _transitionOverlay == null)
        {
            Debug.LogError("스테이지 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _stageButton.onClick.AddListener(OnStageButtonClicked);
        _transitionOverlay.raycastTarget = false;
        SetOverlayAlpha(0f);

        RefreshStageButtonSafeArea();
        UpdateStageButtonVisual();
        _stageButton.gameObject.SetActive(false);
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

    private void SetOverlayAlpha(float alpha)
    {
        Color color = _transitionOverlay.color;
        color.a = alpha;
        _transitionOverlay.color = color;
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
            -rightInset - _buttonMargin,
            -topInset - _buttonMargin);
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
