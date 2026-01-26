using UnityEngine;

public class ClickTarget : MonoBehaviour, IClickable
{
    [SerializeField] private string _name;
    [SerializeField] private int _level = 1;

    public int Level => _level;

    [SerializeField] private float _moveSpeed = 1f; // 이동 속도
    private Rigidbody2D _rb;
    private Vector2 _lastVelocity; // 마지막 속도 저장용

    private void Awake()
    {

    }

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        // 게임 시작 시 랜덤한 방향으로 힘을 가함
        _rb.linearVelocity = Random.insideUnitCircle.normalized * _moveSpeed;
    }

    private void FixedUpdate()
    {
        // 물리 엔진 특성상 속도가 조금씩 줄어들 수 있으므로 속도(Speed)를 강제로 고정
        // 드래그 중이 아닐 때만 적용
        if (_rb.linearVelocity.magnitude != 0)
        {
            _lastVelocity = _rb.linearVelocity; // 충돌 직전 속도 저장
            _rb.linearVelocity = _rb.linearVelocity.normalized * _moveSpeed;
        }
    }

    // 충돌 시 튕겨 나가는 방향 계산 (가끔 벽에 끼는 것 방지)
    private void OnCollisionEnter2D(Collision2D coll)
    {
        // 충돌한 면의 법선 벡터(Normal)를 이용해 반사 벡터 계산
        var speed = _lastVelocity.magnitude;
        var direction = Vector2.Reflect(_lastVelocity.normalized, coll.contacts[0].normal);

        _rb.linearVelocity = direction * Mathf.Max(speed, _moveSpeed);
    }

    private void OnMouseDrag()
    {
        // 1. 마우스 위치를 따라다니게 함 (뛰어다니는 AI 잠시 중지)
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = mousePos;
    }

    private void OnMouseUp()
    {
        // 드래그가 끝났을 때 겹쳐있는 다른 몬스터가 있는지 확인
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.5f);

        foreach (var hit in hits)
        {
            ClickTarget other = hit.GetComponent<ClickTarget>();

            // 나 자신이 아니고, 같은 레벨이라면? -> 합체!
            if (other != null && other != this && other.Level == this.Level)
            {
                MergeManager.Instance.Merge(this, other);
                return;
            }
        }
    }

    public void LevelUp()
    {
        _level++;
        Debug.Log($"{_name} 레벨업! 현재 레벨: {_level}");
        // TODO: 외형 변경 콜백 추가 가능
    }
    public bool OnClick(ClickInfo clickInfo)
    {
        Debug.Log($"{_name} : 히트");

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
