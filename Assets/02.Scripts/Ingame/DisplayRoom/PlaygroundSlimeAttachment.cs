using System;
using TMPro;
using UnityEngine;

// 소품 프리팹 안에 미리 만들어 둔 표시(하트, Zzz, 꽃 장식, 빛 두르기)를 정해진 시간 동안 슬라임
// 머리 위나 몸 둘레에 붙여 따라가게 한다. 끝나면 투명하게 사라지고 원래 자리로 돌아간다.
//
// 오브젝트는 소품 아래에 그대로 둔 채 매 프레임 월드 위치만 맞춘다. 부모를 슬라임으로 바꾸면
// 풀에서 다시 쓰이는 슬라임에 남거나, 소품이 사라질 때 함께 정리되지 않는다.
public sealed class PlaygroundSlimeAttachment : MonoBehaviour
{
    [Tooltip("슬라임 그림의 중심에서 얼마나 위에 둘지입니다. 그림 높이의 절반을 1로 봅니다.")]
    [SerializeField] private float _headHeight = 1.1f;
    [Tooltip("그 위치에서 더 옮길 거리입니다(월드 단위).")]
    [SerializeField] private Vector2 _offset;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [Tooltip("나타날 때 작은 크기에서 튀어나옵니다.")]
    [SerializeField] private bool _popIn = true;
    [Tooltip("자식 그림과 글자의 투명도를 이 컴포넌트가 맡을지입니다. 소품이 직접 깜빡이게 하는 글자(Zzz)는 끕니다.")]
    [SerializeField] private bool _controlAlpha = true;

    [Header("Bob")]
    [SerializeField, Min(0f)] private float _bobHeight;
    [SerializeField, Min(0f)] private float _bobSpeed = 3f;

    [Header("Orbit")]
    [Tooltip("중심 둘레를 도는 자식들입니다(반딧불 등).")]
    [SerializeField] private Transform[] _orbiters = Array.Empty<Transform>();
    [SerializeField, Min(0f)] private float _orbitRadius = 0.5f;
    [SerializeField] private float _orbitSpeed = 2f;

    private SpriteRenderer[] _sprites;
    private float[] _spriteAlphas;
    private TMP_Text[] _texts;
    private float[] _textAlphas;
    private Vector3 _homeLocalPosition;
    private Vector3 _baseScale;
    private SlimeController _target;
    private SpriteRenderer _targetRenderer;
    private float _remaining;
    private float _elapsed;
    private bool _isInitialized;

    public bool IsFollowing => _target != null;
    public SlimeController Target => _target;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (_isInitialized) return;

        _sprites = GetComponentsInChildren<SpriteRenderer>(true);
        _spriteAlphas = new float[_sprites.Length];
        for (int i = 0; i < _sprites.Length; i++) _spriteAlphas[i] = _sprites[i].color.a;
        _texts = GetComponentsInChildren<TMP_Text>(true);
        _textAlphas = new float[_texts.Length];
        for (int i = 0; i < _texts.Length; i++) _textAlphas[i] = _texts[i].color.a;
        _homeLocalPosition = transform.localPosition;
        _baseScale = transform.localScale;
        _isInitialized = true;
        ApplyFade(0f);
    }

    // 이미 다른 슬라임을 따라가고 있으면 새 슬라임으로 옮겨 간다.
    public void Follow(SlimeController slime, float duration)
    {
        Initialize();
        if (slime == null) return;

        _target = slime;
        _targetRenderer = slime.GetComponent<SpriteRenderer>();
        _remaining = Mathf.Max(_fadeDuration * 2f, duration);
        _elapsed = 0f;
        LateUpdate();
    }

    public void Stop()
    {
        Initialize();
        _target = null;
        _targetRenderer = null;
        transform.localPosition = _homeLocalPosition;
        transform.localScale = _baseScale;
        ApplyFade(0f);
    }

    private void OnDisable()
    {
        if (_isInitialized) Stop();
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // 슬라임이 풀로 돌아갔거나 장식장을 떠났다.
        if (!_target.isActiveAndEnabled || _target.Location != ESlimeLocation.DisplayRoom)
        {
            Stop();
            return;
        }

        float delta = Time.deltaTime;
        _elapsed += delta;
        _remaining -= delta;
        if (_remaining <= 0f)
        {
            Stop();
            return;
        }

        float fadeIn = Mathf.Clamp01(_elapsed / _fadeDuration);
        float fadeOut = Mathf.Clamp01(_remaining / _fadeDuration);
        float visibility = Mathf.Min(fadeIn, fadeOut);
        // 대포에 빨려 들어가는 것처럼 그림이 꺼진 동안에는 같이 감춘다.
        if (_targetRenderer != null && !_targetRenderer.enabled) visibility = 0f;
        ApplyFade(visibility);

        Vector3 center = _targetRenderer != null ? _targetRenderer.bounds.center : _target.transform.position;
        float halfHeight = _targetRenderer != null ? _targetRenderer.bounds.extents.y : 0.5f;
        float bob = _bobHeight > 0f ? Mathf.Sin(_elapsed * _bobSpeed) * _bobHeight : 0f;
        transform.position = center + new Vector3(_offset.x, halfHeight * _headHeight + _offset.y + bob, 0f);
        transform.localScale = _popIn ? _baseScale * Mathf.Lerp(.4f, 1f, Mathf.Sqrt(fadeIn)) : _baseScale;

        for (int i = 0; i < _orbiters.Length; i++)
        {
            if (_orbiters[i] == null) continue;

            float angle = _elapsed * _orbitSpeed + i * Mathf.PI * 2f / _orbiters.Length;
            _orbiters[i].localPosition = new Vector3(
                Mathf.Cos(angle) * _orbitRadius,
                Mathf.Sin(angle) * _orbitRadius * .6f,
                0f) / Mathf.Max(.0001f, transform.lossyScale.x);
        }
    }

    private void ApplyFade(float visibility)
    {
        if (!_controlAlpha) return;

        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] == null) continue;
            Color color = _sprites[i].color;
            color.a = _spriteAlphas[i] * visibility;
            _sprites[i].color = color;
        }

        for (int i = 0; i < _texts.Length; i++)
        {
            if (_texts[i] == null) continue;
            Color color = _texts[i].color;
            color.a = _textAlphas[i] * visibility;
            _texts[i].color = color;
        }
    }
}
