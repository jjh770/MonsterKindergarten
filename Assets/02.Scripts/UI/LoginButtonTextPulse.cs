using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LoginButtonTextPulse : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _cycleSeconds = 1.6f;
    [SerializeField, Range(0f, 1f)] private float _minimumAlpha = 0.72f;
    [SerializeField, Range(1f, 1.2f)] private float _maximumScale = 1.04f;

    private TMP_Text _text;
    private Button _button;
    private Vector3 _baseScale;
    private Color _baseColor;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _button = GetComponentInParent<Button>();
        _baseScale = transform.localScale;
        _baseColor = _text.color;
    }

    private void Update()
    {
        if (_button != null && !_button.interactable)
        {
            RestoreBasePresentation();
            return;
        }

        float phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _cycleSeconds) * 0.5f + 0.5f;
        float scale = Mathf.Lerp(1f, _maximumScale, phase);
        Color color = _baseColor;
        color.a *= Mathf.Lerp(_minimumAlpha, 1f, phase);

        transform.localScale = _baseScale * scale;
        _text.color = color;
    }

    private void OnDisable()
    {
        if (_text != null)
        {
            RestoreBasePresentation();
        }
    }

    private void RestoreBasePresentation()
    {
        transform.localScale = _baseScale;
        _text.color = _baseColor;
    }
}
