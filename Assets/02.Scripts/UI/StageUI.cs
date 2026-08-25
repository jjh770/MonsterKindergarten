using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageUI : MonoBehaviour
{
    [SerializeField] private Button _stageButton;
    [SerializeField] private TextMeshProUGUI _stageButtonArrow;
    [SerializeField] private Image _transitionOverlay;

    private EGameStage _displayedStage = EGameStage.Ground;
    private bool _isButtonAvailable;
    private bool _isMenuPresentationRequested;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _stageButton != null
        ? _stageButton.transform as RectTransform
        : null;
    public bool IsButtonAvailable => _isButtonAvailable;

    public event Action ButtonClicked;

    private void Awake()
    {
        if (_stageButton == null ||
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

        UpdateStageButtonVisual();
        _stageButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_stageButton == null) return;

        _stageButton.onClick.RemoveListener(OnStageButtonClicked);
        _stageButton.transform.DOKill();
    }

    public void SetButtonVisible(bool isVisible, bool animated)
    {
        if (_stageButton == null) return;

        _isButtonAvailable = isVisible;
        UpdateStageButtonVisual();
        ApplyButtonPresentation();
    }

    // 버튼의 해금 여부는 StageManager가, 메뉴 안 실제 노출은 DisplayRoomUI가 맡는다.
    public void SetMenuPresentation(bool isVisible)
    {
        if (_stageButton == null) return;

        _isMenuPresentationRequested = isVisible;
        ApplyButtonPresentation();
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

    private void ApplyButtonPresentation()
    {
        bool shouldShow = _isButtonAvailable &&
                          _isMenuPresentationRequested;
        _stageButton.gameObject.SetActive(shouldShow);
        _stageButton.transform.DOKill();
        _stageButton.transform.localScale = shouldShow
            ? Vector3.one
            : Vector3.zero;
    }

}
