using System.Collections;
using UnityEngine;

public class ColorFlashFeedback : MonoBehaviour, IFeedback
{
    private SpriteRenderer _spriteRenderer;
    [SerializeField] private Color _flashColor;

    private Coroutine _coroutine;
    private Color _defaultColor;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _defaultColor = _spriteRenderer.color;
    }

    private void OnDisable()
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
            _coroutine = null;
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _defaultColor;
        }
    }

    public void Play(ClickInfo clickInfo)
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
            _coroutine = null;
        }
        _coroutine = StartCoroutine(Play_Coroutine());
    }

    private IEnumerator Play_Coroutine()
    {
        _spriteRenderer.color = _flashColor;

        yield return new WaitForSeconds(0.3f);

        _spriteRenderer.color = _defaultColor;
        _coroutine = null;
    }
}
