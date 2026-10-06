using System;
using System.Collections.Generic;
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

// 한 번에 뽑은 결과 하나. 슬라임은 이미 필드에 생성되고 저장된 상태다.
public readonly struct GachaPullItem
{
    public readonly SlimeController Slime;
    public readonly EGachaRarity Rarity;

    public GachaPullItem(SlimeController slime, EGachaRarity rarity)
    {
        Slime = slime;
        Rarity = rarity;
    }
}

// 뽑기권을 슬라임으로 바꾼다. 한 번에 1장 또는 여러 장이다. 기획서 §11.2.
//
// 상태가 없어 정적으로 둔다. 필요한 것은 모두 매니저들이 들고 있고, 이 클래스는
// 그 사이의 순서만 정한다.
//
// 순서가 규칙이다. 되돌릴 수 없는 소비를 마지막 확인 뒤로 미룬다. 자리와 후보를
// 먼저 확인하지 않고 티켓부터 쓰면, 실패한 뽑기가 티켓만 먹는다.
public static class GachaService
{
    private readonly struct Pick
    {
        public readonly ESlimeGrade Grade;
        public readonly EGachaRarity Rarity;
        public readonly bool IsSpecial;
        public readonly float Chance;

        public Pick(ESlimeGrade grade, EGachaRarity rarity, bool isSpecial, float chance)
        {
            Grade = grade;
            Rarity = rarity;
            IsSpecial = isSpecial;
            Chance = chance;
        }
    }

    // count장을 한 번에 쓰고 count마리를 만든다. 자리는 count칸이 비어 있어야 하고, 하나라도 모자라면
    // 아무것도 쓰지 않는다. 생성이 중간에 실패하면 만든 만큼만 남기고 나머지 장수는 돌려준다.
    public static EGachaFailure TryPullMany(
        int count,
        List<GachaPullItem> results,
        bool isTutorialPull = false)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

        results.Clear();

        SlimeManager slimeManager = SlimeManager.Instance;
        SpawnManager spawnManager = SpawnManager.Instance;
        CurrencyManager currencyManager = CurrencyManager.Instance;
        if (slimeManager == null || spawnManager == null || currencyManager == null)
        {
            return EGachaFailure.SpawnFailed;
        }

        if (!slimeManager.IsGachaUnlocked) return EGachaFailure.Locked;

        if (!currencyManager.CanAfford(ECurrencyType.GachaTicket, (double)count))
        {
            return EGachaFailure.NoTicket;
        }

        // 메인 필드에 자리가 있어야 한다. 결과는 별도 보관함이 아니라 필드에
        // 실제로 태어나므로, 자리가 없으면 놓을 곳이 없다.
        if (!spawnManager.HasMainFieldRoom(count)) return EGachaFailure.NoRoom;

        // 피버는 앞선 결과가 이미 기록된 것처럼 이어서 굴린다. 실제 기록은 티켓 거래가 성공한 뒤에 한다.
        int missCount = slimeManager.SpecialGachaMissCount;
        bool isFeverActive = slimeManager.IsHiddenFeverUnlocked;
        List<Pick> picks = new(count);
        for (int i = 0; i < count; i++)
        {
            float specialChance = SpecialGachaFever.GetChance(missCount);
            if (!TryPick(
                    slimeManager,
                    specialChance,
                    isTutorialPull,
                    out ESlimeGrade pickedGrade,
                    out EGachaRarity pickedRarity,
                    out bool isSpecial))
            {
                return EGachaFailure.NoCandidate;
            }

            picks.Add(new Pick(pickedGrade, pickedRarity, isSpecial, specialChance));
            if (isFeverActive)
            {
                missCount = isSpecial
                    ? 0
                    : Math.Min(missCount + 1, SpecialGachaFever.MaximumMissCount);
            }
        }

        if (!currencyManager.TrySpend(ECurrencyType.GachaTicket, (double)count))
        {
            return EGachaFailure.NoTicket;
        }

        int spawnedCount = 0;
        try
        {
            foreach (Pick pick in picks)
            {
                SlimeController slime = spawnManager.Spawn(pick.Grade, isSpecial: pick.IsSpecial);
                if (slime == null) break;

                results.Add(new GachaPullItem(slime, pick.Rarity));
                spawnedCount++;
            }
        }
        finally
        {
            // 만들지 못한 장수는 돌려준다. 예외로 끝나도 같다.
            if (spawnedCount < count)
            {
                currencyManager.Add(ECurrencyType.GachaTicket, (double)(count - spawnedCount));
            }
        }

        if (spawnedCount == 0)
        {
            Debug.LogError($"뽑기 결과를 생성하지 못했습니다. : {picks[0].Grade}");
            return EGachaFailure.SpawnFailed;
        }

        // 실패 횟수는 결과가 실제로 생긴 뒤에만 센다. 환불된 뽑기가 피버를 올리면 안 된다.
        for (int i = 0; i < spawnedCount; i++)
        {
            if (!isTutorialPull) slimeManager.RecordSpecialGachaResult(picks[i].IsSpecial);
            slimeManager.RecordGachaObtained(picks[i].Grade);
            LogPull(picks[i].Grade, picks[i].IsSpecial, picks[i].Chance);
        }

        return EGachaFailure.None;
    }

// 기획서 §15.2: 특별 여부를 가장 먼저 판정한다. 종류는 특별이든 일반이든 같은 일반 풀에서
    // 고른다. 특별한 슬라임은 외형과 포인트 배율만 다른 같은 등급이라 후보와 가중치를 나눌
    // 이유가 없다.
    private static bool TryPick(
        SlimeManager slimeManager,
        float specialChance,
        bool isTutorialPull,
        out ESlimeGrade grade,
        out EGachaRarity rarity,
        out bool isSpecial)
    {
        grade = ESlimeGrade.None;
        rarity = EGachaRarity.Common;
        isSpecial = false;

        // 튜토리얼의 무료 한 장은 특별 판정도 하지 않는다. 고정된 좋은 결과로 시작해야
        // 하고, 규칙 밖의 한 장이 피버 실패 횟수를 올리지도 않아야 한다.
        if (isTutorialPull)
        {
            if (!NormalGachaPool.TryPickTutorial(slimeManager.HighestGrade, out NormalGachaResult tutorialResult))
            {
                return false;
            }

            grade = tutorialResult.Grade;
            rarity = tutorialResult.Rarity;
            return true;
        }

        bool isSpecialRoll = UnityEngine.Random.value < specialChance;
        if (!NormalGachaPool.TryPick(slimeManager.HighestGrade, out NormalGachaResult result))
        {
            return false;
        }

        grade = result.Grade;
        isSpecial = isSpecialRoll;
        rarity = isSpecialRoll ? EGachaRarity.Special : result.Rarity;
        return true;
    }

    // 확률과 결과를 에디터 콘솔에만 남긴다. 확률 표시가 게임에 없는 규칙(기획서 §16)이라
    // 시뮬레이션과 수동 확인은 이 로그가 유일한 근거다.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void LogPull(ESlimeGrade grade, bool isSpecial, float specialChance)
    {
        Debug.Log($"[Gacha] {(isSpecial ? "특별" : "일반")} Lv.{(int)grade} (특별 확률 {specialChance:P1})");
    }
}
