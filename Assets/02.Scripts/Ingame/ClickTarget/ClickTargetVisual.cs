using UnityEngine;

public class ClickTargetVisual : MonoBehaviour
{
    [SerializeField] private Sprite[] _levelSprites;
    private SpriteRenderer _spriteRenderer;

    private ClickTarget _clickTarget;

    private void Awake()
    {
        _clickTarget = GetComponent<ClickTarget>();

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        if (_clickTarget != null)
        {
            _clickTarget.OnLevelChanged += UpdateSprite;
        }
    }

    private void OnDisable()
    {
        if (_clickTarget != null)
        {
            _clickTarget.OnLevelChanged -= UpdateSprite;
        }
    }

    private void Start()
    {
        UpdateSprite(_clickTarget.Level);
    }

    private void UpdateSprite(int level)
    {
        if (_levelSprites == null || _levelSprites.Length == 0 || _spriteRenderer == null)
            return;

        int spriteIndex = Mathf.Clamp(level - 1, 0, _levelSprites.Length - 1);
        _spriteRenderer.sprite = _levelSprites[spriteIndex];
    }
}
