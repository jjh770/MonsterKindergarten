using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class BackgroundThemeUI : MonoBehaviour
{
    [Serializable]
    private sealed class ThemeButtonBinding
    {
        [SerializeField] private EBackgroundTheme _theme;
        [SerializeField] private Button _button;

        public EBackgroundTheme Theme => _theme;
        public Button Button => _button;
    }

    private sealed class ThemeButtonRuntime
    {
        public EBackgroundTheme Theme { get; }
        public Button Button { get; }
        public Graphic[] Graphics { get; }
        public float[] Alphas { get; }
        public UnityAction ClickListener { get; }

        public ThemeButtonRuntime(
            EBackgroundTheme theme,
            Button button,
            Graphic[] graphics,
            float[] alphas,
            UnityAction clickListener)
        {
            Theme = theme;
            Button = button;
            Graphics = graphics;
            Alphas = alphas;
            ClickListener = clickListener;
        }
    }

    [FormerlySerializedAs("_stageButton")]
    [SerializeField] private Button _backgroundButton;
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;
    [SerializeField] private Button _groundThemeButton;
    [SerializeField] private Button _skyThemeButton;
    [Tooltip("Ground와 Sky 이외에 추가할 테마의 선택 버튼을 연결합니다.")]
    [SerializeField] private ThemeButtonBinding[] _additionalThemeButtons =
        Array.Empty<ThemeButtonBinding>();

    [Tooltip("지금 쓰고 있는 배경 버튼을 얼마나 흐리게 할지입니다.")]
    [SerializeField, Range(0.1f, 1f)] private float _currentThemeAlpha = 0.45f;

    private bool _isButtonAvailable;
    private bool _isMenuPresentationRequested;

    // 그려 둔 투명도를 그대로 두고 배율만 곱한다. 1로 덮어쓰면 반투명하게 디자인한
    // 그래픽이 선택될 때마다 불투명해진다.
    private readonly List<ThemeButtonRuntime> _themeButtons = new();

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
        _backgroundButton.gameObject.SetActive(false);

        RegisterThemeButton(EBackgroundTheme.Ground, _groundThemeButton);
        RegisterThemeButton(EBackgroundTheme.Sky, _skyThemeButton);
        if (_additionalThemeButtons != null)
        {
            foreach (ThemeButtonBinding binding in _additionalThemeButtons)
            {
                if (binding == null) continue;
                RegisterThemeButton(binding.Theme, binding.Button);
            }
        }
    }

    private void Start()
    {
        if (!enabled) return;

        SlimeManager.OnBackgroundThemesChanged += RefreshOwnership;
        SlimeManager.OnDataInitialized += RefreshOwnership;
        RefreshOwnership();
    }

    // 상점에서 산 테마만 고를 수 있게 한다. 땅과 하늘은 해금이 주는 기본 테마라
    // 늘 남는다. 데이터가 준비되기 전에는 기본 테마만 보인다.
    private void RefreshOwnership()
    {
        SlimeManager manager = SlimeManager.Instance;
        foreach (ThemeButtonRuntime runtime in _themeButtons)
        {
            if (runtime.Button == null) continue;

            bool isOwned = BackgroundThemeRules.IsFree(runtime.Theme) ||
                           (manager != null &&
                            manager.IsBackgroundThemeOwned(runtime.Theme));
            runtime.Button.gameObject.SetActive(isOwned);
        }
    }

    private void OnDestroy()
    {
        SlimeManager.OnBackgroundThemesChanged -= RefreshOwnership;
        SlimeManager.OnDataInitialized -= RefreshOwnership;

        if (_backgroundButton != null)
        {
            _backgroundButton.onClick.RemoveListener(OnBackgroundButtonClicked);
            _backgroundButton.transform.DOKill();
        }

        foreach (ThemeButtonRuntime runtime in _themeButtons)
        {
            if (runtime.Button != null)
            {
                runtime.Button.onClick.RemoveListener(runtime.ClickListener);
            }
        }

        _themeButtons.Clear();
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
        foreach (ThemeButtonRuntime runtime in _themeButtons)
        {
            if (runtime.Button != null)
            {
                runtime.Button.interactable = isInteractable;
            }
        }

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
        foreach (ThemeButtonRuntime runtime in _themeButtons)
        {
            ApplyThemeAlpha(
                runtime.Graphics,
                runtime.Alphas,
                runtime.Theme == theme ? _currentThemeAlpha : 1f);
        }
    }

    private void RegisterThemeButton(EBackgroundTheme theme, Button button)
    {
        if (!BackgroundThemeRules.IsValid(theme) || button == null)
        {
            Debug.LogError($"배경 테마 버튼 연결이 올바르지 않습니다. : {theme}", this);
            return;
        }

        foreach (ThemeButtonRuntime registered in _themeButtons)
        {
            if (registered.Theme != theme) continue;

            Debug.LogError($"배경 테마 버튼이 중복 연결되어 있습니다. : {theme}", this);
            return;
        }

        CacheThemeGraphics(button, out Graphic[] graphics, out float[] alphas);
        UnityAction clickListener = () => SelectTheme(theme);
        button.onClick.AddListener(clickListener);
        _themeButtons.Add(new ThemeButtonRuntime(
            theme,
            button,
            graphics,
            alphas,
            clickListener));
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
