using DG.Tweening;
using UnityEngine;

public class ColorFlashFeedback : MonoBehaviour, IFeedback
{
    private SpriteRenderer _spriteRenderer;
    [SerializeField] private Color _flashColor;
    [SerializeField, Min(0f)] private float _flashDuration = 0.3f;
    [Tooltip("손으로 슬라임을 눌렀을 때 번쩍일지입니다. 눌림, 표정, 포인트 표시가 이미 반응을 알려 줘서 기본은 끕니다.")]
    [SerializeField] private bool _flashOnManualClick = false;
    [Tooltip("자동 생산이 포인트를 만들 때 번쩍일지입니다. 어떤 슬라임이 생산 중인지 보여 주는 신호입니다.")]
    [SerializeField] private bool _flashOnAutoClick = true;

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
        bool isManual = clickInfo.ClickType == EClickType.Manual;
        if (isManual ? !_flashOnManualClick : !_flashOnAutoClick)
        {
            return;
        }

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
