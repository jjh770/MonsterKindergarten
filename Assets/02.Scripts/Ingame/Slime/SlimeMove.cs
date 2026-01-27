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

    private Rigidbody2D _rb;
    private Vector2 _lastVelocity;
    private Slime _slime;
    private Vector3 _rightVector = Vector3.zero;
    private Vector3 _leftVector = new Vector3(0, 180, 0);
    private Coroutine _moveCoroutine;

    private void Start()
    {
        _slime = GetComponent<Slime>();
        _rb = GetComponent<Rigidbody2D>();

        _slime.OnInteracted += OnInteracted;
        _moveCoroutine = StartCoroutine(MoveRoutine());
    }

    private void OnDestroy()
    {
        if (_slime != null)
        {
            _slime.OnInteracted -= OnInteracted;
        }
    }

    private void OnInteracted()
    {
        // 이동 중단하고 Idle 상태로 전환
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
        }
        _rb.DOKill();
        _rb.linearVelocity = Vector2.zero;
        _moveCoroutine = StartCoroutine(IdleThenMoveRoutine());
    }

    private IEnumerator IdleThenMoveRoutine()
    {
        // 상호작용 후 2초 Idle
        yield return new WaitForSeconds(_interactionIdleDuration);
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
            ).SetEase(_moveEase);

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
            ).SetEase(_moveEase);

            yield return new WaitForSeconds(0.3f);
        }
    }

    private void FixedUpdate()
    {
        if (_rb.linearVelocity.magnitude > 0.1f)
        {
            _lastVelocity = _rb.linearVelocity;
        }
    }

    private void OnCollisionEnter2D(Collision2D coll)
    {
        var speed = _lastVelocity.magnitude;
        var direction = Vector2.Reflect(_lastVelocity.normalized, coll.contacts[0].normal);
        _rb.linearVelocity = direction * Mathf.Max(speed, _moveSpeed);

        RotateSlime(direction);
    }

    private void RotateSlime(Vector2 direction)
    {
        if (direction.x > 0)
        {
            transform.DORotate(_rightVector, 0.3f);
        }
        else if (direction.x < 0)
        {
            transform.DORotate(_leftVector, 0.3f);
        }
    }
}
