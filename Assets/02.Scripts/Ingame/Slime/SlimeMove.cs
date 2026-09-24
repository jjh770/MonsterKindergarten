using DG.Tweening;
using System.Collections;
using UnityEngine;

public class SlimeMove : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 1f;
    [SerializeField] private Ease _moveEase;
    [SerializeField] private float _minMoveDuration = 0.5f;
    [SerializeField] private float _maxMoveDuration = 3f;
    [SerializeField] private float _minIdleDuration = 0.3f;
    [SerializeField] private float _maxIdleDuration = 2f;
    [SerializeField] private float _interactionIdleDuration = 2f;

    [Header("External Launch")]
    [Tooltip("밖에서 밀려난 뒤 속도가 이 값 아래로 떨어지면 평소 이동으로 돌아갑니다.")]
    [SerializeField, Min(0.01f)] private float _launchRecoverSpeed = 0.6f;
    [Tooltip("밀려나 있는 동안의 감쇠입니다. 물리 머티리얼의 탄성이 1이라 0이면 멈추지 않습니다.")]
    [SerializeField, Min(0f)] private float _launchDamping = 0.6f;

    [Tooltip("좌우 속도가 이 값을 넘어야 바라보는 쪽을 바꿉니다. 0이면 거의 멈춘 상태에서도 계속 뒤집힙니다.")]
    [SerializeField, Min(0f)] private float _facingDeadzone = 0.15f;

    [Tooltip("슬라임끼리 이 속도 이상으로 부딪히면 결과를 물리에 맡깁니다. 당구처럼 맞은 쪽이 충돌량만큼 밀려납니다.")]
    [SerializeField, Min(0f)] private float _collisionDriveMinimumSpeed = 0.8f;

    private Rigidbody2D _rb;
    private Vector2 _lastVelocity;
    private SlimeController _slime;
    private Vector3 _rightVector = Vector3.zero;
    private Vector3 _leftVector = new Vector3(0, 180, 0);
    private Coroutine _moveCoroutine;
    private bool _isInteractionIdle;
    private bool _isMovementLocked;
    private bool _isLaunched;
    private float _defaultDamping;
    private bool _isFacingRight = true;

    // 밀려나는 동안은 평소 이동이 물러나 있다. 놀이터 오브젝트가 이 값을 보고
    // 같은 슬라임을 연달아 밀지 판단한다.
    public bool IsLaunched => _isLaunched;

    private void Awake()
    {
        _slime = GetComponent<SlimeController>();
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
        {
            _defaultDamping = _rb.linearDamping;
        }
    }

    private void OnEnable()
    {
        _slime.OnInteracted += OnInteracted;

        // 풀에서 나온 오브젝트는 이전 개체가 보던 쪽을 그대로 달고 온다.
        // 캐시와 실제 회전이 어긋나면 그쪽으로 다시는 돌지 않으므로 맞춰 둔다.
        float yaw = transform.localEulerAngles.y;
        _isFacingRight = yaw < 90f || yaw > 270f;

        _moveCoroutine = StartCoroutine(MoveRoutine());
    }

    private void OnDisable()
    {
        if (_slime != null)
        {
            _slime.OnInteracted -= OnInteracted;
        }

        StopMoveCoroutine();

        // 풀에서 같은 오브젝트가 다시 나온다. 감쇠를 되돌리지 않으면 다음 슬라임이
        // 밀림용 감쇠를 그대로 물려받아 평소 이동이 무거워진다.
        ClearLaunch();

        _isInteractionIdle = false;
        _isMovementLocked = false;
        _lastVelocity = Vector2.zero;

        if (_rb != null)
        {
            _rb.DOKill();
            _rb.linearVelocity = Vector2.zero;
        }

        transform.DOKill();
    }

    private void OnInteracted()
    {
        // 이동 중단하고 Idle 상태로 전환
        _isInteractionIdle = true;
        _lastVelocity = Vector2.zero;
        StopMoveCoroutine();
        ClearLaunch();
        _rb.DOKill();
        _rb.linearVelocity = Vector2.zero;

        if (_isMovementLocked) return;

        _moveCoroutine = StartCoroutine(IdleThenMoveRoutine());
    }

    public void SetMovementLocked(bool isLocked)
    {
        if (_isMovementLocked == isLocked) return;

        _isMovementLocked = isLocked;
        _isInteractionIdle = false;
        _lastVelocity = Vector2.zero;
        StopMoveCoroutine();
        ClearLaunch();

        if (_rb != null)
        {
            _rb.DOKill();
            _rb.linearVelocity = Vector2.zero;
        }

        if (!isLocked && isActiveAndEnabled)
        {
            _moveCoroutine = StartCoroutine(MoveRoutine());
        }
    }

    // 대포나 범퍼처럼 밖에서 슬라임을 날릴 때 쓴다.
    // 평소 이동은 0.3초마다 속도를 제 목표로 되돌리고 충돌에서 배회 속도로
    // 덮어쓰므로, 힘을 주는 것만으로는 그 자리에서 지워진다. 그래서 미는 동안에는
    // 이동 루틴을 세우고 물리에 맡긴다.
    //
    // 속도를 0으로 만든 뒤 넣으므로 넘긴 값이 그대로 출발 속도가 된다.
    // 오브젝트마다 세기를 인스펙터 값으로 그대로 읽을 수 있게 하려는 것이다.
    public bool Launch(Vector2 velocity)
    {
        if (velocity == Vector2.zero || !TakeOverByPhysics()) return false;

        _rb.linearVelocity = velocity;
        _lastVelocity = velocity;
        RotateSlime(velocity);
        return true;
    }

    // 속도를 정하지 않고 물리에 넘기기만 한다. 부딪힌 결과를 물리가 계산한 그대로
    // 쓰려면 여기서 속도를 건드리면 안 된다. 그게 당구의 조건이다.
    //
    // 평소 이동 루틴이 물러나므로, 밀려난 슬라임이 0.3초 만에 제 갈 길로 돌아가
    // 전달받은 운동량을 잃는 일이 없다.
    private bool TakeOverByPhysics()
    {
        if (_rb == null ||
            _isMovementLocked ||
            !isActiveAndEnabled ||
            (_slime != null && _slime.IsDragging))
        {
            return false;
        }

        StopMoveCoroutine();
        _isInteractionIdle = false;
        _rb.DOKill();

        _isLaunched = true;
        _rb.linearDamping = _launchDamping;
        return true;
    }

    // 밀림 상태만 거둔다. 평소 이동을 다시 시작할지는 부르는 쪽이 정한다.
    private void ClearLaunch()
    {
        if (!_isLaunched) return;

        _isLaunched = false;
        if (_rb != null)
        {
            _rb.linearDamping = _defaultDamping;
        }
    }

    private void EndLaunch()
    {
        ClearLaunch();

        _lastVelocity = Vector2.zero;
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
        }

        if (!isActiveAndEnabled || _isMovementLocked) return;

        StopMoveCoroutine();
        _moveCoroutine = StartCoroutine(MoveRoutine());
    }

    private IEnumerator IdleThenMoveRoutine()
    {
        // 상호작용 후 2초 Idle
        yield return new WaitForSeconds(_interactionIdleDuration);
        _isInteractionIdle = false;
        _moveCoroutine = StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        while (true)
        {
            if (_slime.IsDragging)
            {
                _rb.linearVelocity = Vector2.zero;
                yield return null;
                continue;
            }

            float idleDuration = Random.Range(_minIdleDuration, _maxIdleDuration);
            yield return new WaitForSeconds(idleDuration);

            if (_slime.IsDragging) continue;

            // 랜덤 방향 계산
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector2 targetVelocity = randomDirection * _moveSpeed;
            RotateSlime(randomDirection);

            // 부드럽게 가속
            _rb.DOKill();
            DOTween.To(
                () => _rb.linearVelocity,
                x => _rb.linearVelocity = x,
                targetVelocity,
                0.3f
            ).SetEase(_moveEase).SetTarget(_rb);

            // 랜덤 시간 동안 이동
            float moveDuration = Random.Range(_minMoveDuration, _maxMoveDuration);
            yield return new WaitForSeconds(moveDuration);

            if (_slime.IsDragging) continue;

            // 부드럽게 감속
            _rb.DOKill();
            DOTween.To(
                () => _rb.linearVelocity,
                x => _rb.linearVelocity = x,
                Vector2.zero,
                0.3f
            ).SetEase(_moveEase).SetTarget(_rb);

            yield return new WaitForSeconds(0.3f);
        }
    }

    private void FixedUpdate()
    {
        if (_isMovementLocked)
        {
            _lastVelocity = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_isLaunched)
        {
            Vector2 velocity = _rb.linearVelocity;
            if (velocity.magnitude <= _launchRecoverSpeed)
            {
                EndLaunch();
                return;
            }

            _lastVelocity = velocity;
            RotateSlime(velocity);
            return;
        }

        if (_rb.linearVelocity.magnitude > 0.1f)
        {
            _lastVelocity = _rb.linearVelocity;
        }
    }

    private void OnCollisionEnter2D(Collision2D coll)
    {
        // 상호작용 대기 또는 드래그 중에는 이전 속도로 다시 튕기지 않도록 한다.
        if (_isMovementLocked || _isInteractionIdle || (_slime != null && _slime.IsDragging))
        {
            _lastVelocity = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        // 밀려나는 동안에는 물리가 튕긴 결과를 그대로 둔다. 아래 반사식은 속도를
        // 배회 속도까지 끌어내리므로, 벽에 한 번만 닿아도 날아가던 기세가 사라진다.
        // 물리 머티리얼의 탄성이 1이라 반사 자체는 물리가 알아서 한다.
        if (_isLaunched) return;

        // 이동 루틴이 0.3초에 걸쳐 속도를 목표값으로 트위닝하는 중일 수 있다.
        // 그대로 두면 아래에서 정한 반사 속도를 다음 프레임에 트윈이 덮어써서,
        // 슬라임이 상대에게 계속 밀고 들어가 둘이 붙은 채로 떨게 된다.
        // 벽만 있을 때는 드물었지만 슬라임끼리 부딪히면서 흔해졌다.
        _rb.DOKill();

        // 슬라임끼리 부딪힌 것이면 아래 반사식을 쓰지 않는다. 저 식은 각자 자기
        // 속도를 그대로 되돌려 보내므로 운동량이 전달되지 않고, 맞은 쪽은 세기와
        // 무관하게 언제나 배회 속도로만 움직인다. 질량과 탄성이 같은 두 바디의
        // 충돌은 물리가 이미 제대로 풀어 두었으니 그 결과를 그냥 쓴다.
        //
        // 살짝 스친 것까지 물리에 넘기면 붙어 있는 동안 평소 이동이 계속 멈춘다.
        if (coll.collider.GetComponent<SlimeController>() != null)
        {
            if (coll.relativeVelocity.magnitude >= _collisionDriveMinimumSpeed)
            {
                TakeOverByPhysics();
            }

            return;
        }

        var speed = _lastVelocity.magnitude;
        var direction = Vector2.Reflect(_lastVelocity.normalized, coll.contacts[0].normal);
        _rb.linearVelocity = direction * Mathf.Max(speed, _moveSpeed);

        RotateSlime(direction);
    }

    // 밀림 중에는 매 물리 프레임 불린다. 향한 쪽이 그대로면 트윈을 다시 걸지 않고,
    // 좌우 속도가 0 근처일 때는 아예 판단하지 않는다. 0을 기준으로 삼으면 위아래로
    // 튕기는 동안 부호가 매 프레임 뒤집혀 0.3초짜리 회전 트윈이 계속 다시 걸리고,
    // 슬라임이 제자리에서 떠는 것처럼 보인다.
    private void RotateSlime(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < _facingDeadzone) return;

        if (direction.x > 0 && !_isFacingRight)
        {
            _isFacingRight = true;
            transform.DORotate(_rightVector, 0.3f);
        }
        else if (direction.x < 0 && _isFacingRight)
        {
            _isFacingRight = false;
            transform.DORotate(_leftVector, 0.3f);
        }
    }

    private void StopMoveCoroutine()
    {
        if (_moveCoroutine == null) return;

        StopCoroutine(_moveCoroutine);
        _moveCoroutine = null;
    }
}
