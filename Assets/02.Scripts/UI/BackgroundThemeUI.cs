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

    [Tooltip("지금 쓰고 있는 배경 버튼을 얼마나 흐리게 할지입니다.")]
    [SerializeField, Range(0.1f, 1f)] private float _currentThemeAlpha = 0.45f;

    private bool _isButtonAvailable;
    private bool _isMenuPresentationRequested;

    // 그려 둔 투명도를 그대로 두고 배율만 곱한다. 1로 덮어쓰면 반투명하게 디자인한
    // 그래픽이 선택될 때마다 불투명해진다.
    private Graphic[] _groundThemeGraphics;
    private Graphic[] _skyThemeGraphics;
    private float[] _groundThemeAlphas;
    private float[] _skyThemeAlphas;

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

        CacheThemeGraphics(
            _groundThemeButton,
            out _groundThemeGraphics,
            out _groundThemeAlphas);
        CacheThemeGraphics(
            _skyThemeButton,
            out _skyThemeGraphics,
            out _skyThemeAlphas);
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

    // 지금 쓰고 있는 배경을 알려 준다. 쓰고 있는 쪽은 눌러도 바뀔 것이 없으므로
    // 흐리게 두고, 고를 수 있는 쪽을 선명하게 남긴다. 누름 자체는
    // GameplaySpaceManager가 막으므로 여기서는 보이기만 맡는다.
    public void SetSelectedTheme(EBackgroundTheme theme)
    {
        bool isGround = theme == EBackgroundTheme.Ground;
        ApplyThemeAlpha(
            _groundThemeGraphics,
            _groundThemeAlphas,
            isGround ? _currentThemeAlpha : 1f);
        ApplyThemeAlpha(
            _skyThemeGraphics,
            _skyThemeAlphas,
            isGround ? 1f : _currentThemeAlpha);
    }

    private static void CacheThemeGraphics(
        Button button,
        out Graphic[] graphics,
        out float[] alphas)
    {
        graphics = button != null
            ? button.GetComponentsInChildren<Graphic>(true)
            : Array.Empty<Graphic>();
        alphas = new float[graphics.Length];
        for (int i = 0; i < graphics.Length; ++i)
        {
            alphas[i] = graphics[i] != null ? graphics[i].color.a : 1f;
        }
    }

    private static void ApplyThemeAlpha(
        Graphic[] graphics,
        float[] alphas,
        float multiplier)
    {
        if (graphics == null || alphas == null) return;

        for (int i = 0; i < graphics.Length; ++i)
        {
            if (graphics[i] == null) continue;

            Color color = graphics[i].color;
            color.a = alphas[i] * multiplier;
            graphics[i].color = color;
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
