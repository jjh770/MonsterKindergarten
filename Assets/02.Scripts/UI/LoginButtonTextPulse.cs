using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로그인 문구가 나타날 때 아래에서 튀어 오르며 커지고, 이후에는 숨 쉬듯 깜박인다.
//
// 버튼은 로고가 내려앉은 뒤에 켜지므로, 이 컴포넌트가 켜지는 순간이 곧 문구가 처음 보이는
// 순간이다. 등장 연출은 그때부터 시간으로 계산한다. 깜박임이 같은 색과 크기를 매 프레임 쓰기
// 때문에, 트윈으로 따로 움직이면 서로 덮어쓴다. 그래서 두 가지를 한곳에서 곱해 적용한다.
[DisallowMultipleComponent]
public sealed class LoginButtonTextPulse : MonoBehaviour
{
    [Header("Pulse")]
    [SerializeField, Min(0.1f)] private float _cycleSeconds = 1.6f;
    [SerializeField, Range(0f, 1f)] private float _minimumAlpha = 0.72f;
    [SerializeField, Range(1f, 1.2f)] private float _maximumScale = 1.04f;

    [Header("Enter")]
    [Tooltip("켜진 뒤 문구가 나타나기 시작할 때까지 기다리는 시간입니다. 로고가 자리 잡을 틈을 줍니다.")]
    [SerializeField, Min(0f)] private float _enterDelay = 0.15f;
    [SerializeField, Min(0.05f)] private float _enterSeconds = 0.7f;
    [Tooltip("문구가 제자리보다 이만큼 아래에서 올라옵니다(캔버스 단위).")]
    [SerializeField] private float _enterRise = 70f;
    [Tooltip("등장할 때의 시작 크기입니다. 1을 향해 커지며 살짝 넘쳤다가 돌아옵니다.")]
    [SerializeField, Range(0.1f, 1f)] private float _enterStartScale = 0.55f;

    [Header("Press")]
    [Tooltip("버튼을 누른 순간 문구가 이 크기에서 시작해 제 크기로 돌아옵니다.")]
    [SerializeField, Range(1f, 2f)] private float _pressScale = 1.4f;
    [SerializeField, Min(0.05f)] private float _pressSeconds = 0.35f;

    private TMP_Text _text;
    private RectTransform _rect;
    private Button _button;
    private Vector3 _baseScale;
    private Vector2 _basePosition;
    private Color _baseColor;
    private float _enabledAt;
    private float _pressedAt = -999f;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _rect = (RectTransform)transform;
        _button = GetComponentInParent<Button>();
        _baseScale = transform.localScale;
        _basePosition = _rect.anchoredPosition;
        _baseColor = _text.color;
    }

    // 버튼을 눌렀을 때 문구가 한 번 커졌다가 제 크기로 돌아온다.
    public void PlayPress()
    {
        _pressedAt = Time.unscaledTime;
    }

    private void OnEnable()
    {
        _enabledAt = Time.unscaledTime;
        // 지연 동안 기본 모습이 한 프레임 비치지 않게 바로 시작 상태로 둔다.
        Apply(0f, 0f);
    }

    private void Update()
    {
        if (_button != null && !_button.interactable)
        {
            RestoreBasePresentation();
            return;
        }

        float enter = Mathf.Clamp01((Time.unscaledTime - _enabledAt - _enterDelay) / _enterSeconds);
        float phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _cycleSeconds) * 0.5f + 0.5f;
        // 등장이 끝나기 전에는 깜박임을 약하게 섞어 이음매가 튀지 않게 한다.
        Apply(enter, enter, phase);
    }

    private void Apply(float enter, float pulseWeight, float phase = 0f)
    {
        float eased = EaseOutBack(enter);
        float rise = 1f - EaseOutCubic(enter);

        float pulseScale = Mathf.Lerp(1f, Mathf.Lerp(1f, _maximumScale, phase), pulseWeight);
        float press = Mathf.Clamp01((Time.unscaledTime - _pressedAt) / _pressSeconds);
        float pressScale = Mathf.Lerp(_pressScale, 1f, EaseOutCubic(press));
        float scale = Mathf.LerpUnclamped(_enterStartScale, 1f, eased) * pulseScale * pressScale;
        float alpha = Mathf.Clamp01(enter * 2f) * Mathf.Lerp(1f, Mathf.Lerp(_minimumAlpha, 1f, phase), pulseWeight);

        transform.localScale = _baseScale * scale;
        _rect.anchoredPosition = _basePosition + new Vector2(0f, -_enterRise * rise);

        Color color = _baseColor;
        color.a *= alpha;
        _text.color = color;
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
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
        _rect.anchoredPosition = _basePosition;
        _text.color = _baseColor;
    }
}
