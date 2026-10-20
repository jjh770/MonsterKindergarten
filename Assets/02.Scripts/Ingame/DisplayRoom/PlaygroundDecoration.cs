using DG.Tweening;
using UnityEngine;

// 슬라임이 다가오면 한 마리를 잠시 붙잡아 소품마다 다른 연출을 하는 장식 소품의 공통 뼈대다.
//
// 감지는 범퍼와 같은 트리거로 한다. 범퍼와 대포처럼 IsPlaygroundTarget으로 고르고, 막 떨어지는
// 중이거나 끌고 있는 슬라임은 고르지 않는다. 한 번 반응하면 쿨다운 동안 쉬어, 슬라임이 많아도
// 반응이 쉬지 않고 터지지 않는다.
//
// 연출은 하나의 Sequence로 만든다. 끝나면 붙잡은 슬라임을 되돌리고, 꺼지거나(공간 전환,
// 소품 놓기, 다시 세우기) 슬라임을 잃으면 연출을 끊고 되돌린다. 포인트나 생산에는 관여하지 않는다.
public abstract class PlaygroundDecoration : MonoBehaviour, IPlaygroundObject
{
    [Tooltip("슬라임을 알아채는 트리거입니다.")]
    [SerializeField] private Collider2D _sensor;
    [Tooltip("한 번 반응한 뒤 다시 반응할 때까지 쉬는 시간입니다(초).")]
    [SerializeField, Min(0f)] private float _cooldown = 4f;
    [Tooltip("소품 연출 소리의 크기입니다. 효과음 원래 크기에 곱합니다.")]
    [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.6f;

    protected readonly PlaygroundSlimeHold Hold = new();
    private Sequence _sequence;
    private bool _isInteractive = true;
    private bool _isReady;
    private float _nextTime;

    protected bool IsPlaying => _sequence != null;

    protected virtual void Awake()
    {
        if (_sensor == null || !_sensor.isTrigger)
        {
            Debug.LogError($"{name}의 감지 트리거가 비어 있거나 트리거가 아니에요.", this);
            enabled = false;
            return;
        }

        _isReady = true;
    }

    public void SetInteractive(bool isInteractive)
    {
        _isInteractive = isInteractive;
        if (_sensor != null) _sensor.enabled = isInteractive;
        if (!isInteractive) Cancel();
    }

    protected virtual void OnDisable()
    {
        Cancel();
        _nextTime = 0f;
    }

    protected virtual void Update()
    {
        if (IsPlaying && Hold.IsLost) Cancel();
    }

    private void OnTriggerStay2D(Collider2D other) => TryStart(other);

    private void TryStart(Collider2D other)
    {
        if (!_isReady || !enabled || !_isInteractive || IsPlaying ||
            !GameplayGate.IsActive || Time.time < _nextTime)
        {
            return;
        }

        SlimeController slime = other.GetComponentInParent<SlimeController>();
        if (slime == null || !slime.HasLanded || !CanReact(slime) || !Hold.TryBegin(slime)) return;

        _sequence = DOTween.Sequence();
        Build(slime, _sequence);
        _sequence.OnComplete(() =>
        {
            _sequence = null;
            Hold.Release();
            OnFinished();
            _nextTime = Time.time + _cooldown;
        });
    }

    // 연출을 중간에 끊는다. 붙잡은 슬라임을 지금 자리에서 놓아 주고 소품 모양을 되돌린다.
    protected void Cancel()
    {
        bool wasPlaying = IsPlaying || Hold.IsHolding;
        _sequence?.Kill();
        _sequence = null;
        Hold.Release();
        ResetVisuals();
        if (wasPlaying) _nextTime = Time.time + _cooldown;
    }

    // 이 슬라임에 반응할지 소품이 따로 정할 때 쓴다.
    protected virtual bool CanReact(SlimeController slime) => true;

    // 연출을 sequence에 쌓는다. 슬라임은 이미 붙잡혀 있다. 연출 중간에 Hold.Release()를 불러
    // 일찍 놓아 줄 수도 있다.
    protected abstract void Build(SlimeController slime, Sequence sequence);

    // 연출이 끝까지 간 뒤에 부른다.
    protected virtual void OnFinished() => ResetVisuals();

    // 소품 쪽 모양(흔들림, 크기, 빛)을 처음 상태로 되돌린다.
    protected abstract void ResetVisuals();

    // from에서 왼쪽이나 오른쪽으로 distance만큼 튀어 내려앉을 자리다. 위아래 기울기는
    // verticalRange 안에서 고르고, 놀이터 밖으로 나가지 않게 당긴다.
    protected Vector3 PickLandingBeside(Vector3 from, float distance, Vector2 verticalRange)
    {
        Vector3 side = new(Random.value < .5f ? -1f : 1f, Random.Range(verticalRange.x, verticalRange.y), 0f);
        return ClampToPlayground(from + side.normalized * distance, transform.parent);
    }

    // 월드 위치를 놀이터 안쪽으로 당긴다. room은 놓인 소품들의 부모(놀이터 좌표계)다.
    private static Vector3 ClampToPlayground(Vector3 world, Transform room)
    {
        if (room == null) return world;

        Vector3 local = room.InverseTransformPoint(world);
        local.x = PlaygroundRules.ClampX(local.x);
        local.y = PlaygroundRules.ClampY(local.y);
        return room.TransformPoint(local);
    }

    protected void PlaySound(EAudioSfx cue) =>
        AudioManager.Instance?.PlaySFXRandomPitch(cue, volumeScale: _soundVolume);
}
