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
    private ScaleTweeningFeedback _scaleFeedback;
    private SpriteRenderer _spriteRenderer;
    private Collider2D[] _colliders = Array.Empty<Collider2D>();
    private Rigidbody2D _rigidbody;
    private RigidbodyInterpolation2D _defaultInterpolation;
    private bool _hasLanded = false;

    [Tooltip("드래그 중에 정렬 순서를 얼마나 올릴지입니다. 합성 후보 이펙트보다 커야 합니다.")]
    [SerializeField] private int _dragSortingOrderOffset = 10;

    private bool _isDragging = false;
    private int _defaultSortingOrder;
    private float _autoProductionTimer;
    private bool _isDisplayRoomFocused;

    public ESlimeGrade Grade => _slime.SpecData.Grade;
    public string InstanceId => Instance?.InstanceId;
    public bool IsSpecial => Instance != null && Instance.IsSpecial;
    public ESlimeLocation Location => Instance != null
        ? Instance.Location
        : ESlimeLocation.MainField;
    public bool IsDragging => _isDragging;
    // 스폰 직후 떨어지는 동안에는 합성 대상으로 삼지 않는다.
    public bool HasLanded => _hasLanded;
    public int Point => _slime != null ? _slime.SpecData.Point : 1;
    public float AutoClickInterval => _slime != null ? _slime.SpecData.AutoClickInterval : 1f;
    public bool IsMainFieldActive =>
        Instance != null &&
        Location == ESlimeLocation.MainField &&
        (GameplaySpaceManager.Instance == null ||
         GameplaySpaceManager.Instance.IsMainFieldInteractionActive);

    public event Action<ESlimeGrade> OnGradeChanged;
    public event Action OnSpawned;
    public event Action OnPromoted;
    public event Action OnLanded;
    public event Action OnInteracted;

    // 슬라임끼리 부딪혔을 때 그 충돌 속도를 넘긴다. 세기에 따라 반응을 달리하려는
    // 쪽이 쓴다. 메인 필드에서는 레이어 행렬이 서로를 막고 있어 장식장에서만 난다.
    public event Action<float> OnBumped;

    private void Awake()
    {
        _feedbacks = GetComponentsInChildren<IFeedback>();
        _slimeMove = GetComponent<SlimeMove>();
        _scaleFeedback = GetComponent<ScaleTweeningFeedback>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _defaultSortingOrder = _spriteRenderer.sortingOrder;
        }

        _colliders = GetComponents<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();
        if (_rigidbody != null)
        {
            _defaultInterpolation = _rigidbody.interpolation;
        }
    }

    private void SetSlime(Slime slime)
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
        SetDragging(false);
        _hasLanded = false;
        ResetAutoProductionPhase();
        OnSpawned?.Invoke();
    }

    // 스폰할 때마다 위상을 다시 흩는다. 같은 등급이 한꺼번에 복원되거나 스폰되면
    // 전부 같은 순간에 터지기 때문이다. 주기 자체는 건드리지 않는다.
    // 오프라인 보상이 AutoClickInterval을 평균 주기로 나눠 쓰므로 평균이 달라지면
    // 그 계산과 어긋난다.
    private void ResetAutoProductionPhase()
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
        SetDragging(false);
        Instance = null;
    }

    public void SetMovementLocked(bool isLocked)
    {
        _slimeMove?.SetMovementLocked(isLocked);
    }

    // 장식장 놀이터가 이 슬라임을 건드려도 되는지.
    //
    // 이 놀이는 장식장 안에서만 성립한다. 메인 필드 슬라임은 터치 포인트와 드래그
    // 합성의 대상이라, 밀려 날아가면 조준이 불가능해지고 합성 판정이 겹친다.
    // 다른 공간의 슬라임은 물리가 꺼져 있어 닿지도 않지만, 충돌을 거치지 않는
    // 호출이 생길 수 있으므로 여기서 한 번 더 막는다.
    //
    // 관찰 중인 슬라임도 뺀다. 카메라가 그 슬라임을 따라다니므로(기획서 §8)
    // 날아가거나 대포에 들어가면 화면 전체가 휘둘린다.
    //
    // 범퍼는 미는 순간에, 대포는 붙잡기 전에 같은 조건을 봐야 한다. 두 곳에
    // 따로 쓰면 한쪽만 고쳐졌을 때 조용히 어긋난다.
    public bool IsPlaygroundTarget =>
        !_isDragging &&
        !_isDisplayRoomFocused &&
        _slimeMove != null &&
        Location == ESlimeLocation.DisplayRoom;

    // 실제로 밀었는지를 돌려주므로, 부르는 쪽은 조건을 다시 쓰지 않고 연출만
    // 이 결과에 맞추면 된다.
    public bool Launch(Vector2 velocity)
    {
        if (!IsPlaygroundTarget) return false;

        return _slimeMove.Launch(velocity);
    }

    // 이미 날아가는 중인지. 오브젝트가 같은 슬라임을 연달아 밀지 판단한다.
    public bool IsLaunched => _slimeMove != null && _slimeMove.IsLaunched;

    // 자동 합성처럼 연출이 개체를 직접 움직이는 동안 쓴다. 콜라이더를 끄므로 터치도
    // 드래그도 닿지 않고, 물리가 연출 위치를 밀어내지도 않는다. 화면에는 계속 보인다.
    public void SetPresentationLocked(bool isLocked)
    {
        if (isLocked)
        {
            CancelDrag();

            // 크기의 주인은 피드백 쪽이다. 돌고 있던 펀치를 여기서 정리하지 않으면
            // 연출이 정한 크기를 펀치가 끝나면서 덮어쓴다.
            _scaleFeedback?.StopAndReset();
        }

        // 풀 때는 공간이 정해 둔 표시 상태를 따른다. 그림이 꺼진 슬라임은 지금 화면에 없는
        // 쪽에 있는 것이라, 콜라이더와 물리만 되살아나면 보이지 않는 채로 터치를 가로채고
        // 보이는 슬라임을 밀어낸다. 공간 전환 중 판정은 흔들리므로 그림 상태를 기준으로 삼는다.
        bool isInteractive = !isLocked &&
                             (_spriteRenderer == null || _spriteRenderer.enabled);

        foreach (Collider2D targetCollider in _colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = isInteractive;
            }
        }

        _slimeMove?.SetMovementLocked(!isInteractive);

        if (_rigidbody != null)
        {
            if (!isInteractive)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }

            _rigidbody.simulated = isInteractive;
        }
    }

    public void SetDisplayRoomCameraFocus(bool isFocused)
    {
        // 관찰 중에는 카메라가 이 슬라임을 따라다닌다(기획서 §8). 이때 밀려 날아가면
        // 화면 전체가 같이 휘둘리므로, 보고 있는 동안에는 놀이터의 힘에서 뺀다.
        _isDisplayRoomFocused = isFocused;

        if (_rigidbody != null)
        {
            _rigidbody.interpolation = isFocused
                ? RigidbodyInterpolation2D.Interpolate
                : _defaultInterpolation;
        }
    }

    public void SetLocationPresentationActive(bool isActive)
    {
        ApplySlimeToSlimeCollision();

        if (!isActive)
        {
            CancelDrag();
            // 다른 공간에서 복원된 슬라임이 다시 활성화될 때
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

    // 슬라임끼리는 레이어 충돌 행렬에서 서로 부딪히지 않게 꺼 두었다(Clickable 대 Clickable).
    // 드래그로 겹쳐서 합성하는 조작이 그 위에 서 있어, 행렬을 켜면 두 마리를 포개는
    // 것 자체가 불가능해져 메인 필드의 합성이 깨진다.
    //
    // 그래서 행렬은 그대로 두고, 장식장에 있는 동안만 이 개체에 한해 같은 레이어를
    // 다시 포함시킨다. 리지드바디와 콜라이더 양쪽에 같은 값을 넣는 것은 둘 중
    // 어느 쪽이 우선하든 결과가 같게 하려는 것이다.
    private void ApplySlimeToSlimeCollision()
    {
        LayerMask mask = Location == ESlimeLocation.DisplayRoom
            ? 1 << gameObject.layer
            : 0;

        if (_rigidbody != null)
        {
            _rigidbody.includeLayers = mask;
        }

        foreach (Collider2D targetCollider in _colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.includeLayers = mask;
            }
        }
    }

    public void PreparePresentationTransfer()
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

        // Collision2D.collider는 상대 쪽 콜라이더다. 벽에 닿은 것과 구분하려면
        // 이 검사가 필요하다.
        if (collision.collider.GetComponent<SlimeController>() == null) return;

        OnBumped?.Invoke(collision.relativeVelocity.magnitude);
    }

    public void StartDrag()
    {
        SetDragging(true);
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
        SetDragging(false);
        OnInteracted?.Invoke();
        TryMerge(preferredTarget);
    }

    public void CancelDrag()
    {
        SetDragging(false);
    }

    // 슬라임은 모두 같은 정렬 순서와 같은 z에 있어 겹쳤을 때 누가 앞에 그려질지
    // 정해져 있지 않다. 잡고 있는 슬라임이 다른 슬라임 뒤로 숨지 않도록 드래그 동안만
    // 앞으로 올린다. 풀에서 재사용되므로 스폰과 디스폰에서도 되돌린다.
    private void SetDragging(bool isDragging)
    {
        _isDragging = isDragging;
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sortingOrder = isDragging
                ? _defaultSortingOrder + _dragSortingOrderOffset
                : _defaultSortingOrder;
        }
    }

    public bool CanMergeWith(SlimeController other)
    {
        return other != null &&
               other != this &&
               // 기획서 §7.2 - 장식장 개체는 합성 대상이 되지 않는다.
               Location == ESlimeLocation.MainField &&
               other.Location == ESlimeLocation.MainField &&
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
        if (!IsMainFieldActive) return true;

        foreach (IFeedback feedback in _feedbacks)
        {
            feedback.Play(clickInfo);
        }

        return true;
    }
}
