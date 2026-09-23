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

    private void ApplyTheme(EBackgroundTheme theme)
    {
        _currentTheme = theme;
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
        _groundThemeRoot.SetActive(false);
        _skyThemeRoot.SetActive(false);
        _displayRoomRoot.SetActive(false);

        Sprite[] backgrounds = _currentSpace == EGameplaySpace.DisplayRoom
            ? _displayRoomBackgrounds
            : _currentTheme == EBackgroundTheme.Ground
            ? _groundBackgrounds
            : _skyBackgrounds;
        _activeTiles = _currentSpace == EGameplaySpace.DisplayRoom
            ? _displayRoomTiles
            : _currentTheme == EBackgroundTheme.Ground
                ? _groundTiles
                : _skyTiles;
        GameObject activeRoot = _currentSpace == EGameplaySpace.DisplayRoom
            ? _displayRoomRoot
            : _currentTheme == EBackgroundTheme.Ground
                ? _groundThemeRoot
                : _skyThemeRoot;
        Sprite selectedBackground =
            backgrounds[Random.Range(0, backgrounds.Length)];
        activeRoot.SetActive(true);

        Transform tileParent = _activeTiles[0].transform.parent;
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

        _tileWidth = selectedBackground.bounds.size.x * Mathf.Abs(worldScaleX);
        if (_tileWidth <= 0f) return;

        float pixelWidth = Mathf.Abs(worldScaleX) /
                           selectedBackground.pixelsPerUnit;
        _tileSpacing = _tileWidth - pixelWidth * _seamOverlapPixels;
        _tileOriginX = _cameraCenterX -
                       selectedBackground.bounds.center.x * worldScaleX;
        float tileOriginY = _cameraCenterY -
                            selectedBackground.bounds.center.y * worldScaleY;

        for (int i = 0; i < _activeTiles.Length; ++i)
        {
            SpriteRenderer tileRenderer = _activeTiles[i];
            tileRenderer.sprite = selectedBackground;
            tileRenderer.transform.localScale = Vector3.one * scaleFactor;
            tileRenderer.transform.position = new Vector3(
                _tileOriginX + _tileSpacing * i,
                tileOriginY,
                transform.position.z);
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
