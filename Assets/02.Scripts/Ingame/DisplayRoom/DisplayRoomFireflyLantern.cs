using DG.Tweening;
using UnityEngine;

// 반딧불 랜턴. 평소에는 은은하게 숨 쉬듯 빛난다. 다가온 슬라임은 랜턴 안으로 쏙 들어가고, 랜턴이
// 환하게 빛나며 흔들린 뒤 하얀 빛을 두른 채 튀어나온다. 빛은 나온 뒤에도 한동안 슬라임을 따라다닌다.
public sealed class DisplayRoomFireflyLantern : PlaygroundDecoration
{
    [SerializeField] private SpriteRenderer _glow;
    [Tooltip("랜턴 그림입니다. 슬라임이 안에 있는 동안 흔들립니다.")]
    [SerializeField] private Transform _lanternBody;
    [Tooltip("나온 슬라임이 두르는 빛입니다.")]
    [SerializeField] private PlaygroundSlimeAttachment _aura;

    [Header("Idle")]
    [SerializeField] private Vector2 _idleGlowRange = new(0.18f, 0.34f);
    [SerializeField, Min(0.01f)] private float _idleBreathSpeed = 1.6f;

    [Header("Inside")]
    [SerializeField, Range(0f, 1f)] private float _brightGlowAlpha = 0.9f;
    [SerializeField, Min(0.2f)] private float _insideDuration = 1.4f;
    [SerializeField, Min(0f)] private float _jumpPower = 0.45f;
    [SerializeField, Min(0f)] private float _hopOutDistance = 0.9f;
    [Tooltip("랜턴에서 나온 뒤 빛을 두르고 있는 시간입니다(초).")]
    [SerializeField, Min(0.5f)] private float _auraDuration = 15f;

    private Vector3 _glowScale;
    private Vector3 _glowPosition;
    private Vector3 _bodyPosition;
    private float _idleTime;

    protected override void Awake()
    {
        base.Awake();
        if (!enabled) return;

        if (_glow == null || _lanternBody == null || _aura == null)
        {
            Debug.LogError("반딧불 랜턴의 빛, 랜턴 그림, 빛 두르기 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        _glowScale = _glow.transform.localScale;
        _glowPosition = _glow.transform.localPosition;
        _bodyPosition = _lanternBody.localPosition;
        ResetVisuals();
    }

    protected override void Update()
    {
        base.Update();
        if (!enabled || IsPlaying || _glow == null) return;

        // 평소: 숨 쉬듯 밝아졌다 어두워진다.
        _idleTime += Time.deltaTime;
        float breath = (Mathf.Sin(_idleTime * _idleBreathSpeed) + 1f) * .5f;
        SetAlpha(_glow, Mathf.Lerp(_idleGlowRange.x, _idleGlowRange.y, breath));
        _glow.transform.localScale = _glowScale * (1f + .05f * breath);
    }

    // 불빛은 랜턴 그림과 형제라 흔들림을 따라가지 않는다. 랜턴이 움직인 만큼 같이 옮긴다.
    private void LateUpdate()
    {
        if (_glow == null || _lanternBody == null) return;

        _glow.transform.localPosition = _glowPosition + (_lanternBody.localPosition - _bodyPosition);
    }

    protected override void Build(SlimeController slime, Sequence sequence)
    {
        Transform body = slime.transform;
        Vector3 scale = Hold.BaseScale;
        Vector3 center = _lanternBody.position;

        // 들어가기
        PlaySound(EAudioSfx.SlimeBounce);
        sequence.Append(body.DOMove(center, .3f).SetEase(Ease.InQuad));
        sequence.Join(body.DOScale(scale * .2f, .3f).SetEase(Ease.InQuad));
        sequence.AppendCallback(() =>
        {
            Hold.Hide();
            PlaySound(EAudioSfx.PlaygroundCannonLoad);
            // 안에 있는 동안 빛을 미리 입혀 둔다. 그림이 꺼진 동안 빛도 감춰지므로, 나오는 순간
            // 이미 빛나는 모습으로 튀어나온다.
            _aura.Follow(slime, _insideDuration + .5f + _auraDuration);
        });

        // 안에서: 랜턴이 환하게 빛나며 흔들린다.
        float inside = sequence.Duration();
        sequence.Insert(inside, _glow.DOFade(_brightGlowAlpha, .25f));
        sequence.Insert(inside, _glow.transform.DOScale(_glowScale * 1.35f, .35f).SetEase(Ease.OutBack));
        sequence.Insert(inside, _lanternBody.DOShakePosition(_insideDuration, new Vector3(.06f, .03f, 0f), 18, 90f, false, false));
        sequence.AppendInterval(_insideDuration);

        // 나오기: 빛을 두른 채 작게 튀어나와 원래 크기로 돌아가며 내려앉는다.
        Vector3 side = new(UnityEngine.Random.value < .5f ? -1f : 1f, UnityEngine.Random.Range(-.5f, .1f), 0f);
        Vector3 landing = ClampToPlayground(center + side.normalized * _hopOutDistance, transform.parent);
        sequence.AppendCallback(() =>
        {
            body.position = center;
            Hold.Show();
            PlaySound(EAudioSfx.SlimeBounce);
        });
        sequence.Append(body.DOJump(landing, _jumpPower, 1, .45f));
        sequence.Join(body.DOScale(scale, .3f).SetEase(Ease.OutBack));
        sequence.Join(_glow.DOFade(_idleGlowRange.y, .45f));
        sequence.Join(_glow.transform.DOScale(_glowScale, .45f));
    }

    protected override void ResetVisuals()
    {
        if (_glow != null)
        {
            _glow.DOKill();
            _glow.transform.DOKill();
            _glow.transform.localScale = _glowScale;
            SetAlpha(_glow, _idleGlowRange.x);
        }

        if (_lanternBody == null) return;
        _lanternBody.DOKill();
        _lanternBody.localPosition = _bodyPosition;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_aura != null) _aura.Stop();
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}
