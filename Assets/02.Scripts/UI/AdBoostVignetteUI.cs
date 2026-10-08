using UnityEngine;
using UnityEngine.UI;

// 포인트 부스트가 켜져 있는 동안 화면 가장자리를 불꽃으로 물들인다.
//
// 상단 알약은 작아서 놓치기 쉬우므로, 부스트가 도는 동안 불꽃 테두리가 천천히 숨 쉬듯 맥동하고
// 아래 가장자리에서 불씨가 올라온다. 남은 시간이 얼마 없으면 맥동이 빨라져 곧 끝난다는 것을 알린다.
// 깜빡이지 않고 속도만 올리므로 눈이 부시지 않다.
//
// 진짜 ParticleSystem이 아니라 UI 이미지다. HUD가 Screen Space - Overlay 캔버스라 파티클은
// 그려지지 않는다. 입력은 받지 않는다(모든 그래픽의 Raycast Target을 끈다). 이 컴포넌트는 늘 켜져 있는
// 오브젝트에 두고, 켜고 끄는 것은 테두리(_frame)만 한다.
public sealed class AdBoostVignetteUI : MonoBehaviour
{
    [SerializeField] private AdRewardService _adRewardService;
    [SerializeField] private GameObject _frame;
    [SerializeField] private CanvasGroup _frameGroup;
    [Tooltip("불씨가 움직이는 영역입니다. 테두리와 같은 크기로 채웁니다.")]
    [SerializeField] private RectTransform _emberRoot;
    [Tooltip("불씨 원본입니다. 씬에서는 비활성으로 둡니다.")]
    [SerializeField] private Image _emberTemplate;

    [Header("Fade")]
    [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.6f;
    [SerializeField, Min(0.05f)] private float _checkInterval = 0.1f;

    [Header("Pulse")]
    [Tooltip("맥동 한 번에 걸리는 시간(초)입니다.")]
    [SerializeField, Min(0.2f)] private float _pulseSeconds = 2.6f;
    [Tooltip("맥동이 가장 옅어졌을 때의 불투명도입니다. 1이 가장 진한 상태입니다.")]
    [SerializeField, Range(0f, 1f)] private float _pulseMinAlpha = 0.65f;
    [Tooltip("남은 시간이 이 값 이하이면 맥동이 빨라집니다. 0이면 쓰지 않습니다.")]
    [SerializeField, Min(0f)] private float _warningSeconds = 10f;
    [SerializeField, Min(0.2f)] private float _warningPulseSeconds = 1f;
    [SerializeField, Range(0f, 1f)] private float _warningMinAlpha = 0.4f;

    [Header("Ember")]
    [SerializeField, Range(0, 16)] private int _emberCount = 8;
    [SerializeField] private Vector2 _emberDurationRange = new Vector2(2.4f, 3.8f);
    [Tooltip("불씨가 오르는 높이입니다. 영역 높이에 대한 비율입니다.")]
    [SerializeField] private Vector2 _emberRiseRange = new Vector2(0.35f, 0.7f);
    [SerializeField] private Vector2 _emberScaleRange = new Vector2(0.18f, 0.36f);
    [Tooltip("좌우로 흔들리는 폭(캔버스 단위)입니다.")]
    [SerializeField, Min(0f)] private float _emberSway = 40f;

    private sealed class Ember
    {
        public RectTransform Rect;
        public Graphic Graphic;
        public float Time;
        public float Duration;
        public float StartX;
        public float Rise;
        public float Scale;
        public float SwayPhase;
        public float SwaySign;
    }

    private readonly System.Collections.Generic.List<Ember> _embers = new();
    private float _checkTimer;
    private float _remaining;
    private float _fade;
    private float _pulsePhase;
    private float _warningBlend;
    private bool _wasActive;

    private void Awake()
    {
        if (_adRewardService == null || _frame == null || _frameGroup == null ||
            _emberRoot == null || _emberTemplate == null)
        {
            Debug.LogError("부스트 불꽃 효과의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _emberTemplate.gameObject.SetActive(false);
        for (int i = 0; i < _emberCount; i++)
        {
            _embers.Add(CreateEmber(i));
        }

        _frameGroup.alpha = 0f;
        _frame.SetActive(false);
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        _checkTimer += delta;
        if (_checkTimer >= _checkInterval)
        {
            _checkTimer = 0f;
            _remaining = _adRewardService.GetPointBoostRemainingSeconds();
        }

        bool isActive = _remaining > 0f;
        _fade = Mathf.MoveTowards(_fade, isActive ? 1f : 0f, delta / _fadeSeconds);
        if (_fade <= 0f)
        {
            if (_frame.activeSelf) _frame.SetActive(false);
            _wasActive = false;
            return;
        }

        if (!_frame.activeSelf) _frame.SetActive(true);
        if (isActive && !_wasActive) ResetEmbers();
        _wasActive = isActive;

        bool warning = isActive && _warningSeconds > 0f && _remaining <= _warningSeconds;
        _warningBlend = Mathf.MoveTowards(_warningBlend, warning ? 1f : 0f, delta * 1.5f);
        float period = Mathf.Lerp(_pulseSeconds, _warningPulseSeconds, _warningBlend);
        float minAlpha = Mathf.Lerp(_pulseMinAlpha, _warningMinAlpha, _warningBlend);
        // 위상을 쌓아 가므로 주기가 바뀌어도 불투명도가 튀지 않는다.
        _pulsePhase += delta * Mathf.PI * 2f / period;
        float wave = 0.5f + 0.5f * Mathf.Sin(_pulsePhase);
        _frameGroup.alpha = _fade * Mathf.Lerp(minAlpha, 1f, wave);

        UpdateEmbers(delta, isActive);
    }

    private Ember CreateEmber(int index)
    {
        GameObject clone = Instantiate(_emberTemplate.gameObject, _emberRoot);
        clone.name = $"Ember{index + 1}";
        RectTransform rect = (RectTransform)clone.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        clone.SetActive(false);
        return new Ember { Rect = rect, Graphic = clone.GetComponent<Graphic>() };
    }

    // 켜질 때마다 불씨를 흩어 놓는다. 시작 시각을 음수로 주어 한꺼번에 올라오지 않게 한다.
    private void ResetEmbers()
    {
        foreach (Ember ember in _embers)
        {
            Roll(ember);
            ember.Time = -Random.Range(0f, ember.Duration);
        }
    }

    private void Roll(Ember ember)
    {
        Rect area = _emberRoot.rect;
        ember.Duration = Random.Range(_emberDurationRange.x, _emberDurationRange.y);
        ember.StartX = Random.Range(area.width * 0.05f, area.width * 0.95f);
        ember.Rise = area.height * Random.Range(_emberRiseRange.x, _emberRiseRange.y);
        ember.Scale = Random.Range(_emberScaleRange.x, _emberScaleRange.y);
        ember.SwayPhase = Random.Range(0f, Mathf.PI * 2f);
        ember.SwaySign = Random.value < 0.5f ? -1f : 1f;
        ember.Time = 0f;
    }

    private void UpdateEmbers(float delta, bool respawn)
    {
        foreach (Ember ember in _embers)
        {
            ember.Time += delta;
            if (ember.Time < 0f)
            {
                ember.Rect.gameObject.SetActive(false);
                continue;
            }

            if (ember.Time >= ember.Duration)
            {
                // 부스트가 끝나 가는 동안에는 새로 띄우지 않고, 올라가던 불씨만 마저 사라지게 한다.
                if (!respawn)
                {
                    ember.Rect.gameObject.SetActive(false);
                    continue;
                }

                Roll(ember);
            }

            float t = ember.Time / ember.Duration;
            float x = ember.StartX + Mathf.Sin(t * Mathf.PI * 2f + ember.SwayPhase) * _emberSway * ember.SwaySign;
            float y = Mathf.Lerp(-20f, ember.Rise, 1f - (1f - t) * (1f - t));
            // 빠르게 켜지고 천천히 사라진다. 올라갈수록 작아진다.
            float alpha = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1f - t) / 0.55f);
            ember.Rect.gameObject.SetActive(true);
            ember.Rect.anchoredPosition = new Vector2(x, y);
            ember.Rect.localScale = Vector3.one * (ember.Scale * Mathf.Lerp(1f, 0.5f, t));
            ember.Graphic.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
