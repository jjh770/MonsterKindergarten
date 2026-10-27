using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 뽑기 버튼을 고르는 동안 통을 잡고 좌우로 흔들게 한다. 기계 오브젝트(GachaMachine)에 붙는다.
//
// 통은 손가락을 용수철처럼 따라가고, 놓으면 출렁이다 제자리로 돌아온다. 그 가속도와 기울기가 통 안
// 캡슐 더미에 관성으로 전해지고, 세게 흔들면 캡슐이 튀어 오른다. 한동안 잡지 않으면 통이 혼자 부르르
// 떨어 잡을 수 있다는 것을 알린다.
//
// 드래그는 기계 그림 아래에서만 받는다. 이 오브젝트 아래에는 버튼이 없어서, 드래그 처리기가 버튼의
// 탭을 취소하는 문제가 생기지 않는다. 결과 캡슐 버튼은 형제인 캡슐 층에 있다. 기계의 CanvasGroup은
// 평소 터치를 막지 않게 되어 있어서, 잡을 수 있는 동안에만 터치를 받게 켠다.
//
// 통이 세게 흔들리는 동안에는 캡슐 섞이는 소리(GachaShake)를 반복하고, 흔들림이 잠깐 멎으면 줄여 끈다.
// 반복 효과음 자리는 배출 때의 대기음과 같이 쓰므로, 이 컴포넌트가 켠 소리만 끈다. 잡기는 배출 전에
// 끝나므로 두 소리가 겹치지 않는다.
//
// 직접 끌어 흔드는 동안에는 몸체 그림을 어지러워하는 얼굴(_dizzySprite)로 바꾸고, 그 흔들림이 멎으면
// 원래 그림으로 돌린다. 누르기만 하거나 혼자 떠는 안내 떨림에는 바꾸지 않는다.
//
// 잡는 동안에는 이 컴포넌트가 기계의 위치와 기울기를 쓴다. 나타나기와 배출 같은 다른 연출은 잡기가
// 꺼진 뒤에만 기계를 움직인다.
public sealed class GachaMachineGrab : MonoBehaviour,
    IInitializePotentialDragHandler,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [SerializeField] private RectTransform _machineRoot;
    [SerializeField] private CanvasGroup _machineGroup;
    [SerializeField] private GachaCapsulePileView _pile;
    [Tooltip("통을 잡고 좌우로 끌 수 있는 거리입니다. 끝에 가까울수록 고무줄처럼 버팁니다.")]
    [SerializeField, Min(1f)] private float _range = 150f;
    [Tooltip("통의 가속도가 캡슐에 전해지는 비율입니다. 클수록 캡슐이 세게 쏠립니다.")]
    [SerializeField, Min(0f)] private float _inertia = 0.6f;
    [Tooltip("통을 세게 흔들 때 캡슐이 튀어 오르는 정도입니다. 통이 받은 충격에 곱합니다.")]
    [SerializeField, Min(0f)] private float _rattle = 0.1f;
    [Tooltip("통이 움직일 때 기울어지는 최대 각도입니다.")]
    [SerializeField, Range(0f, 20f)] private float _maxTilt = 12f;
    [Tooltip("통을 이만큼 잡지 않으면 혼자 떨어서 잡을 수 있다고 알립니다. 초 단위입니다.")]
    [SerializeField, Min(0.5f)] private float _hintDelay = 4f;
    [Tooltip("혼자 떨 때 좌우로 움직이는 거리입니다.")]
    [SerializeField, Min(0f)] private float _hintAmplitude = 18f;
    [Tooltip("기계 몸체 그림입니다. 직접 끌어 흔드는 동안 그림을 바꿉니다.")]
    [SerializeField] private Image _body;
    [Tooltip("직접 끌어 흔드는 동안 몸체에 쓰는 그림입니다. 원래 그림과 크기와 배치가 같아야 합니다.")]
    [SerializeField] private Sprite _dizzySprite;

    // 통의 용수철. 잡았을 때와 돌아갈 때는 바로 따라가고, 놓았을 때만 덜 감쇠해서 출렁인다.
    private const float StepSeconds = 1f / 120f;
    private const float GrabStiffness = 700f;
    private const float ReleaseStiffness = 220f;
    private const float ReleaseDampingRatio = 0.18f;
    private const float ReturnStiffness = 500f;
    private const float ReturnTimeoutSeconds = 0.45f;
    // 이만큼 가까이, 이만큼 느리게 움직이면 멈춘 것으로 본다.
    private const float RestDistance = 0.5f;
    private const float RestSpeed = 5f;
    // 손가락이 이만큼 움직여야 끌기로 본다. 누르기만 해서는 그림을 바꾸지 않는다.
    private const float DragStartDistance = 4f;
    // 속도 1단위/초마다 기우는 각도. 움직이는 쪽 반대로 윗부분이 처진다.
    private const float TiltPerSpeed = 0.008f;
    private const float MaxFrameAcceleration = 20000f;
    // 통이 받은 충격이 이보다 크면 캡슐을 튀겨 올린다. 너무 자주 튀기지 않게 간격을 둔다.
    private const float RattleThreshold = 2500f;
    private const float RattleInterval = 0.05f;
    // 흔들림이 이만큼 멎어 있으면 섞이는 소리를 줄여 끈다. 좌우로 흔들 때 방향이 바뀌는 순간마다
    // 끊기지 않게 조금 기다린다.
    private const float ShakeSoundHoldSeconds = 0.25f;
    private const float ShakeSoundFadeSeconds = 0.2f;
    private const float HintSeconds = 0.55f;
    private const float HintFrequency = 7f;

    // _shakeX는 제자리에서 옆으로 벗어난 거리다.
    private bool _active;
    private bool _grabbable;
    private bool _grabbing;
    private bool _returning;
    private int _pointerId;
    private float _startLocalX;
    private float _startOffset;
    private float _target;
    private float _shakeX;
    private float _velocity;
    private float _acceleration;
    private float _clock;
    private float _rattleClock;
    private bool _isShakeSoundPlaying;
    private float _shakeQuietTime;
    private float _idleTime;
    private float _hintElapsed = -1f;
    private Vector2 _rest;
    private Sprite _bodySprite;
    private bool _dizzy;
    private bool _isReady;

    private void Awake()
    {
        if (_machineRoot == null || _machineGroup == null || _pile == null ||
            _body == null || _dizzySprite == null)
        {
            Debug.LogError("뽑기 통 잡기의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _rest = _machineRoot.anchoredPosition;
        _bodySprite = _body.sprite;
        _isReady = true;
    }

    private void Update()
    {
        if (!_isReady || !_active) return;

        Step(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
    }

    // 버튼을 고르는 동안 통을 잡을 수 있게 한다. 기계가 다 나타난 뒤에 켜야 한다. 나타나는 연출도
    // 기계의 위치를 움직이기 때문이다.
    public void SetGrabbable(bool grabbable)
    {
        if (!_isReady) return;

        _machineGroup.blocksRaycasts = grabbable;
        if (!grabbable)
        {
            _grabbable = false;
            _grabbing = false;
            _hintElapsed = -1f;
            return;
        }

        _active = true;
        _returning = false;
        _grabbable = true;
        _idleTime = 0f;
        _hintElapsed = -1f;
    }

    // 잡기를 끝내고 통을 제자리로 돌려 세운다. 티켓이 날아갈 투입구와 배출구는 통이 제자리에
    // 있다고 보고 계산하므로, 뽑기 연출은 이것이 끝난 뒤에 시작해야 한다.
    public async UniTask<bool> ReturnToRest(CancellationToken token)
    {
        if (!_isReady || !_active) return false;

        SetGrabbable(false);
        _returning = true;
        float waited = 0f;
        while (waited < ReturnTimeoutSeconds && !IsAtRest())
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
            {
                return true;
            }

            waited += Time.unscaledDeltaTime;
        }

        Stop();
        return false;
    }

    // 잡기를 바로 멈추고 통을 제자리에 둔다. 새 연출을 준비하거나 화면을 닫을 때 부른다.
    public void Stop()
    {
        if (!_isReady) return;

        _active = false;
        _grabbable = false;
        _grabbing = false;
        _returning = false;
        _hintElapsed = -1f;
        _idleTime = 0f;
        _shakeX = 0f;
        _velocity = 0f;
        _acceleration = 0f;
        _clock = 0f;
        _rattleClock = 0f;
        _machineRoot.anchoredPosition = _rest;
        _machineRoot.localRotation = Quaternion.identity;
        _machineGroup.blocksRaycasts = false;
        _pile.SetFrameAcceleration(Vector2.zero);
        SetDizzy(false);
        StopShakeSound(0f);
    }

    private void OnDisable()
    {
        StopShakeSound(0f);
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // 누르자마자 따라오게 한다. 이 아래에는 탭을 받을 버튼이 없다.
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_grabbable || _grabbing) return;
        if (!TryGetLocalX(eventData, out float localX)) return;

        _grabbing = true;
        _pointerId = eventData.pointerId;
        _startLocalX = localX;
        _startOffset = _shakeX;
        _target = _shakeX;
        _idleTime = 0f;
        _hintElapsed = -1f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_grabbing || eventData.pointerId != _pointerId) return;
        if (!TryGetLocalX(eventData, out float localX)) return;

        if (!_dizzy && Mathf.Abs(localX - _startLocalX) >= DragStartDistance) SetDizzy(true);

        // 끝에 가까울수록 고무줄처럼 버틴다.
        float raw = _startOffset + (localX - _startLocalX);
        _target = _range * (float)Math.Tanh(raw / _range);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_grabbing || eventData.pointerId != _pointerId) return;

        _grabbing = false;
        _idleTime = 0f;
    }

    private bool TryGetLocalX(PointerEventData eventData, out float localX)
    {
        localX = 0f;
        if (_machineRoot.parent is not RectTransform parent) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            return false;
        }

        localX = local.x;
        return true;
    }

    // 통이 향하는 자리를 정하고 용수철로 따라가게 한 뒤, 그 가속도와 기울기를 통 안 캡슐에 넘긴다.
    private void Step(float delta)
    {
        float target = GetTarget(delta);
        float stiffness;
        float dampingRatio = 1f;
        if (_returning)
        {
            stiffness = ReturnStiffness;
        }
        else if (_grabbing || _hintElapsed >= 0f)
        {
            stiffness = GrabStiffness;
        }
        else
        {
            stiffness = ReleaseStiffness;
            dampingRatio = ReleaseDampingRatio;
        }

        float damping = 2f * Mathf.Sqrt(stiffness) * dampingRatio;
        _clock += delta;
        while (_clock >= StepSeconds)
        {
            _clock -= StepSeconds;
            _acceleration = stiffness * (target - _shakeX) - damping * _velocity;
            _velocity += _acceleration * StepSeconds;
            _shakeX += _velocity * StepSeconds;
        }

        // 끌어서 생긴 흔들림이 멎으면 원래 그림으로 돌린다.
        if (_dizzy && !_grabbing && IsAtRest()) SetDizzy(false);

        float tilt = Mathf.Clamp(_velocity * TiltPerSpeed, -_maxTilt, _maxTilt);
        _machineRoot.anchoredPosition = _rest + new Vector2(_shakeX, 0f);
        _machineRoot.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // 통이 오른쪽으로 가속하면 캡슐은 왼쪽으로 밀린다. 통이 기울면 통 안에서 본 중력이 비스듬해진다.
        float radians = tilt * Mathf.Deg2Rad;
        float gravity = _pile.Gravity;
        Vector2 frame = new(
            -_acceleration * _inertia - gravity * Mathf.Sin(radians),
            gravity * (1f - Mathf.Cos(radians)));
        _pile.SetFrameAcceleration(Vector2.ClampMagnitude(frame, MaxFrameAcceleration));

        // 세게 흔들면 캡슐이 위로 튀어 오른다. 옆으로 미는 힘만으로는 바닥을 따라 미끄러지기만 한다.
        float jolt = Mathf.Abs(_acceleration) * _inertia;
        _rattleClock += delta;
        bool isRattling = !_returning && jolt > RattleThreshold;
        if (isRattling && _rattleClock >= RattleInterval)
        {
            _rattleClock = 0f;
            _pile.Kick(jolt * _rattle);
        }

        UpdateShakeSound(isRattling, delta);
    }

    private void UpdateShakeSound(bool isRattling, float delta)
    {
        if (isRattling)
        {
            _shakeQuietTime = 0f;
            if (_isShakeSoundPlaying) return;

            _isShakeSoundPlaying = true;
            AudioManager.Instance?.PlayLoopingSFX(EAudioSfx.GachaShake);
            return;
        }

        if (!_isShakeSoundPlaying) return;

        _shakeQuietTime += delta;
        if (_shakeQuietTime >= ShakeSoundHoldSeconds) StopShakeSound(ShakeSoundFadeSeconds);
    }

    private void StopShakeSound(float fadeSeconds)
    {
        if (!_isShakeSoundPlaying) return;

        _isShakeSoundPlaying = false;
        _shakeQuietTime = 0f;
        AudioManager.Instance?.StopLoopingSFX(fadeSeconds);
    }

    private bool IsAtRest()
    {
        return Mathf.Abs(_shakeX) <= RestDistance && Mathf.Abs(_velocity) <= RestSpeed;
    }

    private void SetDizzy(bool dizzy)
    {
        _dizzy = dizzy;
        _body.sprite = dizzy ? _dizzySprite : _bodySprite;
    }

    // 잡고 있으면 손가락, 돌아가는 중이면 제자리다. 그 밖에는 한동안 잡지 않을 때 잠깐 부르르 떤다.
    private float GetTarget(float delta)
    {
        if (_grabbing) return _target;
        if (_returning || !_grabbable) return 0f;

        if (_hintElapsed >= 0f)
        {
            _hintElapsed += delta;
            float ratio = _hintElapsed / HintSeconds;
            if (ratio < 1f)
            {
                return Mathf.Sin(_hintElapsed * HintFrequency * Mathf.PI * 2f) *
                       _hintAmplitude * (1f - ratio);
            }

            _hintElapsed = -1f;
            _idleTime = 0f;
            return 0f;
        }

        _idleTime += delta;
        if (_idleTime >= _hintDelay) _hintElapsed = 0f;
        return 0f;
    }
}
