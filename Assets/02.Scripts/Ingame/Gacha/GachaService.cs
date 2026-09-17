using UnityEngine;

public enum EGachaFailure
{
    None,
    Locked,
    NoTicket,
    NoRoom,
    NoCandidate,
    SpawnFailed,
}

// 가챠권 한 장을 슬라임 한 마리로 바꾼다. 기획서 §11.2.
//
// 상태가 없어 정적으로 둔다. 필요한 것은 모두 매니저들이 들고 있고, 이 클래스는
// 그 사이의 순서만 정한다.
//
// 순서가 규칙이다. 되돌릴 수 없는 소비를 마지막 확인 뒤로 미룬다. 자리와 후보를
// 먼저 확인하지 않고 티켓부터 쓰면, 실패한 가챠가 티켓만 먹는다.
public static class GachaService
{
    public static EGachaFailure TryPull(out SlimeController spawned)
    {
        spawned = null;

        SlimeManager slimeManager = SlimeManager.Instance;
        SpawnManager spawnManager = SpawnManager.Instance;
        CurrencyManager currencyManager = CurrencyManager.Instance;
        if (slimeManager == null || spawnManager == null || currencyManager == null)
        {
            return EGachaFailure.SpawnFailed;
        }

        if (!slimeManager.IsGachaUnlocked) return EGachaFailure.Locked;

        if (!currencyManager.CanAfford(ECurrencyType.GachaTicket, 1d))
        {
            return EGachaFailure.NoTicket;
        }

        // 메인 스테이지에 자리가 있어야 한다. 결과는 별도 보관함이 아니라 필드에
        // 실제로 태어나므로, 자리가 없으면 놓을 곳이 없다.
        if (!spawnManager.HasMainStageRoom()) return EGachaFailure.NoRoom;

        if (!NormalGachaPool.TryPick(slimeManager.HighestGrade, out ESlimeGrade grade))
        {
            return EGachaFailure.NoCandidate;
        }

        if (!currencyManager.TrySpend(ECurrencyType.GachaTicket, 1d))
        {
            return EGachaFailure.NoTicket;
        }

        spawned = spawnManager.Spawn(grade);
        if (spawned != null) return EGachaFailure.None;

        // 티켓만 사라지는 것이 가장 나쁘다. 되돌린다.
        currencyManager.Add(ECurrencyType.GachaTicket, 1d);
        Debug.LogError($"가챠 결과를 생성하지 못했습니다. : {grade}");
        return EGachaFailure.SpawnFailed;
    }
}
