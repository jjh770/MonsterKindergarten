using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Utility;

// 화면과 저장을 거치지 않는 게임 규칙을 확인한다. 밸런스 값을 바꾸면 여기 적힌 기대값도
// 함께 고쳐야 하며, 그때 실패하는 테스트가 바뀐 규칙이 무엇인지 알려 준다.
//
// EditMode에서는 매니저 싱글턴이 없으므로 PointCalculator는 업그레이드와 도감 보너스 0으로 계산한다.
public class GameRulesTests
{
    private const string UpgradeTablePath = "Assets/02.Scripts/Outgame/Feature/Upgrade/UpgradeSpecTableSO.asset";

    [TestCase(10d, false, 1d, 10d)]
    [TestCase(10d, true, 1d, 30d)]
    [TestCase(10d, false, 2d, 20d)]
    [TestCase(10d, true, 2d, 60d)]
    [TestCase(2.5d, false, 1d, 3d)]
    [TestCase(10.4d, false, 1d, 10d)]
    public void 포인트는_특별배율과_부스트를_곱한_뒤_반올림한다(
        double basePoint, bool isSpecial, double boost, double expected)
    {
        double point = PointCalculator.Calculate(
            basePoint, ESlimeGrade.Grade1, EClickType.Manual, isSpecial, boost);

        Assert.AreEqual(expected, point);
    }

    [TestCase(-1, 0d)]
    [TestCase(0, 0d)]
    [TestCase(2, 0d)]
    [TestCase(3, 10d)]
    [TestCase(14, 40d)]
    [TestCase(15, 50d)]
    [TestCase(20, 50d)]
    public void 도감_보너스는_3종마다_10퍼센트이고_15종에서_멈춘다(int registered, double expected)
    {
        Assert.AreEqual(expected, NormalCollectionRules.GetPointBonusPercent(registered));
    }

    [TestCase(ESlimeGrade.Grade12, 0)]
    [TestCase(ESlimeGrade.Grade13, 1)]
    [TestCase(ESlimeGrade.Grade15, 1)]
    [TestCase(ESlimeGrade.Grade16, 2)]
    [TestCase(ESlimeGrade.Grade18, 2)]
    [TestCase(ESlimeGrade.Grade19, 3)]
    [TestCase(ESlimeGrade.Grade20, 3)]
    public void 후반_등급의_첫_도달은_뽑기권을_준다(ESlimeGrade grade, int expected)
    {
        Assert.AreEqual(expected, GradeReachTicketRewards.GetTicketCount(grade));
    }

    // 장식장은 Lv.6부터 열리지만, Lv.3에서 열리던 때 이미 장식장을 쓴 계정은 닫지 않는다.
    // 등록이 하나라도 있으면 그 계정이다.
    [TestCase(ESlimeGrade.Grade5, false, false)]
    [TestCase(ESlimeGrade.Grade6, false, true)]
    [TestCase(ESlimeGrade.Grade3, true, true)]
    [TestCase(ESlimeGrade.Grade5, true, true)]
    public void 장식장은_Lv6부터_열리고_이미_등록한_계정은_열린_채로_둔다(
        ESlimeGrade highest, bool hasRegistration, bool expected)
    {
        SlimeStatusSaveData data = SlimeStatusSaveData.Default;
        data.HighestGrade = (int)highest;
        if (hasRegistration) data.NormalCollectionRegistered[0] = true;
        Assert.IsTrue(SlimeStatusSaveMapper.TryRestore(
            data, DateTime.UtcNow, out SlimeStatus status, out _, out _, out string failure), failure);

        var owner = new UnityEngine.GameObject("SlimeManagerTest");
        try
        {
            SlimeManager manager = owner.AddComponent<SlimeManager>();
            typeof(SlimeManager)
                .GetField("_status", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(manager, status);
            Assert.AreEqual(expected, manager.IsDisplayRoomUnlocked);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    // 뽑기는 Lv.8부터 열리지만, Lv.7에서 열리던 때 이미 뽑기권을 받은 계정은 닫지 않는다.
    [TestCase(ESlimeGrade.Grade7, 0L, 0, false)]
    [TestCase(ESlimeGrade.Grade8, 0L, 0, true)]
    [TestCase(ESlimeGrade.Grade7, 1L, 0, true)]
    [TestCase(ESlimeGrade.Grade7, 0L, 2, true)]
    public void 뽑기는_Lv8부터_열리고_이미_뽑기권을_받은_계정은_열린_채로_둔다(
        ESlimeGrade highest, long ticketsObtained, int pendingTickets, bool expected)
    {
        SlimeStatusSaveData data = SlimeStatusSaveData.Default;
        data.HighestGrade = (int)highest;
        data.GachaTicketsObtainedTotal = ticketsObtained;
        data.PendingTickets = pendingTickets;
        Assert.AreEqual(expected, CreateManager(data, out UnityEngine.GameObject owner).IsGachaUnlocked);
        UnityEngine.Object.DestroyImmediate(owner);
    }

    // 상점과 배경은 Lv.10부터 열리지만, Lv.9에서 열리던 때 이미 상점에서 산 계정은 닫지 않는다.
    // 탐험 장식(꽃밭, 인덱스 2)만 가진 계정은 상점을 쓴 적이 없다.
    [TestCase(ESlimeGrade.Grade9, -1, -1, false)]
    [TestCase(ESlimeGrade.Grade10, -1, -1, true)]
    [TestCase(ESlimeGrade.Grade9, (int)EBackgroundTheme.Sky, -1, true)]
    [TestCase(ESlimeGrade.Grade9, -1, (int)EPlaygroundObjectType.Bumper, true)]
    [TestCase(ESlimeGrade.Grade9, -1, (int)EPlaygroundObjectType.FlowerPatch, false)]
    public void 상점은_Lv10부터_열리고_이미_상점에서_산_계정은_열린_채로_둔다(
        ESlimeGrade highest, int ownedTheme, int ownedObject, bool expected)
    {
        SlimeStatusSaveData data = SlimeStatusSaveData.Default;
        data.HighestGrade = (int)highest;
        if (ownedTheme >= 0)
        {
            data.OwnedBackgroundThemes = new List<int> { ownedTheme };
            data.SelectedBackgroundTheme = ownedTheme;
        }

        if (ownedObject >= 0)
        {
            data.OwnedPlaygroundObjects = new List<int>(new int[(int)EPlaygroundObjectType.Count]);
            data.OwnedPlaygroundObjects[ownedObject] = 1;
        }

        SlimeManager manager = CreateManager(data, out UnityEngine.GameObject owner);
        Assert.AreEqual(expected, manager.IsShopUnlocked);
        Assert.AreEqual(expected, manager.IsBackgroundThemeUnlocked);
        // 산 배경을 고른 채로 저장한 계정은 다음 접속에서도 그 배경이 남아야 한다.
        if (ownedTheme >= 0) Assert.AreEqual((EBackgroundTheme)ownedTheme, manager.SelectedBackgroundTheme);
        UnityEngine.Object.DestroyImmediate(owner);
    }

    // 탐험은 Lv.12부터 열리지만, 그 전에 한 번이라도 보낸 계정은 닫지 않는다.
    [TestCase(ESlimeGrade.Grade11, false, false)]
    [TestCase(ESlimeGrade.Grade12, false, true)]
    [TestCase(ESlimeGrade.Grade8, true, true)]
    public void 탐험은_Lv12부터_열리고_이미_보낸_계정은_열린_채로_둔다(
        ESlimeGrade highest, bool hasSettlement, bool expected)
    {
        SlimeStatusSaveData data = SlimeStatusSaveData.Default;
        data.HighestGrade = (int)highest;
        if (hasSettlement)
        {
            data.ExpeditionSettlement = new ExpeditionSettlementSaveData
            {
                Sequence = 1,
                Points = 100d,
                Tickets = 1,
                CompletedDepartures = new Dictionary<string, long> { ["expedition-short"] = 1 },
            };
            // 탐험으로 받은 뽑기권은 누적 획득 수에도 들어가 있어야 저장이 맞는다.
            data.GachaTicketsObtainedTotal = 1;
        }

        Assert.AreEqual(expected, CreateManager(data, out UnityEngine.GameObject owner).IsExpeditionUnlocked);
        UnityEngine.Object.DestroyImmediate(owner);
    }

    private static SlimeManager CreateManager(SlimeStatusSaveData data, out UnityEngine.GameObject owner)
    {
        Assert.IsTrue(SlimeStatusSaveMapper.TryRestore(
            data, DateTime.UtcNow, out SlimeStatus status, out NormalSlimeCollectionStats stats, out _, out string failure), failure);
        owner = new UnityEngine.GameObject("SlimeManagerTest");
        SlimeManager manager = owner.AddComponent<SlimeManager>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(SlimeManager).GetField("_status", flags).SetValue(manager, status);
        typeof(SlimeManager).GetField("_collectionStats", flags).SetValue(manager, stats);
        return manager;
    }

    [Test]
    public void 최고_등급이_오르면_같은_상태에_뽑기권이_쌓인다()
    {
        SlimeStatusSaveData data = SlimeStatusSaveData.Default;
        data.HighestGrade = (int)ESlimeGrade.Grade12;
        Assert.IsTrue(SlimeStatusSaveMapper.TryRestore(
            data, DateTime.UtcNow, out SlimeStatus status, out _, out _, out string failure), failure);
        int before = status.PendingTickets;

        status.UpdateHighestGrade(ESlimeGrade.Grade13);
        Assert.AreEqual(before + 1, status.PendingTickets);

        // 한 번에 여러 등급을 건너도 지나친 등급의 몫을 모두 더한다: 14(1) + 15(1) + 16(2)
        status.UpdateHighestGrade(ESlimeGrade.Grade16);
        Assert.AreEqual(before + 5, status.PendingTickets);
    }

    [TestCase(0, 0.03f)]
    [TestCase(1, 0.035f)]
    [TestCase(14, 0.10f)]
    [TestCase(100, 0.10f)]
    public void 특별_피버_확률은_실패마다_오르고_10퍼센트에서_멈춘다(int misses, float expected)
    {
        Assert.AreEqual(expected, SpecialGachaFever.GetChance(misses), 1e-6f);
    }

    [Test]
    public void 뽑기_확률표는_합이_1이고_낮은_등급일수록_높다()
    {
        for (int highest = 1; highest <= 20; highest++)
        {
            List<NormalGachaProbability> table = NormalGachaPool.GetProbabilities((ESlimeGrade)highest);
            Assert.AreEqual(highest, table.Count, $"최고 Lv.{highest}의 후보 수");

            double sum = 0d;
            double previous = double.MaxValue;
            foreach (NormalGachaProbability row in table)
            {
                Assert.LessOrEqual((int)row.Grade, highest, "최고 등급 위는 후보가 아니다");
                Assert.Less(row.Probability, previous, "등급이 오를수록 확률이 줄어야 한다");
                previous = row.Probability;
                sum += row.Probability;
            }

            Assert.AreEqual(1d, sum, 1e-9, $"최고 Lv.{highest}의 확률 합");
        }
    }

    [Test]
    public void 뽑기는_최고_등급을_넘는_결과를_만들지_않는다()
    {
        for (int i = 0; i < 2000; i++)
        {
            Assert.IsTrue(NormalGachaPool.TryPick(ESlimeGrade.Grade7, out NormalGachaResult result));
            Assert.LessOrEqual((int)result.Grade, (int)ESlimeGrade.Grade7);
            Assert.GreaterOrEqual((int)result.Grade, (int)ESlimeGrade.Grade1);

            Assert.IsTrue(NormalGachaPool.TryPickSpecial(ESlimeGrade.Grade7, out ESlimeGrade special));
            Assert.LessOrEqual((int)special, (int)ESlimeGrade.Grade7);
        }
    }

    [Test]
    public void 튜토리얼_뽑기는_최고와_그_바로_아래만_나온다()
    {
        for (int i = 0; i < 500; i++)
        {
            Assert.IsTrue(NormalGachaPool.TryPickTutorial(ESlimeGrade.Grade7, out NormalGachaResult result));
            Assert.That((int)result.Grade, Is.InRange(6, 7));

            Assert.IsTrue(NormalGachaPool.TryPickTutorial(ESlimeGrade.Grade1, out NormalGachaResult first));
            Assert.AreEqual(ESlimeGrade.Grade1, first.Grade, "Lv.1 아래로는 내려가지 않는다");
        }
    }

    [TestCase(0, EGachaRarity.Jackpot)]
    [TestCase(1, EGachaRarity.Jackpot)]
    [TestCase(2, EGachaRarity.Rare)]
    [TestCase(3, EGachaRarity.Rare)]
    [TestCase(4, EGachaRarity.Uncommon)]
    [TestCase(6, EGachaRarity.Uncommon)]
    [TestCase(7, EGachaRarity.Common)]
    public void 희귀도는_최고_등급과의_거리로_정한다(int distance, EGachaRarity expected)
    {
        Assert.AreEqual(expected, NormalGachaPool.GetRarity(20, 20 - distance));
    }

    [TestCase("범퍼", "범퍼를")]
    [TestCase("대포", "대포를")]
    [TestCase("꽃밭", "꽃밭을")]
    [TestCase("반딧불 랜턴", "반딧불 랜턴을")]
    [TestCase("abc", "abc를")]
    [TestCase("", "를")]
    public void 목적격_조사는_받침에_맞춘다(string word, string expected)
    {
        Assert.AreEqual(expected, KoreanParticle.Object(word));
    }

    [TestCase(0d, "0")]
    [TestCase(999d, "999")]
    [TestCase(1234d, "1.23K")]
    [TestCase(12345d, "12.3K")]
    [TestCase(999999d, "1.00M")]
    [TestCase(1e12d, "1.00T")]
    [TestCase(1e15d, "1.00aa")]
    public void 큰_수는_단위를_붙여_세_자리로_줄인다(double value, string expected)
    {
        Assert.AreEqual(expected, value.ToFormattedString());
    }

    [Test]
    public void 큰_수_표기는_지수_표기로_나오지_않는다()
    {
        for (double value = 1d; value < 1e300; value *= 7.3d)
        {
            string text = value.ToFormattedString();
            StringAssert.DoesNotContain("E+", text, $"{value}");
        }
    }

    [TestCase(1, 1d)]
    [TestCase(50, 50d)]
    [TestCase(100, 75d)]
    [TestCase(200, 150d)]
    public void 포인트_획득량_업그레이드는_구간마다_다른_증가폭을_가진다(int level, double expectedPercent)
    {
        Upgrade upgrade = new Upgrade(GetSpec(EUpgradeType.AllSlimePointPercentAdd), level);

        Assert.AreEqual(expectedPercent, upgrade.Point, 1e-9);
    }

    [Test]
    public void 포인트_획득량_업그레이드_비용은_100레벨까지_125퍼센트씩_오르고_이후_1T부터_다시_오른다()
    {
        UpgradeSpecData spec = GetSpec(EUpgradeType.AllSlimePointPercentAdd);

        Assert.AreEqual(100d, (double)new Upgrade(spec, 0).Cost);
        Assert.AreEqual(Math.Ceiling(100d * Math.Pow(1.25d, 10)), (double)new Upgrade(spec, 10).Cost);
        Assert.AreEqual(1e12d, (double)new Upgrade(spec, 100).Cost);
        Assert.AreEqual(Math.Ceiling(1e12d * 1.025d), (double)new Upgrade(spec, 101).Cost);
    }

    [Test]
    public void 업그레이드_표의_모든_항목은_도메인_검증을_통과한다()
    {
        foreach (UpgradeSpecData spec in LoadUpgradeTable().Datas)
        {
            Assert.DoesNotThrow(() => new Upgrade(spec, 0), $"{spec.Type}");
            Assert.DoesNotThrow(() => new Upgrade(spec, spec.MaxLevel), $"{spec.Type}");
        }
    }

    private static UpgradeSpecData GetSpec(EUpgradeType type)
    {
        foreach (UpgradeSpecData spec in LoadUpgradeTable().Datas)
        {
            if (spec.Type == type) return spec;
        }

        Assert.Fail($"업그레이드 표에 {type}이 없다");
        return null;
    }

    private static UpgradeSpecTableSO LoadUpgradeTable()
    {
        var table = AssetDatabase.LoadAssetAtPath<UpgradeSpecTableSO>(UpgradeTablePath);
        Assert.IsNotNull(table, UpgradeTablePath);
        return table;
    }
}
