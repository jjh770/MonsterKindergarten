using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class BackgroundThemeUI : MonoBehaviour
{
    [FormerlySerializedAs("_stageButton")]
    [SerializeField] private Button _backgroundButton;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private Button _groundThemeButton;
    [SerializeField] private Button _skyThemeButton;

    private bool _isButtonAvailable;
    private bool _isMenuPresentationRequested;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _backgroundButton != null
        ? _backgroundButton.transform as RectTransform
        : null;
    public event Action ButtonClicked;
    public event Action<EBackgroundTheme> ThemeSelected;

    private void Awake()
    {
        if (_backgroundButton == null ||
            _panelSwitcher == null ||
            _groundThemeButton == null ||
            _skyThemeButton == null)
        {
            Debug.LogError("배경 테마 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _backgroundButton.onClick.AddListener(OnBackgroundButtonClicked);
        _groundThemeButton.onClick.AddListener(OnGroundThemeButtonClicked);
        _skyThemeButton.onClick.AddListener(OnSkyThemeButtonClicked);
        _backgroundButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_backgroundButton != null)
        {
            _backgroundButton.onClick.RemoveListener(OnBackgroundButtonClicked);
            _backgroundButton.transform.DOKill();
        }

        if (_groundThemeButton != null)
        {
            _groundThemeButton.onClick.RemoveListener(OnGroundThemeButtonClicked);
        }

        if (_skyThemeButton != null)
        {
            _skyThemeButton.onClick.RemoveListener(OnSkyThemeButtonClicked);
        }
    }

    public void SetButtonVisible(bool isVisible)
    {
        if (_backgroundButton == null) return;

        _isButtonAvailable = isVisible;
        ApplyButtonPresentation();
    }

    // 버튼의 해금 여부는 GameplaySpaceManager가, 메뉴 안 실제 노출은 DisplayRoomUI가 맡는다.
    public void SetMenuPresentation(bool isVisible)
    {
        if (_backgroundButton == null) return;

        _isMenuPresentationRequested = isVisible;
        ApplyButtonPresentation();
    }

    public void SetButtonInteractable(bool isInteractable)
    {
        if (_backgroundButton == null) return;

        _backgroundButton.interactable = isInteractable;
        _groundThemeButton.interactable = isInteractable;
        _skyThemeButton.interactable = isInteractable;
        if (!isInteractable)
        {
            _panelSwitcher.TryHideBackgroundThemePanel(animated: false);
        }
    }

    public void ToggleThemeSelector()
    {
        if (!_backgroundButton.interactable ||
            !_backgroundButton.gameObject.activeInHierarchy)
        {
            return;
        }

        _panelSwitcher.TryShowBackgroundThemePanel();
    }

    public void CloseThemeSelector()
    {
        _panelSwitcher?.TryHideBackgroundThemePanel();
    }

    private void OnBackgroundButtonClicked()
    {
        ButtonClicked?.Invoke();
    }

    private void OnGroundThemeButtonClicked()
    {
        SelectTheme(EBackgroundTheme.Ground);
    }

    private void OnSkyThemeButtonClicked()
    {
        SelectTheme(EBackgroundTheme.Sky);
    }

    private void SelectTheme(EBackgroundTheme theme)
    {
        CloseThemeSelector();
        ThemeSelected?.Invoke(theme);
    }

    private void ApplyButtonPresentation()
    {
        bool shouldShow = _isButtonAvailable &&
                          _isMenuPresentationRequested;
        _backgroundButton.gameObject.SetActive(shouldShow);
        _backgroundButton.transform.DOKill();
        _backgroundButton.transform.localScale = shouldShow
            ? Vector3.one
            : Vector3.zero;
    }

}
