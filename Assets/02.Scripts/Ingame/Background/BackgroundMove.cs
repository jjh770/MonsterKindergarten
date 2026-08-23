using UnityEngine;

public class BackgroundMove : MonoBehaviour
{
    [SerializeField] private Sprite[] _groundBackgrounds;
    [SerializeField] private Sprite[] _skyBackgrounds;
    [SerializeField] private SpriteRenderer _background;
    [SerializeField, Min(0.1f)] private float _duration = 60f;
    [SerializeField, Min(0f)] private float _verticalOverscan = 0.1f;
    [SerializeField, Range(0f, 2f)] private float _seamOverlapPixels = 1f;

    private readonly SpriteRenderer[] _tiles = new SpriteRenderer[2];
    private StageManager _stageManager;
    private EGameStage? _appliedStage;
    private Color _tileColor;
    private float _tileWidth;
    private float _tileSpacing;
    private float _tileOriginX;
    private float _cameraCenterX;
    private float _cameraCenterY;
    private float _cameraHeight;

    private void Start()
    {
        if (_background == null ||
            _groundBackgrounds == null ||
            _groundBackgrounds.Length == 0 ||
            _skyBackgrounds == null ||
            _skyBackgrounds.Length == 0)
        {
            Debug.LogError("스테이지 배경 참조가 비어 있습니다.", this);
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

        _tileColor = _background.color;
        _background.enabled = false;
        _tiles[0] = CreateTile("Background Tile 1");
        _tiles[1] = CreateTile("Background Tile 2");

        // StageTransitionPlayer가 원본 Renderer를 다시 켜도 중복 노출되지 않게 한다.
        Color hiddenColor = _background.color;
        hiddenColor.a = 0f;
        _background.color = hiddenColor;

        _stageManager = StageManager.Instance;
        if (_stageManager != null)
        {
            _stageManager.StageChanged += ApplyStage;
            ApplyStage(_stageManager.CurrentStage);
        }
        else
        {
            ApplyStage(EGameStage.Ground);
        }
    }

    private void OnDestroy()
    {
        if (_stageManager != null)
        {
            _stageManager.StageChanged -= ApplyStage;
        }
    }

    private void Update()
    {
        if (_tiles[0] == null || _tiles[1] == null) return;

        float movement = _tileSpacing / _duration * Time.deltaTime;

        foreach (SpriteRenderer tileRenderer in _tiles)
        {
            Transform tile = tileRenderer.transform;
            tile.position += Vector3.left * movement;

            if (tile.position.x <= _tileOriginX - _tileSpacing)
            {
                tile.position += Vector3.right * (_tileSpacing * _tiles.Length);
            }
        }
    }

    private void ApplyStage(EGameStage stage)
    {
        if (_appliedStage == stage) return;

        _appliedStage = stage;
        Sprite[] backgrounds = stage == EGameStage.Ground
            ? _groundBackgrounds
            : _skyBackgrounds;
        Sprite selectedBackground =
            backgrounds[Random.Range(0, backgrounds.Length)];

        float parentScaleY = Mathf.Abs(transform.lossyScale.y);
        float scaleFactor = _cameraHeight > 0f && parentScaleY > 0f
            ? (_cameraHeight + _verticalOverscan) /
              (selectedBackground.bounds.size.y * parentScaleY)
            : 1f;
        float worldScaleX = transform.lossyScale.x * scaleFactor;
        float worldScaleY = transform.lossyScale.y * scaleFactor;

        _tileWidth = selectedBackground.bounds.size.x * Mathf.Abs(worldScaleX);
        if (_tileWidth <= 0f) return;

        float pixelWidth = Mathf.Abs(worldScaleX) /
                           selectedBackground.pixelsPerUnit;
        _tileSpacing = _tileWidth - pixelWidth * _seamOverlapPixels;
        _tileOriginX = _cameraCenterX -
                       selectedBackground.bounds.center.x * worldScaleX;
        float tileOriginY = _cameraCenterY -
                            selectedBackground.bounds.center.y * worldScaleY;

        for (int i = 0; i < _tiles.Length; ++i)
        {
            SpriteRenderer tileRenderer = _tiles[i];
            tileRenderer.sprite = selectedBackground;
            tileRenderer.transform.localScale = Vector3.one * scaleFactor;
            tileRenderer.transform.position = new Vector3(
                _tileOriginX + _tileSpacing * i,
                tileOriginY,
                transform.position.z);
        }
    }

    private SpriteRenderer CreateTile(string tileName)
    {
        GameObject tileObject = new(tileName);
        Transform tile = tileObject.transform;
        tile.SetParent(transform, worldPositionStays: true);

        SpriteRenderer tileRenderer = tileObject.AddComponent<SpriteRenderer>();
        tileRenderer.sharedMaterial = _background.sharedMaterial;
        tileRenderer.color = _tileColor;
        tileRenderer.flipX = _background.flipX;
        tileRenderer.flipY = _background.flipY;
        tileRenderer.sortingLayerID = _background.sortingLayerID;
        tileRenderer.sortingOrder = _background.sortingOrder;
        tileRenderer.maskInteraction = _background.maskInteraction;

        return tileRenderer;
    }
}
