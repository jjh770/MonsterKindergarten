using DG.Tweening;
using UnityEngine;

public class ColorFlashFeedback : MonoBehaviour, IFeedback
{
    private SpriteRenderer _spriteRenderer;
    [SerializeField] private Color _flashColor;
    [SerializeField, Min(0f)] private float _flashDuration = 0.3f;

    private Tween _flashTween;
    private Color _defaultColor;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _defaultColor = _spriteRenderer.color;
    }

    private void OnDisable()
    {
        CleanupFlash();
    }

    private void OnDestroy()
    {
        CleanupFlash();
    }

    public void Play(ClickInfo clickInfo)
    {
        CleanupFlash();
        if (_spriteRenderer == null) return;

        _spriteRenderer.color = _flashColor;
        _flashTween = DOVirtual.DelayedCall(
                _flashDuration,
                CompleteFlash,
                true)
            .SetTarget(this);
    }

    private void CompleteFlash()
    {
        RestoreColor();
        _flashTween = null;
    }

    private void CleanupFlash()
    {
        _flashTween?.Kill();
        _flashTween = null;
        RestoreColor();
    }

    private void RestoreColor()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _defaultColor;
        }
    }
}
