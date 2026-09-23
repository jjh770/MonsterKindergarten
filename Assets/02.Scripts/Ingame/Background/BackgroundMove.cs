using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class BackgroundMove : MonoBehaviour
{
    [SerializeField] private Sprite[] _groundBackgrounds;
    [SerializeField] private Sprite[] _skyBackgrounds;
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

    // 배경 테마를 세로로 밀어 바꾸는 연출. 미는 동안에는 두 배경이 함께 켜져 있다.
    private Tween _slideTween;
    private Vector3 _groundRootBasePosition;
    private Vector3 _skyRootBasePosition;

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
            !HasTwoTiles(_displayRoomTiles))
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

        _groundRootBasePosition = _groundThemeRoot.transform.localPosition;
        _skyRootBasePosition = _skyThemeRoot.transform.localPosition;

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
        _slideTween?.Kill();
        _slideTween = null;

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

    // 배경 테마를 세로로 밀어 바꾼다. 하늘은 위에서 내려오고 땅은 아래에서 올라온다.
    // 슬라임은 배경 루트 밖에 있어 제자리에 그대로 남으므로, 화면이 올라가는 것이
    // 아니라 배경만 갈리는 것으로 읽힌다.
    //
    // 장식장을 보는 중이거나 카메라 높이를 못 구한 상태면 연출할 자리가 없다.
    // 그때는 false를 돌려주고 호출부가 화면을 덮는 기존 방식으로 처리한다.
    public bool TryPlayThemeSlide(
        EBackgroundTheme targetTheme,
        float duration,
        Action onCompleted)
    {
        if (!enabled || _slideTween != null) return false;
        if (_currentSpace != EGameplaySpace.MainField) return false;
        if (targetTheme == _currentTheme) return false;
        if (_cameraHeight <= 0f || duration <= 0f) return false;

        SpriteRenderer[] incomingTiles = GetThemeTiles(targetTheme);
        GameObject incomingRoot = GetThemeRoot(targetTheme);
        GameObject outgoingRoot = GetThemeRoot(_currentTheme);
        if (!TryBuildLayout(
                GetThemeBackgrounds(targetTheme),
                incomingTiles,
                out TileLayout layout))
        {
            return false;
        }

        ApplyLayout(incomingTiles, layout);
        incomingRoot.SetActive(true);

        float direction = targetTheme == EBackgroundTheme.Sky ? 1f : -1f;
        float distance = _cameraHeight;
        SetRootOffset(outgoingRoot, 0f);
        SetRootOffset(incomingRoot, direction * distance);

        // 미는 동안 들어오는 배경은 흐르지 않는다. 한 바퀴가 분 단위라 이 짧은
        // 시간에 멈춰 있어도 눈에 띄지 않는다.
        _slideTween = DOVirtual.Float(0f, 1f, duration, progress =>
            {
                SetRootOffset(incomingRoot, direction * distance * (1f - progress));
                SetRootOffset(outgoingRoot, -direction * distance * progress);
            })
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                _slideTween = null;
                SetRootOffset(incomingRoot, 0f);
                SetRootOffset(outgoingRoot, 0f);
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

        // 슬라이드가 이미 이 배경을 깔아 두고 미는 중이다. 여기서 다시 깔면 끊긴다.
        if (_slideTween != null) return;

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
        CancelSlide();

        _groundThemeRoot.SetActive(false);
        _skyThemeRoot.SetActive(false);
        _displayRoomRoot.SetActive(false);

        Sprite[] backgrounds = _currentSpace == EGameplaySpace.DisplayRoom
            ? _displayRoomBackgrounds
            : GetThemeBackgrounds(_currentTheme);
        _activeTiles = _currentSpace == EGameplaySpace.DisplayRoom
            ? _displayRoomTiles
            : GetThemeTiles(_currentTheme);
        GameObject activeRoot = _currentSpace == EGameplaySpace.DisplayRoom
            ? _displayRoomRoot
            : GetThemeRoot(_currentTheme);
        activeRoot.SetActive(true);

        if (!TryBuildLayout(backgrounds, _activeTiles, out TileLayout layout)) return;

        ApplyLayout(_activeTiles, layout);
        CommitLayout(layout);
    }

    // 미는 도중에 장식장으로 넘어가는 것처럼 배경을 다시 깔아야 하는 일이 생기면
    // 연출을 접고 두 루트를 제자리로 돌린다.
    private void CancelSlide()
    {
        if (_slideTween == null) return;

        _slideTween.Kill();
        _slideTween = null;
        SetRootOffset(_groundThemeRoot, 0f);
        SetRootOffset(_skyThemeRoot, 0f);
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

    private void SetRootOffset(GameObject root, float offsetY)
    {
        if (root == null) return;

        Vector3 basePosition = root == _skyThemeRoot
            ? _skyRootBasePosition
            : _groundRootBasePosition;
        root.transform.localPosition = basePosition + Vector3.up * offsetY;
    }

    private Sprite[] GetThemeBackgrounds(EBackgroundTheme theme)
    {
        return theme == EBackgroundTheme.Ground
            ? _groundBackgrounds
            : _skyBackgrounds;
    }

    private SpriteRenderer[] GetThemeTiles(EBackgroundTheme theme)
    {
        return theme == EBackgroundTheme.Ground ? _groundTiles : _skyTiles;
    }

    private GameObject GetThemeRoot(EBackgroundTheme theme)
    {
        return theme == EBackgroundTheme.Ground ? _groundThemeRoot : _skyThemeRoot;
    }

    private static bool HasTwoTiles(SpriteRenderer[] tiles)
    {
        return tiles != null &&
               tiles.Length == 2 &&
               tiles[0] != null &&
               tiles[1] != null;
    }

}
