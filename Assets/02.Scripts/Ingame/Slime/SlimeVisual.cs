using UnityEngine;

public class SlimeVisual : MonoBehaviour
{
    [SerializeField] private Sprite[] _levelSprites;
    private SpriteRenderer _spriteRenderer;

    private Slime _slime;

    private void Awake()
    {
        _slime = GetComponent<Slime>();

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        if (_slime != null)
        {
            _slime.OnLevelChanged += UpdateSprite;
        }
    }

    private void OnDisable()
    {
        if (_slime != null)
        {
            _slime.OnLevelChanged -= UpdateSprite;
        }
    }

    private void Start()
    {
        UpdateSprite(_slime.Level);
    }

    private void UpdateSprite(int level)
    {
        if (_levelSprites == null || _levelSprites.Length == 0 || _spriteRenderer == null)
            return;

        int spriteIndex = Mathf.Clamp(level - 1, 0, _levelSprites.Length - 1);
        _spriteRenderer.sprite = _levelSprites[spriteIndex];
    }
}
