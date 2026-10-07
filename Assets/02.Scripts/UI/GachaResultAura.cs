using UnityEngine;
using UnityEngine.UI;

// Rare 이상의 뽑기 결과 뒤에서 은은하게 도는 빛과 바깥으로 퍼지는 반짝이를 맡는다.
// 결과 이미지의 실제 크기를 기준으로 계산해서 1회 결과와 5회 슬롯이 같은 비율로 보인다.
public sealed class GachaResultAura : MonoBehaviour
{
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

    [SerializeField] private RectTransform _target;
    [SerializeField] private Image _glow;
    [SerializeField] private Image[] _twinkles;

    private RectTransform _rect;
    private EGachaRarity _rarity;
    private float _elapsed;

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

        _rarity = rarity;
        _elapsed = 0f;
        gameObject.SetActive(true);
        ApplyFrame();
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
        _elapsed += Time.unscaledDeltaTime;
        ApplyFrame();
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
