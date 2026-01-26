using DG.Tweening;
using System.Collections;
using UnityEngine;

public class ClickTargetMove : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 1f;
    [SerializeField] private Ease _moveEase;
    [SerializeField] private float _minMoveDuration = 0.5f;
    [SerializeField] private float _maxMoveDuration = 3f;
    [SerializeField] private float _minIdleDuration = 0.3f;
    [SerializeField] private float _maxIdleDuration = 2f;

    private Rigidbody2D _rb;
    private Vector2 _lastVelocity;
    private ClickTarget _clickTarget;
    private Vector3 _rightVector = Vector3.zero;
    private Vector3 _leftVector = new Vector3(0, 180, 0);
    private Coroutine _moveCoroutine;

    private void Start()
    {
        _clickTarget = GetComponent<ClickTarget>();
        _rb = GetComponent<Rigidbody2D>();
        _moveCoroutine = StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        while (true)
        {
            if (_clickTarget.IsDragging)
            {
                yield return null;
                continue;
            }
            float idleDuration = Random.Range(_minIdleDuration, _maxIdleDuration);
            yield return new WaitForSeconds(idleDuration);

            // 랜덤 방향 계산
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector2 targetVelocity = randomDirection * _moveSpeed;
            RotateSlime(randomDirection);

            // 부드럽게 가속 (0.3초)
            _rb.DOKill(); // 이전 트윈 중단
            DOTween.To(
                () => _rb.linearVelocity,
                x => _rb.linearVelocity = x,
                targetVelocity,
                0.3f
            ).SetEase(_moveEase);

            // 랜덤 시간 동안 이동
            float moveDuration = Random.Range(_minMoveDuration, _maxMoveDuration);
            yield return new WaitForSeconds(moveDuration);

            if (_clickTarget.IsDragging) continue;

            // 부드럽게 감속 (0.3초)
            _rb.DOKill();
            DOTween.To(
                () => _rb.linearVelocity,
                x => _rb.linearVelocity = x,
                Vector2.zero,
                0.3f
            ).SetEase(_moveEase);

            // 랜덤 시간 동안 대기
            //float idleDuration = Random.Range(_minIdleDuration, _maxIdleDuration);
            yield return new WaitForSeconds(idleDuration);
        }
    }

    private void FixedUpdate()
    {
        // 충돌 직전 속도 저장 (반사용)
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
        // 반사 후 방향에 따라 회전
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
