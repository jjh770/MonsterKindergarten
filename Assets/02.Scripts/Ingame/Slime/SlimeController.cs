using System;
using UnityEngine;

// 슬라임 도메인을 가지고 있고 실제 갖가지 기능을 동작하게하는 슬라임 컨트롤러
public class SlimeController : MonoBehaviour, IClickable
{
    private Slime _slime;
    public Slime Slime => _slime;

    private IFeedback[] _feedbacks = Array.Empty<IFeedback>();
    private bool _hasLanded = false;

    private bool _isDragging = false;

    public ESlimeGrade Grade => _slime.SpecData.Grade;
    public bool IsDragging => _isDragging;
    public int Point => _slime != null ? _slime.SpecData.Point : 1;
    public float AutoClickInterval => _slime != null ? _slime.SpecData.AutoClickInterval : 1f;

    public event Action<ESlimeGrade> OnGradeChanged;
    public event Action OnSpawned;
    public event Action OnPromoted;
    public event Action OnLanded;
    public event Action OnInteracted;

    private void Awake()
    {
        _feedbacks = GetComponentsInChildren<IFeedback>();
    }

    public void SetSlime(Slime slime)
    {
        _slime = slime;

        OnGradeChanged?.Invoke(_slime.SpecData.Grade);
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
        OnSpawned?.Invoke();
    }

    public void OnDespawn()
    {
        _isDragging = false;
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

        // 포인트 적립
        CurrencyManager.Instance.Add(ECurrencyType.Point, clickInfo.Point);

        // 클릭에 대한 피드백
        foreach (IFeedback feedback in _feedbacks)
        {
            feedback.Play(clickInfo);
        }

        return true;
    }
}
