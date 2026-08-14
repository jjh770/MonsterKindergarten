using UnityEngine;

public class SlimeAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private AnimatorOverrideController[] _levelAnimators;

    private SlimeController _slime;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;

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
        _animator.SetBool(IsDragging, _slime.IsDragging);
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
