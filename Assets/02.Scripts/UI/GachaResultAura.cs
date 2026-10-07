using UnityEngine;
using UnityEngine.UI;

// Rare 이상의 뽑기 결과 뒤에서 은은하게 도는 빛과 바깥으로 퍼지는 반짝이를 맡는다.
// 결과 이미지의 실제 크기를 기준으로 계산해서 1회 결과와 5회 슬롯이 같은 비율로 보인다.
// 같은 반짝이 조각으로 필드로 날아갈 때 뒤에 남는 꼬리와 도착할 때 퍼지는 파편도 맡는다.
// 꼬리와 파편은 희귀도와 상관없이 모든 결과에 쓴다.
public sealed class GachaResultAura : MonoBehaviour
{
    private enum Mode
    {
        Aura,
        Trail,
        Burst,
    }

    private readonly struct AuraStyle
    {
        public readonly Color Color;
        public readonly float GlowSize;
        public readonly float GlowAlpha;
        public readonly float Pulse;
        public readonly float RotationSpeed;
        public readonly int TwinkleCount;
        public readonly float TwinkleSpeed;
        public readonly float TwinkleDistance;

        public AuraStyle(
            Color color,
            float glowSize,
            float glowAlpha,
            float pulse,
            float rotationSpeed,
            int twinkleCount,
            float twinkleSpeed,
            float twinkleDistance)
        {
            Color = color;
            GlowSize = glowSize;
            GlowAlpha = glowAlpha;
            Pulse = pulse;
            RotationSpeed = rotationSpeed;
            TwinkleCount = twinkleCount;
            TwinkleSpeed = twinkleSpeed;
            TwinkleDistance = twinkleDistance;
        }
    }

    private const float TrailLife = 0.45f;
    private const float TrailInterval = 0.028f;
    private const float BurstDuration = 0.6f;

    [SerializeField] private RectTransform _target;
    [SerializeField] private Image _glow;
    [SerializeField] private Image[] _twinkles;

    private RectTransform _rect;
    private EGachaRarity _rarity;
    private Mode _mode;
    private float _elapsed;

    private Color _effectTint;
    private bool _effectRainbow;
    private float[] _trailBirth;
    private Vector2[] _trailPoint;
    private int _trailNext;
    private float _nextEmit;
    private float _burstElapsed;

    // 파편이 다 퍼지고 스스로 꺼질 때까지 걸리는 시간이다. 기다릴 쪽이 이 값으로 맞춘다.
    public float BurstSeconds => BurstDuration;

    private void Awake()
    {
        _rect = transform as RectTransform;
    }

    public void Show(EGachaRarity rarity)
    {
        if (rarity != EGachaRarity.Rare &&
            rarity != EGachaRarity.Jackpot &&
            rarity != EGachaRarity.Special)
        {
            Hide();
            return;
        }

        _mode = Mode.Aura;
        _rarity = rarity;
        _elapsed = 0f;
        gameObject.SetActive(true);
        ApplyFrame();
    }

    // 날아가는 동안 슬라임이 지나간 자리에 반짝이가 남았다가 흩어진다.
    public void PlayTrail(Color tint, bool rainbow)
    {
        if (_twinkles == null || _twinkles.Length == 0) return;

        _mode = Mode.Trail;
        _effectTint = tint;
        _effectRainbow = rainbow;
        if (_trailBirth == null || _trailBirth.Length != _twinkles.Length)
        {
            _trailBirth = new float[_twinkles.Length];
            _trailPoint = new Vector2[_twinkles.Length];
        }

        for (int i = 0; i < _trailBirth.Length; i++) _trailBirth[i] = float.NegativeInfinity;
        _trailNext = 0;
        _nextEmit = _elapsed;
        gameObject.SetActive(true);
        if (_glow != null) _glow.gameObject.SetActive(false);
    }

    // 도착한 자리에서 빛이 번쩍하고 반짝이가 사방으로 튄다. 다 퍼지면 스스로 꺼진다.
    public void PlayBurst(Color tint, bool rainbow)
    {
        if (_twinkles == null || _twinkles.Length == 0) return;

        _mode = Mode.Burst;
        _effectTint = tint;
        _effectRainbow = rainbow;
        _burstElapsed = 0f;
        gameObject.SetActive(true);
        ApplyBurst();
    }

    public void Hide()
    {
        if (_glow != null) _glow.gameObject.SetActive(false);
        if (_twinkles != null)
        {
            foreach (Image twinkle in _twinkles)
            {
                if (twinkle != null) twinkle.gameObject.SetActive(false);
            }
        }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        _elapsed += delta;
        switch (_mode)
        {
            case Mode.Trail:
                ApplyTrail();
                break;
            case Mode.Burst:
                _burstElapsed += delta;
                ApplyBurst();
                break;
            default:
                ApplyFrame();
                break;
        }
    }

    private void ApplyFrame()
    {
        if (_rect == null) _rect = transform as RectTransform;
        if (_rect == null || _target == null || _glow == null || _twinkles == null) return;

        _rect.anchoredPosition = _target.anchoredPosition;
        AuraStyle style = GetStyle(_rarity);
        Color tint = _rarity == EGachaRarity.Special ? RainbowTint.Shimmer() : style.Color;
        float targetSize = Mathf.Max(_target.rect.width, _target.rect.height);
        float pulse = 1f + Mathf.Sin(_elapsed * Mathf.PI * 2f * 0.55f) * style.Pulse;

        _glow.gameObject.SetActive(true);
        _glow.rectTransform.sizeDelta = Vector2.one * (targetSize * style.GlowSize);
        _glow.rectTransform.localScale = Vector3.one * pulse;
        _glow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _elapsed * style.RotationSpeed);
        SetColor(_glow, tint, style.GlowAlpha * (0.88f + 0.12f * pulse));

        int count = Mathf.Min(style.TwinkleCount, _twinkles.Length);
        for (int i = 0; i < _twinkles.Length; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;
            bool visible = i < count;
            twinkle.gameObject.SetActive(visible);
            if (!visible) continue;

            float phase = Mathf.Repeat(_elapsed * style.TwinkleSpeed + (float)i / count, 1f);
            float angle = Mathf.PI * 2f * i / count + _elapsed * 0.22f * (i % 2 == 0 ? 1f : -1f);
            float distance = Mathf.Lerp(targetSize * 0.18f, targetSize * style.TwinkleDistance, phase);
            float alpha = Mathf.Sin(phase * Mathf.PI);
            twinkle.rectTransform.sizeDelta = Vector2.one * (targetSize * 0.14f);
            twinkle.rectTransform.anchoredPosition =
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.75f, 0.12f, phase);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, phase * 150f + i * 31f);
            SetColor(twinkle, Color.Lerp(Color.white, tint, 0.55f), alpha * 0.85f);
        }
    }

    // 꼬리 조각은 놓인 자리에 가만히 있고 작아지며 사라진다. 이 오브젝트는 원점에 두고 조각만 옮긴다.
    private void ApplyTrail()
    {
        if (_rect == null) _rect = transform as RectTransform;
        if (_rect == null || _target == null || _twinkles == null || _trailBirth == null) return;

        _rect.anchoredPosition = Vector2.zero;
        float size = GetVisibleSize();

        if (_elapsed >= _nextEmit)
        {
            _nextEmit = _elapsed + TrailInterval;
            int slot = _trailNext;
            _trailNext = (slot + 1) % _twinkles.Length;
            _trailBirth[slot] = _elapsed;
            _trailPoint[slot] = _target.anchoredPosition + Random.insideUnitCircle * (size * 0.16f);
        }

        Color tint = _effectRainbow ? RainbowTint.Shimmer() : _effectTint;
        for (int i = 0; i < _twinkles.Length; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            float age = _elapsed - _trailBirth[i];
            bool visible = age >= 0f && age < TrailLife;
            twinkle.gameObject.SetActive(visible);
            if (!visible) continue;

            float ratio = age / TrailLife;
            twinkle.rectTransform.sizeDelta = Vector2.one * (size * 0.3f);
            twinkle.rectTransform.anchoredPosition = _trailPoint[i];
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.1f, ratio);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ratio * 180f + i * 31f);
            SetColor(twinkle, Color.Lerp(Color.white, tint, 0.5f), (1f - ratio) * 0.9f);
        }
    }

    private void ApplyBurst()
    {
        if (_rect == null) _rect = transform as RectTransform;
        if (_rect == null || _target == null || _glow == null || _twinkles == null) return;

        float ratio = Mathf.Clamp01(_burstElapsed / BurstDuration);
        if (ratio >= 1f)
        {
            Hide();
            return;
        }

        _rect.anchoredPosition = _target.anchoredPosition;
        float size = GetVisibleSize();
        float spread = 1f - Mathf.Pow(1f - ratio, 3f);
        Color tint = _effectRainbow ? RainbowTint.Shimmer() : _effectTint;

        // 번쩍이는 빛은 처음에 가장 밝고 빠르게 퍼지며 사라진다.
        _glow.gameObject.SetActive(true);
        _glow.rectTransform.sizeDelta = Vector2.one * (size * Mathf.Lerp(1.1f, 3.2f, spread));
        _glow.rectTransform.localScale = Vector3.one;
        _glow.rectTransform.localRotation = Quaternion.identity;
        SetColor(_glow, Color.Lerp(Color.white, tint, 0.45f), 0.7f * (1f - ratio) * (1f - ratio));

        int count = _twinkles.Length;
        for (int i = 0; i < count; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            twinkle.gameObject.SetActive(true);
            float angle = Mathf.PI * 2f * i / count + (i % 2) * 0.2f;
            float reach = size * (0.9f + 0.4f * (i % 3));
            float distance = Mathf.Lerp(size * 0.25f, reach, spread);
            twinkle.rectTransform.sizeDelta = Vector2.one * (size * 0.3f);
            twinkle.rectTransform.anchoredPosition =
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.12f, ratio);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ratio * 220f + i * 31f);
            SetColor(twinkle, Color.Lerp(Color.white, tint, 0.5f), 1f - ratio);
        }
    }

    // 목표가 지금 화면에서 차지하는 크기다. 날아가며 작아지는 것을 따라가 꼬리와 파편이 슬라임 몸집에 맞는다.
    // 너무 작아져도 보이도록 아래를 막아 둔다.
    private float GetVisibleSize()
    {
        float size = Mathf.Max(_target.rect.width, _target.rect.height);
        return size * Mathf.Max(0.35f, _target.localScale.x);
    }

    private static AuraStyle GetStyle(EGachaRarity rarity)
    {
        return rarity switch
        {
            EGachaRarity.Special => new AuraStyle(
                Color.white, 2.15f, 0.38f, 0.12f, 30f, 12, 0.95f, 0.92f),
            EGachaRarity.Jackpot => new AuraStyle(
                new Color(1f, 0.76f, 0.22f), 1.85f, 0.3f, 0.08f, 18f, 8, 0.72f, 0.8f),
            _ => new AuraStyle(
                new Color(0.67f, 0.42f, 1f), 1.55f, 0.22f, 0.05f, 8f, 5, 0.52f, 0.68f),
        };
    }

    private static void SetColor(Image image, Color color, float alpha)
    {
        image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
