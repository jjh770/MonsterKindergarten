using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageUI : MonoBehaviour
{
    [SerializeField] private Button _stageButton;

    private bool _isButtonAvailable;
    private bool _isMenuPresentationRequested;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _stageButton != null
        ? _stageButton.transform as RectTransform
        : null;
    public event Action ButtonClicked;

    private void Awake()
    {
        if (_stageButton == null)
        {
            Debug.LogError("스테이지 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _stageButton.onClick.AddListener(OnStageButtonClicked);
        _stageButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_stageButton == null) return;

        _stageButton.onClick.RemoveListener(OnStageButtonClicked);
        _stageButton.transform.DOKill();
    }

    public void SetButtonVisible(bool isVisible)
    {
        if (_stageButton == null) return;

        _isButtonAvailable = isVisible;
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

    private void OnStageButtonClicked()
    {
        ButtonClicked?.Invoke();
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
