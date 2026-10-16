using UnityEngine;

// 슬라임이 지나가면 빛과 반딧불만 반응한다. 이동이나 생산에는 관여하지 않는다.
public sealed class DisplayRoomFireflyLantern : MonoBehaviour, IPlaygroundObject
{
    [SerializeField] private CircleCollider2D _sensor;
    [SerializeField] private SpriteRenderer _glow;
    [SerializeField] private SpriteRenderer[] _fireflies = System.Array.Empty<SpriteRenderer>();
    [SerializeField, Min(0.1f)] private float _reactionDuration = 2.4f;
    [SerializeField, Min(0f)] private float _cooldown = 1.5f;
    [SerializeField, Range(0f, 1f)] private float _peakGlowAlpha = 0.65f;
    [SerializeField, Min(0f)] private float _riseDistance = 0.7f;

    private Vector3[] _fireflyOrigins;
    private Vector3 _glowScale;
    private float _elapsed;
    private float _nextReactionTime;
    private bool _isReacting;
    private bool _isInteractive = true;
    private bool _isInitialized;

    private void Awake()
    {
        if (_sensor == null || !_sensor.isTrigger || _glow == null)
        {
            Debug.LogError("반딧불 랜턴의 감지 트리거 또는 빛 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        _glowScale = _glow.transform.localScale;
        _fireflyOrigins = new Vector3[_fireflies.Length];
        for (int i = 0; i < _fireflies.Length; i++)
        {
            if (_fireflies[i] != null)
                _fireflyOrigins[i] = _fireflies[i].transform.localPosition;
        }

        _isInitialized = true;
        ResetReaction();
    }

    public void SetInteractive(bool isInteractive)
    {
        _isInteractive = isInteractive;
        if (_sensor != null) _sensor.enabled = isInteractive;
        if (!isInteractive) ResetReaction();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled || !_isInteractive || _isReacting ||
            !GameplayGate.IsActive || Time.time < _nextReactionTime)
        {
            return;
        }

        SlimeController slime = other.GetComponentInParent<SlimeController>();
        if (slime == null || slime.Location != ESlimeLocation.DisplayRoom ||
            slime.IsDragging || !slime.HasLanded)
        {
            return;
        }

        _elapsed = 0f;
        _isReacting = true;
        _nextReactionTime = Time.time + _reactionDuration + _cooldown;
    }

    private void Update()
    {
        if (!_isReacting || !GameplayGate.IsActive) return;

        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.1f, _reactionDuration));
        float envelope = Mathf.Sin(progress * Mathf.PI);
        SetAlpha(_glow, envelope * _peakGlowAlpha);
        _glow.transform.localScale = _glowScale * (1f + 0.15f * envelope);

        for (int i = 0; i < _fireflies.Length; i++)
        {
            SpriteRenderer firefly = _fireflies[i];
            if (firefly == null) continue;

            float phase = progress * Mathf.PI * 2f + i * 1.7f;
            firefly.transform.localPosition = _fireflyOrigins[i] + new Vector3(
                Mathf.Sin(phase) * 0.12f,
                progress * _riseDistance + Mathf.Sin(phase * 1.3f) * 0.05f,
                0f);
            SetAlpha(firefly, envelope * (0.7f + 0.3f * Mathf.Sin(phase)));
        }

        if (progress >= 1f) ResetReaction();
    }

    private void OnDisable()
    {
        ResetReaction();
        _nextReactionTime = 0f;
    }

    private void ResetReaction()
    {
        _isReacting = false;
        _elapsed = 0f;
        // 꺼진 장식장 안에 생성되면 Awake보다 먼저 배치 모드가 호출할 수 있다.
        if (!_isInitialized) return;

        if (_glow != null)
        {
            SetAlpha(_glow, 0f);
            _glow.transform.localScale = _glowScale;
        }

        for (int i = 0; i < _fireflies.Length; i++)
        {
            SpriteRenderer firefly = _fireflies[i];
            if (firefly == null) continue;
            SetAlpha(firefly, 0f);
            if (_fireflyOrigins != null)
                firefly.transform.localPosition = _fireflyOrigins[i];
        }
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}
