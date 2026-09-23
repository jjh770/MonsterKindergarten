using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class BackgroundMove : MonoBehaviour
{
    [Serializable]
    private sealed class ThemeBinding
    {
        [SerializeField] private EBackgroundTheme _theme;
        [SerializeField] private Sprite[] _backgrounds;
        [SerializeField] private GameObject _root;
        [SerializeField] private SpriteRenderer[] _tiles;

        public EBackgroundTheme Theme => _theme;
        public Sprite[] Backgrounds => _backgrounds;
        public GameObject Root => _root;
        public SpriteRenderer[] Tiles => _tiles;
    }

    [SerializeField] private Sprite[] _groundBackgrounds;
    [SerializeField] private Sprite[] _skyBackgrounds;
    [Tooltip("Ground와 Sky 이외에 추가할 테마의 배경, 루트, 타일을 연결합니다.")]
    [SerializeField] private ThemeBinding[] _additionalThemes = Array.Empty<ThemeBinding>();
    [SerializeField] private Sprite[] _displayRoomBackgrounds;
    [FormerlySerializedAs("_groundStageRoot")]
    [SerializeField] private GameObject _groundThemeRoot;
    [FormerlySerializedAs("_skyStageRoot")]
    [SerializeField] private GameObject _skyThemeRoot;
    [SerializeField] private GameObject _displayRoomRoot;
    [SerializeField] private SpriteRenderer[] _groundTiles;
    [SerializeField] private SpriteRenderer[] _skyTiles;
    [SerializeField] private SpriteRenderer[] _displayRoomTiles;
    [SerializeField, Min(0.1f)] private float _duration = 60f;
    [SerializeField, Min(0f)] private float _verticalOverscan = 0.1f;
    [SerializeField, Range(0f, 2f)] private float _seamOverlapPixels = 1f;

    private SpriteRenderer[] _activeTiles;
    private GameplaySpaceManager _spaceManager;
    private EBackgroundTheme _currentTheme = EBackgroundTheme.Ground;
    private EGameplaySpace _currentSpace = EGameplaySpace.MainField;
    private float _tileWidth;
    private float _tileSpacing;
    private float _tileOriginX;
    private float _cameraCenterX;
    private float _cameraCenterY;
    private float _cameraHeight;

    // 두 테마 루트 전체를 함께 흐리게 바꾼다. 타일만 페이드하면 루트 아래의
    // 장식 스프라이트가 순간적으로 나타나므로 모든 SpriteRenderer를 잡아 둔다.
    private Sequence _themeTransitionSequence;
    private SpriteRenderer[] _outgoingThemeRenderers;
    private SpriteRenderer[] _incomingThemeRenderers;
    private Color[] _outgoingThemeColors;
    private Color[] _incomingThemeColors;

    // 한 배경을 깔기 위해 필요한 값 묶음. 들어오는 배경을 미리 깔아 두었다가
    // 연출이 끝난 뒤에 현재 값으로 넘기려고 따로 담는다.
    private readonly struct TileLayout
    {
        public Sprite Sprite { get; }
        public float ScaleFactor { get; }
        public float Width { get; }
        public float Spacing { get; }
        public float OriginX { get; }
        public float OriginY { get; }

        public TileLayout(
            Sprite sprite,
            float scaleFactor,
            float width,
            float spacing,
            float originX,
            float originY)
        {
            Sprite = sprite;
            ScaleFactor = scaleFactor;
            Width = width;
            Spacing = spacing;
            OriginX = originX;
            OriginY = originY;
        }
    }

    private void Start()
    {
        if (_groundBackgrounds == null ||
            _groundBackgrounds.Length == 0 ||
            _skyBackgrounds == null ||
            _skyBackgrounds.Length == 0 ||
            _displayRoomBackgrounds == null ||
            _displayRoomBackgrounds.Length == 0 ||
            _groundThemeRoot == null ||
            _skyThemeRoot == null ||
            _displayRoomRoot == null ||
            !HasTwoTiles(_groundTiles) ||
            !HasTwoTiles(_skyTiles) ||
            !HasTwoTiles(_displayRoomTiles) ||
            !AreAdditionalThemesValid())
        {
            Debug.LogError("배경 테마 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        Camera mainCamera = Camera.main;
        _cameraCenterX = mainCamera != null
            ? mainCamera.transform.position.x
            : transform.position.x;
        _cameraCenterY = mainCamera != null
            ? mainCamera.transform.position.y
            : transform.position.y;
        _cameraHeight = mainCamera != null && mainCamera.orthographic
            ? mainCamera.orthographicSize * 2f
            : 0f;

        _spaceManager = GameplaySpaceManager.Instance;
        if (_spaceManager != null)
        {
            _spaceManager.BackgroundThemeChanged += ApplyTheme;
            _spaceManager.SpaceChanged += ApplySpace;
            _currentTheme = _spaceManager.CurrentBackgroundTheme;
            _currentSpace = _spaceManager.CurrentSpace;
        }

        ApplyBackground();
    }

    private void OnDestroy()
    {
        CancelThemeTransition();
        if (_spaceManager != null)
        {
            _spaceManager.BackgroundThemeChanged -= ApplyTheme;
            _spaceManager.SpaceChanged -= ApplySpace;
        }
    }

    private void Update()
    {
        if (!HasTwoTiles(_activeTiles)) return;

        float movement = _tileSpacing / _duration * Time.deltaTime;

        foreach (SpriteRenderer tileRenderer in _activeTiles)
        {
            Transform tile = tileRenderer.transform;
            tile.position += Vector3.left * movement;

            if (tile.position.x <= _tileOriginX - _tileSpacing)
            {
                tile.position += Vector3.right * (_tileSpacing * _activeTiles.Length);
            }
        }
    }

    // 두 배경을 같은 자리에서 부드럽게 교차시킨다. 슬라임은 배경 루트 밖에 있어
    // 그대로 남으므로 장소 이동이 아니라 환경만 바뀌는 것으로 읽힌다.
    // 장식장을 보는 중이거나 유효한 지속 시간이 없으면 호출부의 화면 페이드로 넘긴다.
    public bool TryPlayThemeDissolve(
        EBackgroundTheme targetTheme,
        float duration,
        Action onCompleted)
    {
        if (!enabled || _themeTransitionSequence != null) return false;
        if (_currentSpace != EGameplaySpace.MainField) return false;
        if (targetTheme == _currentTheme) return false;
        if (duration <= 0f) return false;

        if (!TryGetThemeBinding(
                targetTheme,
                out Sprite[] incomingBackgrounds,
                out SpriteRenderer[] incomingTiles,
                out GameObject incomingRoot) ||
            !TryGetThemeBinding(
                _currentTheme,
                out _,
                out _,
                out GameObject outgoingRoot))
        {
            return false;
        }

        if (!TryBuildLayout(
                incomingBackgrounds,
                incomingTiles,
                out TileLayout layout))
        {
            return false;
        }

        ApplyLayout(incomingTiles, layout);
        incomingRoot.SetActive(true);

        _outgoingThemeRenderers =
            outgoingRoot.GetComponentsInChildren<SpriteRenderer>(true);
        _incomingThemeRenderers =
            incomingRoot.GetComponentsInChildren<SpriteRenderer>(true);
        _outgoingThemeColors = CaptureColors(_outgoingThemeRenderers);
        _incomingThemeColors = CaptureColors(_incomingThemeRenderers);
        ApplyAlpha(_outgoingThemeRenderers, _outgoingThemeColors, 1f);
        ApplyAlpha(_incomingThemeRenderers, _incomingThemeColors, 0f);

        // 들어오는 배경은 짧은 전환 동안 멈춘다. 평소 한 바퀴가 분 단위라 눈에 띄지
        // 않고, 두 타일의 위상이 어긋나 틈이 생기는 위험도 피할 수 있다.
        _themeTransitionSequence = DOTween.Sequence();
        _themeTransitionSequence.Append(
            DOVirtual.Float(0f, 1f, duration, progress =>
            {
                ApplyAlpha(
                    _outgoingThemeRenderers,
                    _outgoingThemeColors,
                    1f - progress);
                ApplyAlpha(
                    _incomingThemeRenderers,
                    _incomingThemeColors,
                    progress);
            }).SetEase(Ease.InOutSine));
        _themeTransitionSequence.OnComplete(() =>
        {
            RestoreThemeRendererColors();
            _themeTransitionSequence = null;
            outgoingRoot.SetActive(false);
            _currentTheme = targetTheme;
            _activeTiles = incomingTiles;
            CommitLayout(layout);
            onCompleted?.Invoke();
        });
        return true;
    }

    private void ApplyTheme(EBackgroundTheme theme)
    {
        _currentTheme = theme;

        // 디졸브가 이미 이 배경을 깔아 두었다. 여기서 다시 깔면 전환이 끊긴다.
        if (_themeTransitionSequence != null) return;

        ApplyBackground();
    }

    private void ApplySpace(EGameplaySpace space)
    {
        if (_currentSpace == space && _activeTiles != null) return;

        _currentSpace = space;
        ApplyBackground();
    }

    private void ApplyBackground()
    {
        CancelThemeTransition();

        SetAllThemeRootsActive(false);
        _displayRoomRoot.SetActive(false);

        Sprite[] backgrounds;
        GameObject activeRoot;
        if (_currentSpace == EGameplaySpace.DisplayRoom)
        {
            backgrounds = _displayRoomBackgrounds;
            _activeTiles = _displayRoomTiles;
            activeRoot = _displayRoomRoot;
        }
        else if (!TryGetThemeBinding(
                     _currentTheme,
                     out backgrounds,
                     out _activeTiles,
                     out activeRoot))
        {
            Debug.LogError($"연결되지 않은 배경 테마입니다. : {_currentTheme}", this);
            return;
        }

        activeRoot.SetActive(true);

        if (!TryBuildLayout(backgrounds, _activeTiles, out TileLayout layout)) return;

        ApplyLayout(_activeTiles, layout);
        CommitLayout(layout);
    }

    // 전환 중 장식장으로 넘어가는 것처럼 배경을 다시 깔아야 하면 투명도를 원복한 뒤
    // ApplyBackground가 현재 공간의 루트만 다시 고른다.
    private void CancelThemeTransition()
    {
        if (_themeTransitionSequence == null) return;

        _themeTransitionSequence.Kill();
        _themeTransitionSequence = null;
        RestoreThemeRendererColors();
    }

    private bool TryBuildLayout(
        Sprite[] backgrounds,
        SpriteRenderer[] tiles,
        out TileLayout layout)
    {
        layout = default;
        if (backgrounds == null || backgrounds.Length == 0 ||
            !HasTwoTiles(tiles))
        {
            return false;
        }

        Sprite selectedBackground =
            backgrounds[UnityEngine.Random.Range(0, backgrounds.Length)];

        Transform tileParent = tiles[0].transform.parent;
        float parentScaleX = tileParent != null
            ? Mathf.Abs(tileParent.lossyScale.x)
            : 1f;
        float parentScaleY = tileParent != null
            ? Mathf.Abs(tileParent.lossyScale.y)
            : 1f;
        float scaleFactor = _cameraHeight > 0f && parentScaleY > 0f
            ? (_cameraHeight + _verticalOverscan) /
              (selectedBackground.bounds.size.y * parentScaleY)
            : 1f;
        float worldScaleX = parentScaleX * scaleFactor;
        float worldScaleY = parentScaleY * scaleFactor;

        float width = selectedBackground.bounds.size.x * Mathf.Abs(worldScaleX);
        if (width <= 0f) return false;

        float pixelWidth = Mathf.Abs(worldScaleX) /
                           selectedBackground.pixelsPerUnit;
        layout = new TileLayout(
            selectedBackground,
            scaleFactor,
            width,
            width - pixelWidth * _seamOverlapPixels,
            _cameraCenterX - selectedBackground.bounds.center.x * worldScaleX,
            _cameraCenterY - selectedBackground.bounds.center.y * worldScaleY);
        return true;
    }

    private void ApplyLayout(SpriteRenderer[] tiles, TileLayout layout)
    {
        for (int i = 0; i < tiles.Length; ++i)
        {
            SpriteRenderer tileRenderer = tiles[i];
            tileRenderer.sprite = layout.Sprite;
            tileRenderer.transform.localScale = Vector3.one * layout.ScaleFactor;
            tileRenderer.transform.position = new Vector3(
                layout.OriginX + layout.Spacing * i,
                layout.OriginY,
                transform.position.z);
        }
    }

    private void CommitLayout(TileLayout layout)
    {
        _tileWidth = layout.Width;
        _tileSpacing = layout.Spacing;
        _tileOriginX = layout.OriginX;
    }

    private static Color[] CaptureColors(SpriteRenderer[] renderers)
    {
        if (renderers == null) return Array.Empty<Color>();

        var colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; ++i)
        {
            colors[i] = renderers[i] != null
                ? renderers[i].color
                : Color.white;
        }

        return colors;
    }

    private static void ApplyAlpha(
        SpriteRenderer[] renderers,
        Color[] baseColors,
        float multiplier)
    {
        if (renderers == null || baseColors == null) return;

        int count = Mathf.Min(renderers.Length, baseColors.Length);
        for (int i = 0; i < count; ++i)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null) continue;

            Color color = baseColors[i];
            color.a *= multiplier;
            renderer.color = color;
        }
    }

    private void RestoreThemeRendererColors()
    {
        ApplyAlpha(_outgoingThemeRenderers, _outgoingThemeColors, 1f);
        ApplyAlpha(_incomingThemeRenderers, _incomingThemeColors, 1f);
        _outgoingThemeRenderers = null;
        _incomingThemeRenderers = null;
        _outgoingThemeColors = null;
        _incomingThemeColors = null;
    }

    private bool AreAdditionalThemesValid()
    {
        if (_additionalThemes == null) return true;

        for (int i = 0; i < _additionalThemes.Length; ++i)
        {
            ThemeBinding binding = _additionalThemes[i];
            if (binding == null ||
                !BackgroundThemeRules.IsValid(binding.Theme) ||
                binding.Theme == EBackgroundTheme.Ground ||
                binding.Theme == EBackgroundTheme.Sky ||
                binding.Backgrounds == null ||
                binding.Backgrounds.Length == 0 ||
                binding.Root == null ||
                !HasTwoTiles(binding.Tiles))
            {
                return false;
            }

            for (int previous = 0; previous < i; ++previous)
            {
                if (_additionalThemes[previous] != null &&
                    _additionalThemes[previous].Theme == binding.Theme)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool TryGetThemeBinding(
        EBackgroundTheme theme,
        out Sprite[] backgrounds,
        out SpriteRenderer[] tiles,
        out GameObject root)
    {
        if (theme == EBackgroundTheme.Ground)
        {
            backgrounds = _groundBackgrounds;
            tiles = _groundTiles;
            root = _groundThemeRoot;
            return true;
        }

        if (theme == EBackgroundTheme.Sky)
        {
            backgrounds = _skyBackgrounds;
            tiles = _skyTiles;
            root = _skyThemeRoot;
            return true;
        }

        if (_additionalThemes != null)
        {
            foreach (ThemeBinding binding in _additionalThemes)
            {
                if (binding == null || binding.Theme != theme) continue;

                backgrounds = binding.Backgrounds;
                tiles = binding.Tiles;
                root = binding.Root;
                return true;
            }
        }

        backgrounds = null;
        tiles = null;
        root = null;
        return false;
    }

    private void SetAllThemeRootsActive(bool isActive)
    {
        _groundThemeRoot.SetActive(isActive);
        _skyThemeRoot.SetActive(isActive);

        if (_additionalThemes == null) return;

        foreach (ThemeBinding binding in _additionalThemes)
        {
            if (binding?.Root != null)
            {
                binding.Root.SetActive(isActive);
            }
        }
    }

    private static bool HasTwoTiles(SpriteRenderer[] tiles)
    {
        return tiles != null &&
               tiles.Length == 2 &&
               tiles[0] != null &&
               tiles[1] != null;
    }

}
