using UnityEngine;

public class ClickTargetAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private AnimatorOverrideController[] _levelAnimators;

    private ClickTarget _clickTarget;
    private Rigidbody2D _rb;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsDragging = Animator.StringToHash("IsDragging");

    private void Awake()
    {
        _clickTarget = GetComponent<ClickTarget>();
        _rb = GetComponent<Rigidbody2D>();

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        _clickTarget.OnLevelChanged += UpdateAnimator;
        UpdateAnimator(_clickTarget.Level);
    }

    private void OnDestroy()
    {
        if (_clickTarget != null)
        {
            _clickTarget.OnLevelChanged -= UpdateAnimator;
        }
    }

    private void Update()
    {
        if (_animator == null) return;

        bool isMoving = _rb != null && _rb.linearVelocity.magnitude > 0.1f;
        _animator.SetBool(IsMoving, isMoving);
        _animator.SetBool(IsDragging, _clickTarget.IsDragging);
    }

    private void UpdateAnimator(int level)
    {
        if (_levelAnimators == null || _levelAnimators.Length == 0 || _animator == null)
            return;

        int index = Mathf.Clamp(level - 1, 0, _levelAnimators.Length - 1);
        _animator.runtimeAnimatorController = _levelAnimators[index];
    }
}
