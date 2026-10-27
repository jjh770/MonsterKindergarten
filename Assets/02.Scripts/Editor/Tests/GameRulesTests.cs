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
