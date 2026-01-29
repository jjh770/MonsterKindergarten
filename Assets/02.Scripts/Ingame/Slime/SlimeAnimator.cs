using UnityEngine;

public class SlimeAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private AnimatorOverrideController[] _levelAnimators;

    private Slime _slime;
    private Rigidbody2D _rb;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsDragging = Animator.StringToHash("IsDragging");

    private void Awake()
    {
        _slime = GetComponent<Slime>();
        _rb = GetComponent<Rigidbody2D>();

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        _slime.OnLevelChanged += UpdateAnimator;
        _slime.OnInteracted += OnInteracted;
        UpdateAnimator(_slime.Level);
    }

    private void OnDestroy()
    {
        if (_slime != null)
        {
            _slime.OnLevelChanged -= UpdateAnimator;
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

    private void UpdateAnimator(int level)
    {
        if (_levelAnimators == null || _levelAnimators.Length == 0 || _animator == null)
            return;

        int index = Mathf.Clamp(level - 1, 0, _levelAnimators.Length - 1);
        _animator.runtimeAnimatorController = _levelAnimators[index];
    }

    private void OnInteracted()
    {
        _animator.SetTrigger("IsClick");
    }
}
