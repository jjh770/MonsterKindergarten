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
    [Tooltip("기준 속도로 부딪혔을 때 말랑한 반응의 세기입니다. 1이면 탭을 놓을 때와 같은 크기입니다.")]
    [SerializeField, Range(0f, 1f)] private float _bumpStrength = 0.6f;

    [Tooltip("이 속도로 부딪히면 위 세기가 그대로 나옵니다. 느릴수록 약해집니다.")]
    [SerializeField, Min(0.01f)] private float _bumpReferenceSpeed = 4f;

    [Tooltip("이보다 느리게 스치면 반응하지 않습니다. 붙어서 비비는 동안 계속 떨리는 것을 막습니다.")]
    [SerializeField, Min(0f)] private float _bumpMinimumSpeed = 0.6f;

    [Tooltip("한 번 반응한 뒤 이만큼은 다시 반응하지 않습니다. 연달아 부딪힐 때 찌그러짐이 겹치는 것을 막습니다.")]
    [SerializeField, Min(0f)] private float _bumpCooldown = 0.3f;

    [Header("Press")]
    [Tooltip("누르는 순간 납작하게 눌리는 정도입니다. 가로는 늘고 세로는 줄어 부피가 비슷하게 유지됩니다.")]
    [SerializeField, Range(1f, 2f)] private float _pressWidthScale = 1.35f;
    [SerializeField, Range(0.3f, 1f)] private float _pressHeightScale = 0.6f;
    [SerializeField, Min(0.01f)] private float _pressDuration = 0.06f;

    [Header("Release")]
    [Tooltip("손을 뗄 때 눌린 상태에서 위로 길쭉하게 튀는 정도입니다.")]
    [SerializeField, Range(0.3f, 1f)] private float _releaseWidthScale = 0.75f;
    [SerializeField, Range(1f, 2f)] private float _releaseHeightScale = 1.45f;
    [Tooltip("튀어 오른 뒤 한 번 더 가볍게 눌렸다가 돌아오는 정도입니다.")]
    [SerializeField, Range(0f, 0.4f)] private float _releaseSettleAmount = 0.15f;
    [SerializeField, Min(0.01f)] private float _releaseStepDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float _releaseReturnDuration = 0.3f;

    [Header("Drag")]
    [Tooltip("끌 때 움직이는 방향으로 늘어나는 최대 정도입니다. 잡고만 있을 때는 눌린 모양을 유지하고, 움직일수록 이 모양에 가까워집니다.")]
    [SerializeField, Range(0f, 0.6f)] private float _dragStretch = 0.25f;
    [Tooltip("이 속도(월드 거리/초)에서 늘어남이 최대가 됩니다.")]
    [SerializeField, Min(0.1f)] private float _dragReferenceSpeed = 8f;
    [SerializeField, Min(0.01f)] private float _dragSmoothTime = 0.08f;
    [Tooltip("드래그를 놓았을 때 원래 모양으로 돌아오는 시간입니다. 출렁임 없이 부드럽게 돌아옵니다.")]
    [SerializeField, Min(0.01f)] private float _dragReleaseDuration = 0.15f;

    [Header("Common")]
    [SerializeField, Min(1)] private int _vibrato = 10;
    [SerializeField, Range(0f, 1f)] private float _elasticity = 1f;

    private SlimeController _owner;
    private GameplaySpaceManager _spaceManager;
    private Tween _scaleTween;
    private Vector3 _defaultScale;
    private float _nextBumpTime;
    private bool _isDragStretching;
    private Vector3 _lastPosition;
    private Vector2 _stretch = Vector2.one;
    private Vector2 _stretchVelocity;

    private void Awake()
    {
        _owner = GetComponent<SlimeController>();
        _defaultScale = transform.localScale;
    }

    private void OnEnable()
    {
        _owner.OnPromoted += PlayPromoteFeedback;
        _owner.OnBumped += PlayBumpReaction;
        _owner.OnPressed += HandlePressed;
        _owner.OnPressReleased += HandlePressReleased;
        _owner.OnDragStarted += HandleDragStarted;
        _owner.OnDragEnded += HandleDragEnded;

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
        // 손으로 누른 클릭은 누름과 뗌 이벤트가 이미 모양을 움직였다. 자동 생산만 펀치로 알린다.
        if (clickInfo.ClickType == EClickType.Manual)
        {
            return;
        }

        PlayPunch(_clickPunchScale, _clickDuration);
    }

    private void OnDisable()
    {
        if (_owner != null)
        {
            _owner.OnPromoted -= PlayPromoteFeedback;
            _owner.OnBumped -= PlayBumpReaction;
            _owner.OnPressed -= HandlePressed;
            _owner.OnPressReleased -= HandlePressReleased;
            _owner.OnDragStarted -= HandleDragStarted;
            _owner.OnDragEnded -= HandleDragEnded;
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
        _isDragStretching = false;
        CleanupTween();
    }

    // 메인 필드에서 손으로 잡는 슬라임만 말랑하게 반응한다. 장식장 슬라임은 부딪힘 반응이 따로 있다.
    private bool CanSquish => _owner != null && _owner.IsMainFieldActive;

    private void HandlePressed()
    {
        if (!CanSquish) return;

        _scaleTween?.Kill();
        _scaleTween = _owner.transform
            .DOScale(Scaled(_pressWidthScale, _pressHeightScale), _pressDuration)
            .SetEase(Ease.OutQuad);
    }

    private void HandlePressReleased()
    {
        if (!CanSquish)
        {
            CleanupTween();
            return;
        }

        PlayJelly();
    }

    private void HandleDragStarted()
    {
        if (!CanSquish) return;

        _scaleTween?.Kill();
        _scaleTween = null;
        _isDragStretching = true;
        _lastPosition = _owner.transform.position;
        _stretchVelocity = Vector2.zero;
        // 눌려 있던 모양에서 이어서 시작해 드래그로 넘어가는 순간 튀지 않게 한다.
        Vector3 ratio = _owner.transform.localScale;
        _stretch = new Vector2(
            ratio.x / _defaultScale.x,
            ratio.y / _defaultScale.y);
    }

    private void HandleDragEnded()
    {
        if (!_isDragStretching) return;

        _isDragStretching = false;
        _scaleTween?.Kill();
        if (!CanSquish)
        {
            CleanupTween();
            return;
        }

        // 끄는 동안의 모양에서 출렁임 없이 그대로 돌아온다. 출렁임은 탭을 놓을 때만 쓴다.
        _scaleTween = _owner.transform
            .DOScale(_defaultScale, _dragReleaseDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(CompleteTween);
    }

    // 끄는 동안 움직이는 방향으로 늘린다. 위치는 건드리지 않고 크기만 바꾼다.
    private void LateUpdate()
    {
        if (!_isDragStretching || _owner == null) return;

        Vector3 position = _owner.transform.position;
        float deltaTime = Time.deltaTime;
        Vector2 velocity = deltaTime > 0f
            ? (Vector2)(position - _lastPosition) / deltaTime
            : Vector2.zero;
        _lastPosition = position;

        float horizontal = Mathf.Clamp01(Mathf.Abs(velocity.x) / _dragReferenceSpeed);
        float vertical = Mathf.Clamp01(Mathf.Abs(velocity.y) / _dragReferenceSpeed);
        // 한쪽으로 늘어난 만큼 반대쪽은 조금 줄여 부피가 비슷해 보이게 한다.
        var stretched = new Vector2(
            1f + _dragStretch * (horizontal - 0.6f * vertical),
            1f + _dragStretch * (vertical - 0.6f * horizontal));
        // 잡고만 있을 때는 눌린 모양을 유지하고, 움직이는 만큼만 늘어난 모양으로 옮겨 간다.
        var pressed = new Vector2(_pressWidthScale, _pressHeightScale);
        float movement = Mathf.Max(horizontal, vertical);
        Vector2 target = Vector2.Lerp(pressed, stretched, movement);
        _stretch = Vector2.SmoothDamp(_stretch, target, ref _stretchVelocity, _dragSmoothTime);
        _owner.transform.localScale = Scaled(_stretch.x, _stretch.y);
    }

    // 눌려 있던(또는 늘어나 있던) 현재 모양에서 위로 길쭉하게 튀었다가, 가볍게 눌렸다 돌아온다.
    // strength가 1이면 탭을 놓을 때와 같은 크기이고, 작을수록 원래 모양에 가깝게 움직인다.
    // squashFirst는 눌려 있지 않은 상태에서 시작할 때(부딪힘) 먼저 납작하게 찌그러뜨린다.
    private void PlayJelly(float strength = 1f, bool squashFirst = false)
    {
        if (_owner == null) return;

        if (squashFirst)
        {
            CleanupTween();
        }
        else
        {
            _scaleTween?.Kill();
        }

        float settle = _releaseSettleAmount * strength;
        Sequence sequence = DOTween.Sequence();
        if (squashFirst)
        {
            sequence.Append(_owner.transform
                .DOScale(
                    Scaled(
                        Mathf.Lerp(1f, _pressWidthScale, strength),
                        Mathf.Lerp(1f, _pressHeightScale, strength)),
                    _pressDuration)
                .SetEase(Ease.OutQuad));
        }

        sequence.Append(_owner.transform
            .DOScale(
                Scaled(
                    Mathf.Lerp(1f, _releaseWidthScale, strength),
                    Mathf.Lerp(1f, _releaseHeightScale, strength)),
                _releaseStepDuration)
            .SetEase(Ease.OutQuad));
        sequence.Append(_owner.transform
            .DOScale(Scaled(1f + settle, 1f - settle), _releaseStepDuration)
            .SetEase(Ease.InOutQuad));
        sequence.Append(_owner.transform
            .DOScale(_defaultScale, _releaseReturnDuration)
            .SetEase(Ease.OutSine));
        sequence.OnComplete(CompleteTween);
        _scaleTween = sequence;
    }

    private Vector3 Scaled(float width, float height)
    {
        return new Vector3(
            _defaultScale.x * width,
            _defaultScale.y * height,
            _defaultScale.z);
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

        float strength = Mathf.Clamp01(impactSpeed / _bumpReferenceSpeed) * _bumpStrength;
        PlayJelly(strength, squashFirst: true);
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
