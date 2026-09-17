using System;
using UnityEngine;

// 가챠권이 떨어지는지만 판정한다. 티켓을 어디에 만들고 어떻게 저장할지는 모른다.
//
// 스폰·자동 생산과 분리된 자체 주기를 쓴다. 명세가 그렇게 정해 두었고, 스폰 간격은
// 업그레이드로 줄어들기 때문에 거기 얹으면 업그레이드가 곧 드랍량 증가가 된다.
//
// 주기는 하나만 두고 그 순간 필드의 슬라임이 각자 판정한다. 개체마다 타이머를 주면
// 풀에서 재사용될 때 위상이 흩어져 "60초마다 전원 판정"이 아니게 되고, 합성으로
// 두 마리가 한 마리가 될 때 누구의 타이머를 남길지가 규칙이 되어 버린다.
//
// 대상 규칙은 자동 생산과 같다. 장식장 슬라임은 드랍하지 않는다.
//
// 가챠가 해금되고 소개 튜토리얼까지 끝난 뒤에만 판정한다. 튜토리얼 중에는 체험용
// 한 장만 보여 줘야, 설명을 듣기도 전에 정체 모를 티켓이 필드에 생기지 않는다.
public class GachaTicketDropper : MonoBehaviour
{
    [Tooltip("판정 주기(초).")]
    [SerializeField, Min(1f)] private float _judgeInterval = 60f;

    [Tooltip("한 번의 판정에서 슬라임 한 마리가 티켓을 떨어뜨릴 확률. 1차 밸런스 값 0.167%.")]
    [SerializeField, Range(0f, 1f)] private float _dropChancePerSlime = 0.00167f;

    private float _timer;

    // 판정에 성공한 슬라임을 넘긴다. 티켓의 위치와 소속 스테이지는 받는 쪽이 정한다.
    public event Action<SlimeController> Dropped;

    private void Update()
    {
        if (!GameplayGate.IsActive) return;
        if (SpawnManager.Instance == null) return;
        if (SlimeManager.Instance == null || !SlimeManager.Instance.IsGachaUnlocked) return;
        if (!TutorialProgress.IsCompleted(TutorialIds.Gacha) || TutorialManager.IsRunning) return;

        _timer += Time.deltaTime;
        if (_timer < _judgeInterval) return;

        _timer = 0f;
        Judge();
    }

    private void Judge()
    {
        // 순회 중에는 슬라임을 만들거나 없애지 않는다. 활성 목록이 바뀌면 예외가 난다.
        // 구독자가 티켓을 만드는 것은 슬라임 목록을 건드리지 않으므로 괜찮다.
        foreach (SlimeController target in SpawnManager.Instance.GetActiveTargets())
        {
            if (target == null || target.Location != ESlimeLocation.MainStage) continue;
            if (UnityEngine.Random.value >= _dropChancePerSlime) continue;

            // 시간당 몇 장 나오는지는 밸런스 조정의 근거가 되고, 드랍은 시간당 몇 번
            // 수준이라 로그가 흐름을 가리지 않는다.
            Debug.Log($"가챠권 드랍 : {target.Grade} : {target.transform.position}");
            Dropped?.Invoke(target);
        }
    }
}
