using System;
using UnityEngine;

// 슬라임 도메인을 가지고 있고 실제 갖가지 기능을 동작하게하는 슬라임 컨트롤러
public class SlimeController : MonoBehaviour, IClickable
{
    private Slime _slime;
    public Slime Slime => _slime;
    private SlimeInstance Instance { get; set; }

    private IFeedback[] _feedbacks = Array.Empty<IFeedback>();
    private SlimeMove _slimeMove;
    private SpriteRenderer _spriteRenderer;
    private Collider2D[] _colliders = Array.Empty<Collider2D>();
    private Rigidbody2D _rigidbody;
    private RigidbodyInterpolation2D _defaultInterpolation;
    private bool _hasLanded = false;

    private bool _isDragging = false;
    private float _autoProductionTimer;

    public ESlimeGrade Grade => _slime.SpecData.Grade;
    public string InstanceId => Instance?.InstanceId;
    public bool IsSpecial => Instance != null && Instance.IsSpecial;
    public ESlimeLocation Location => Instance != null
        ? Instance.Location
        : ESlimeLocation.MainStage;
    public bool IsDragging => _isDragging;
    public int Point => _slime != null ? _slime.SpecData.Point : 1;
    public float AutoClickInterval => _slime != null ? _slime.SpecData.AutoClickInterval : 1f;
    public bool IsCurrentStageActive =>
        Instance != null &&
        Location == ESlimeLocation.MainStage &&
        (StageManager.Instance == null ||
         StageManager.Instance.IsStageActive(Grade));

    public event Action<ESlimeGrade> OnGradeChanged;
    public event Action OnSpawned;
    public event Action OnPromoted;
    public event Action OnLanded;
    public event Action OnInteracted;

    private void Awake()
    {
        _feedbacks = GetComponentsInChildren<IFeedback>();
        _slimeMove = GetComponent<SlimeMove>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _colliders = GetComponents<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();
        if (_rigidbody != null)
        {
            _defaultInterpolation = _rigidbody.interpolation;
        }
    }

    public void SetSlime(Slime slime)
    {
        _slime = slime;

        OnGradeChanged?.Invoke(_slime.SpecData.Grade);
    }

    public void Bind(Slime slime, SlimeInstance instance)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        SetSlime(slime);
    }

    public void PromoteTo(Slime slime)
    {
        SetSlime(slime);
        OnPromoted?.Invoke();
    }

    public void OnSpawn()
    {
        _isDragging = false;
        _hasLanded = false;
        ResetAutoProductionPhase();
        OnSpawned?.Invoke();
    }

    // 스폰할 때마다 위상을 다시 흩는다. 같은 등급이 한꺼번에 복원되거나 스폰되면
    // 전부 같은 순간에 터지기 때문이다. 주기 자체는 건드리지 않는다.
    // 오프라인 보상이 AutoClickInterval을 평균 주기로 나눠 쓰므로 평균이 달라지면
    // 그 계산과 어긋난다.
    public void ResetAutoProductionPhase()
    {
        float interval = AutoClickInterval;
        _autoProductionTimer = interval > 0f
            ? UnityEngine.Random.Range(0f, interval)
            : 0f;
    }

    // 자동 생산 주기를 진행시키고, 한 주기를 채웠으면 true를 돌려준다.
    // 대상 판정과 일시정지는 호출부인 AutoClicker가 맡는다.
    public bool TickAutoProduction(float deltaTime)
    {
        float interval = AutoClickInterval;
        if (interval <= 0f) return false;

        _autoProductionTimer += deltaTime;
        if (_autoProductionTimer < interval) return false;

        _autoProductionTimer = 0f;
        return true;
    }

    public void OnDespawn()
    {
        _isDragging = false;
        Instance = null;
    }

    public void SetMovementLocked(bool isLocked)
    {
        _slimeMove?.SetMovementLocked(isLocked);
    }

    public void SetDisplayRoomCameraFocus(bool isFocused)
    {
        if (_rigidbody != null)
        {
            _rigidbody.interpolation = isFocused
                ? RigidbodyInterpolation2D.Interpolate
                : _defaultInterpolation;
        }
    }

    public void SetStagePresentationActive(bool isActive)
    {
        if (!isActive)
        {
            CancelDrag();
            // 비활성 스테이지에서 복원된 슬라임이 다시 활성화될 때
            // 일괄 충돌로 착지음이 재생되지 않도록 첫 착지를 소비한다.
            _hasLanded = true;
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = isActive;
        }

        foreach (Collider2D targetCollider in _colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = isActive;
            }
        }

        if (!isActive)
        {
            _slimeMove?.SetMovementLocked(true);
        }

        if (_rigidbody != null)
        {
            if (!isActive)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }

            _rigidbody.simulated = isActive;
        }

        if (isActive)
        {
            _slimeMove?.SetMovementLocked(false);
        }
    }

    public void PrepareStageTransfer()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
        }

        foreach (Collider2D targetCollider in _colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = false;
            }
        }

        _slimeMove?.SetMovementLocked(true);

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.simulated = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_hasLanded)
        {
            _hasLanded = true;
            OnLanded?.Invoke();
        }
    }

    public void StartDrag()
    {
        _isDragging = true;
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void EndDrag()
    {
        EndDrag(null);
    }

    public void EndDrag(SlimeController preferredTarget)
    {
        _isDragging = false;
        OnInteracted?.Invoke();
        TryMerge(preferredTarget);
    }

    public void CancelDrag()
    {
        _isDragging = false;
    }

    public bool CanMergeWith(SlimeController other)
    {
        return other != null &&
               other != this &&
               // 기획서 §7.2 - 장식장 개체는 합성 대상이 되지 않는다.
               Location == ESlimeLocation.MainStage &&
               other.Location == ESlimeLocation.MainStage &&
               _slime != null &&
               other.Slime != null &&
               SlimeManager.Instance != null &&
               SlimeManager.Instance.CanMerge(_slime, other.Slime);
    }

    private void TryMerge(SlimeController preferredTarget)
    {
        if (CanMergeWith(preferredTarget))
        {
            MergeManager.Instance.Merge(this, preferredTarget);
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.5f);

        foreach (var hit in hits)
        {
            SlimeController other = hit.GetComponent<SlimeController>();

            if (CanMergeWith(other))
            {
                MergeManager.Instance.Merge(this, other);
                return;
            }
        }
    }
    public bool OnClick(ClickInfo clickInfo)
    {
        // 수동 클릭일 때만 멈춤
        if (clickInfo.ClickType == EClickType.Manual)
        {
            OnInteracted?.Invoke();
        }

        if (!IsSpecial)
        {
            SlimeManager.Instance?.RecordProduction(
                clickInfo.Grade,
                clickInfo.ClickType,
                clickInfo.Point);
        }

        // 포인트 적립
        CurrencyManager.Instance.Add(ECurrencyType.Point, clickInfo.Point);

        // 클릭에 대한 피드백
        if (!IsCurrentStageActive) return true;

        foreach (IFeedback feedback in _feedbacks)
        {
            feedback.Play(clickInfo);
        }

        return true;
    }
}
