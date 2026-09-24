using DG.Tweening;
using UnityEngine;

public class ScaleTweeningFeedback : MonoBehaviour, IFeedback
{
    [Header("Click")]
    [SerializeField, Min(0f)] private float _clickPunchScale = 0.5f;
    [SerializeField, Min(0f)] private float _clickDuration = 0.5f;

    [Header("Promote")]
    [SerializeField, Min(0f)] private float _promotePunchScale = 1f;
    [SerializeField, Min(0f)] private float _promoteDuration = 1f;

    [Header("Background Theme")]
    [Tooltip("배경이 바뀔 때 움찔하는 크기입니다.")]
    [SerializeField, Min(0f)] private float _themeReactionPunchScale = 0.3f;
    [SerializeField, Min(0f)] private float _themeReactionDuration = 0.5f;

    [Tooltip("모두 한꺼번에 움찔하지 않도록 시작 시각을 흩뿌리는 폭입니다.")]
    [SerializeField, Min(0f)] private float _themeReactionSpread = 0.35f;

    [Header("Bump")]
    [Tooltip("장식장에서 슬라임끼리 부딪힐 때 찌그러지는 크기입니다.")]
    [SerializeField, Min(0f)] private float _bumpPunchScale = 0.35f;
    [SerializeField, Min(0f)] private float _bumpDuration = 0.35f;

    [Tooltip("이 속도로 부딪히면 위 크기가 그대로 나옵니다. 느릴수록 약해집니다.")]
    [SerializeField, Min(0.01f)] private float _bumpReferenceSpeed = 4f;

    [Tooltip("이보다 느리게 스치면 반응하지 않습니다. 붙어서 비비는 동안 계속 떨리는 것을 막습니다.")]
    [SerializeField, Min(0f)] private float _bumpMinimumSpeed = 0.6f;

    [Tooltip("한 번 반응한 뒤 이만큼은 다시 반응하지 않습니다. 연달아 부딪힐 때 찌그러짐이 겹치는 것을 막습니다.")]
    [SerializeField, Min(0f)] private float _bumpCooldown = 0.3f;

    [Header("Common")]
    [SerializeField, Min(1)] private int _vibrato = 10;
    [SerializeField, Range(0f, 1f)] private float _elasticity = 1f;

    private SlimeController _owner;
    private GameplaySpaceManager _spaceManager;
    private Tween _scaleTween;
    private Vector3 _defaultScale;
    private float _nextBumpTime;

    private void Awake()
    {
        _owner = GetComponent<SlimeController>();
        _defaultScale = transform.localScale;
    }

    private void OnEnable()
    {
        _owner.OnPromoted += PlayPromoteFeedback;
        _owner.OnBumped += PlayBumpReaction;

        // 풀에서 다시 나온 오브젝트가 이전 개체의 쿨다운을 물려받지 않게 한다.
        _nextBumpTime = 0f;

        // 풀에서 다시 꺼내질 때마다 매니저를 새로 잡는다. 씬이 바뀌면 인스턴스도
        // 바뀌므로 구독해 둔 것을 그대로 들고 있으면 안 된다.
        _spaceManager = GameplaySpaceManager.Instance;
        if (_spaceManager != null)
        {
            _spaceManager.BackgroundThemeChanged += PlayThemeReaction;
        }
    }

    // 역할 : 스케일 트위닝 피드백에 대한 로직을 담당
    public void Play(ClickInfo clickInfo)
    {
        PlayPunch(_clickPunchScale, _clickDuration);
    }

    private void OnDisable()
    {
        if (_owner != null)
        {
            _owner.OnPromoted -= PlayPromoteFeedback;
            _owner.OnBumped -= PlayBumpReaction;
        }

        if (_spaceManager != null)
        {
            _spaceManager.BackgroundThemeChanged -= PlayThemeReaction;
            _spaceManager = null;
        }

        // 비활성화 시 Tween 정리 (오브젝트 풀링 대응)
        CleanupTween();
    }

    private void OnDestroy()
    {
        // 파괴 시에도 안전하게 정리
        CleanupTween();
    }

    // 연출이 크기를 직접 다루기 전에 부른다. 돌고 있던 펀치를 멈추고 원래 크기로
    // 되돌린다.
    //
    // 이게 없으면 부딪힌 직후 연출에 들어간 슬라임이 두 주인을 갖는다. 연출이
    // 줄여 놓은 크기를 펀치가 끝나면서 제 기본값으로 덮어쓰고, 연출이 그때의
    // 부푼 크기를 "원래 크기"로 기억해 두면 끝난 뒤에도 그대로 남는다.
    public void StopAndReset()
    {
        CleanupTween();
    }

    private void PlayPromoteFeedback()
    {
        PlayPunch(_promotePunchScale, _promoteDuration);
    }

    // 배경이 바뀌면 필드에 나와 있는 슬라임이 한 번 움찔한다. 들고 있는 슬라임과
    // 아직 내려앉는 중인 슬라임은 건드리지 않는다. 장식장 쪽 슬라임도 제외된다.
    //
    // IsMainFieldActive는 쓰지 않는다. 전환 중에는 조작이 잠겨 있어 그 값이 false라,
    // 정작 배경이 바뀌는 순간에 모두가 걸러진다.
    private void PlayThemeReaction(EBackgroundTheme theme)
    {
        if (_owner == null ||
            _owner.IsDragging ||
            !_owner.HasLanded ||
            _owner.Location != ESlimeLocation.MainField)
        {
            return;
        }

        PlayPunch(
            _themeReactionPunchScale,
            _themeReactionDuration,
            Random.Range(0f, _themeReactionSpread));
    }

    // 세게 부딪힐수록 크게 찌그러진다. 기준 속도에서 1이 되도록 묶어 두지 않으면
    // 대포에 맞은 슬라임이 화면을 덮을 만큼 커진다.
    //
    // 여기서는 장식장인지 묻지 않는다. 슬라임끼리 부딪히는 일 자체가 장식장에서만
    // 일어나고, 공간을 다시 물으면 전환 중에 판정이 흔들린다.
    private void PlayBumpReaction(float impactSpeed)
    {
        if (_owner == null ||
            _owner.IsDragging ||
            impactSpeed < _bumpMinimumSpeed ||
            Time.time < _nextBumpTime)
        {
            return;
        }

        _nextBumpTime = Time.time + _bumpCooldown;

        float strength = Mathf.Clamp01(impactSpeed / _bumpReferenceSpeed);
        PlayPunch(_bumpPunchScale * strength, _bumpDuration);
    }

    private void PlayPunch(float punchScale, float duration, float delay = 0f)
    {
        CleanupTween();
        if (_owner == null) return;

        _scaleTween = _owner.transform
            .DOPunchScale(
                Vector3.one * punchScale,
                duration,
                _vibrato,
                _elasticity)
            .SetDelay(delay)
            .OnComplete(CompleteTween);
    }

    private void CompleteTween()
    {
        ResetScale();
        _scaleTween = null;
    }

    private void CleanupTween()
    {
        _scaleTween?.Kill();
        _scaleTween = null;
        ResetScale();
    }

    private void ResetScale()
    {
        if (_owner != null)
        {
            _owner.transform.localScale = _defaultScale;
        }
    }
}
