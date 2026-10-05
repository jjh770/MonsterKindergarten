using UnityEngine;

public class SlimeAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private AnimatorOverrideController[] _levelAnimators;

    [Header("Dragging")]
    [Tooltip("끄는 중 이 속도(월드 거리/초)보다 빠르게 움직여야 드래그 모션이 나옵니다. 누르고만 있을 때 손가락 떨림은 걸러냅니다.")]
    [SerializeField, Min(0f)] private float _dragMoveSpeed = 1.5f;
    [Tooltip("움직임이 멈춘 뒤에도 드래그 모션을 이만큼 유지합니다. 끊겨 보이지 않게 하려는 값입니다.")]
    [SerializeField, Min(0f)] private float _dragMotionHold = 0.15f;

    private SlimeController _slime;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Vector3 _lastPosition;
    private float _dragMotionTimer;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsDragging = Animator.StringToHash("IsDragging");

    private void Awake()
    {
        _slime = GetComponent<SlimeController>();
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        _slime.OnGradeChanged += UpdateAnimator;
        _slime.OnInteracted += OnInteracted;
        UpdateAnimator(_slime.Grade);
    }

    private void OnDestroy()
    {
        if (_slime != null)
        {
            _slime.OnGradeChanged -= UpdateAnimator;
            _slime.OnInteracted -= OnInteracted;
        }
    }

    private void Update()
    {
        if (_animator == null) return;

        bool isMoving = _rb != null && _rb.linearVelocity.magnitude > 0.1f;
        _animator.SetBool(IsMoving, isMoving);
        _animator.SetBool(IsDragging, IsDragMotionPlaying());
    }

    // 누르고만 있는 동안은 드래그 상태여도 Idle로 두고, 실제로 움직일 때만 드래그 모션을 낸다.
    private bool IsDragMotionPlaying()
    {
        Vector3 position = transform.position;
        float deltaTime = Time.deltaTime;
        float speed = deltaTime > 0f
            ? (position - _lastPosition).magnitude / deltaTime
            : 0f;
        _lastPosition = position;

        if (!_slime.IsDragging)
        {
            _dragMotionTimer = 0f;
            return false;
        }

        if (speed > _dragMoveSpeed)
        {
            _dragMotionTimer = _dragMotionHold;
        }
        else
        {
            _dragMotionTimer -= deltaTime;
        }

        return _dragMotionTimer > 0f;
    }

    private void UpdateAnimator(ESlimeGrade grade)
    {
        if (_levelAnimators == null || _levelAnimators.Length == 0 || _animator == null || _slime.Slime == null)
            return;

        int index = Mathf.Clamp((int)grade - 1, 0, _levelAnimators.Length - 1);
        AnimatorOverrideController controller = _levelAnimators[index];

        // 아직 전용 애니메이션이 없는 등급은 기본 애니메이션 대신 등록된 스프라이트를 표시한다.
        bool usesBaseAnimation = index == 0;
        if (!usesBaseAnimation && !HasCustomAnimation(controller))
        {
            _animator.enabled = false;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = _slime.Slime.SpecData.Sprite;
            }
            return;
        }

        _animator.runtimeAnimatorController = controller;
        _animator.enabled = true;
    }

    private static bool HasCustomAnimation(AnimatorOverrideController controller)
    {
        if (controller == null) return false;

        var overrides =
            new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>(
                controller.overridesCount);
        controller.GetOverrides(overrides);

        foreach (var pair in overrides)
        {
            if (pair.Value != null && pair.Value != pair.Key)
            {
                return true;
            }
        }

        return false;
    }

    private void OnInteracted()
    {
        _animator.SetTrigger("IsClick");
    }
}
