using UnityEngine;

public class ClickTarget : MonoBehaviour, IClickable
{
    [SerializeField] private string _name;
    [SerializeField] private int _level = 1;
    [SerializeField] private bool _isDragging = false;

    public int Level => _level;
    public bool IsDragging => _isDragging;
    public void LevelUp()
    {
        _level++;
        Debug.Log($"{_name} 레벨업! 현재 레벨: {_level}");
        // TODO: 외형 변경 콜백 추가 가능
    }
    public void StartDrag()
    {
        _isDragging = true;
        // Rigidbody 속도 정지
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void EndDrag()
    {
        _isDragging = false;
        TryMerge();
    }

    private void TryMerge()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.5f);

        foreach (var hit in hits)
        {
            ClickTarget other = hit.GetComponent<ClickTarget>();

            if (other != null && other != this && other.Level == this.Level)
            {
                MergeManager.Instance.Merge(this, other);
                return;
            }
        }
    }
    public bool OnClick(ClickInfo clickInfo)
    {
        Debug.Log($"{_name} 레벨 {_level} 히트 {clickInfo.Damage}데미지");

        // 클릭에 대한 피드백

        // ClickTarget : 타켓에 대한 중앙 관리자이자 소통의 창구 (객체지향 상호작용)
        var feedbacks = GetComponentsInChildren<IFeedback>();
        foreach (var feedback in feedbacks)
        {
            feedback.Play(clickInfo);
        }
        // 아래 사항을 컴포넌트 별로 나누기
        // 1. 클릭 이펙트
        // 2. 캐릭터 애니메이션
        // 3. 스케일 트위닝
        // 4. 사운드 재생
        // 5. 데미지 플로팅
        // 6. 화면 흔들림
        // 7. 재화 떨구기

        return true;
    }
}
