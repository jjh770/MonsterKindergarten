using NUnit.Framework;

// 졸업식 뒤 포인트로 뽑기권을 사는 하루 기록의 규칙을 확인한다.
public class PointTicketPurchaseTests
{
    [Test]
    public void 같은_날에는_횟수가_쌓이고_한도에서_멈춘다()
    {
        var record = new PointTicketPurchase(0, 0);
        for (int i = 0; i < PointTicketPurchase.DailyLimit; i++) record = record.WithPurchased(100, 1);

        Assert.AreEqual(PointTicketPurchase.DailyLimit, record.GetCount(100));
        Assert.AreEqual(0, record.GetRemaining(100));
    }

    [Test]
    public void 다음_날이면_횟수가_다시_시작한다()
    {
        var record = new PointTicketPurchase(PointTicketPurchase.DailyLimit, 100);

        Assert.AreEqual(PointTicketPurchase.DailyLimit, record.GetRemaining(101));
        PointTicketPurchase next = record.WithPurchased(101, 1);
        Assert.AreEqual(1, next.Count);
        Assert.AreEqual(101, next.DayIndex);
    }

    [Test]
    public void 시계를_되돌려도_횟수가_다시_늘지_않는다()
    {
        var record = new PointTicketPurchase(PointTicketPurchase.DailyLimit, 100);

        Assert.AreEqual(0, record.GetRemaining(99));
        Assert.AreEqual(100, record.WithPurchased(99, 1).DayIndex);
    }

    [Test]
    public void 음수_기록은_만들_수_없다()
    {
        Assert.Throws<System.ArgumentException>(() => new PointTicketPurchase(-1, 0));
        Assert.Throws<System.ArgumentException>(() => new PointTicketPurchase(0, -1));
    }
}
