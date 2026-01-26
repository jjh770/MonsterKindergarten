using UnityEngine;

public class ClickTarget : MonoBehaviour, IClickable
{
    [SerializeField] private string _name;

    private void Awake()
    {

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
