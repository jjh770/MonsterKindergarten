using NUnit.Framework;
using UnityEngine;

// 뽑기 통 안 캡슐 더미의 물리가 통을 벗어나지 않고, 쌓이고, 빠진 자리를 메우는지 확인한다.
// 씬과 같은 크기(더미 514x494, 캡슐 140, 충돌 반지름 0.6배)로, 5x6 배치에서 6자리를 비운 24개로 시작한다.
public class GachaCapsulePileTests
{
    private static readonly Vector2 HalfSize = new(257f, 247f);
    private const float Radius = 70f * 0.6f;
    private const float Gravity = 2600f;

    private static GachaCapsulePile CreateFullPile()
    {
        var pile = new GachaCapsulePile(30);
        pile.Reset(HalfSize, Gravity);
        int index = 0;
        for (int row = 0; row < 6; row++)
        {
            for (int column = 0; column < 5; column++)
            {
                int slot = index++;
                if (column == row % 5) continue;

                pile.Place(slot, new Vector2(-192f + column * 96f, -190f + row * 76f), Radius, 0f);
            }
        }

        return pile;
    }

    [Test]
    public void 쌓인_캡슐은_모두_통_안에_있고_거의_겹치지_않는다()
    {
        GachaCapsulePile pile = CreateFullPile();
        pile.Simulate(2f);

        for (int i = 0; i < pile.Count; i++)
        {
            if (!pile.IsVisible(i)) continue;

            Vector2 position = pile.GetPosition(i);
            Assert.LessOrEqual(Mathf.Abs(position.x), HalfSize.x - Radius + 0.5f, $"{i}번 x");
            Assert.LessOrEqual(Mathf.Abs(position.y), HalfSize.y - Radius + 0.5f, $"{i}번 y");

            for (int j = i + 1; j < pile.Count; j++)
            {
                if (!pile.IsVisible(j)) continue;

                float distance = Vector2.Distance(position, pile.GetPosition(j));
                Assert.Greater(distance, Radius * 2f * 0.9f, $"{i}번과 {j}번이 너무 겹친다");
            }
        }
    }

    [Test]
    public void 중력으로_바닥부터_쌓인다()
    {
        GachaCapsulePile pile = CreateFullPile();
        pile.Simulate(2f);

        float lowest = float.MaxValue;
        for (int i = 0; i < pile.Count; i++)
        {
            if (!pile.IsVisible(i)) continue;

            lowest = Mathf.Min(lowest, pile.GetPosition(i).y);
        }

        Assert.AreEqual(-HalfSize.y + Radius, lowest, 1f, "맨 아래 캡슐은 바닥에 닿아 있어야 한다");
    }

    [Test]
    public void 빠뜨린_캡슐은_통_아래로_사라지고_나머지는_통_안에_남는다()
    {
        GachaCapsulePile pile = CreateFullPile();
        pile.Simulate(2f);

        int dropped = pile.DropLowest(-136f);
        Assert.GreaterOrEqual(dropped, 0);
        Assert.IsFalse(pile.IsInPile(dropped));

        pile.Simulate(2f);
        Assert.IsFalse(pile.IsVisible(dropped), "빠진 캡슐은 몸체 뒤로 내려가 감춰져야 한다");
        int remaining = 0;
        for (int i = 0; i < pile.Count; i++)
        {
            if (i == dropped || !pile.IsVisible(i)) continue;

            remaining++;
            Assert.LessOrEqual(Mathf.Abs(pile.GetPosition(i).y), HalfSize.y - Radius + 0.5f, $"{i}번 y");
        }

        Assert.AreEqual(23, remaining, "빠뜨린 하나 말고는 모두 통 안에 보여야 한다");
    }

    [Test]
    public void 튀겨_올린_캡슐은_떠올랐다가_다시_가라앉는다()
    {
        GachaCapsulePile pile = CreateFullPile();
        pile.Simulate(2f);

        int top = -1;
        for (int i = 0; i < pile.Count; i++)
        {
            if (!pile.IsVisible(i)) continue;
            if (top < 0 || pile.GetPosition(i).y > pile.GetPosition(top).y) top = i;
        }

        float rest = pile.GetPosition(top).y;
        pile.Kick(top, new Vector2(0f, 1300f));
        pile.Simulate(0.15f);
        Assert.Greater(pile.GetPosition(top).y, rest + 20f, "튀겨 올리면 떠올라야 한다");

        pile.Simulate(2f);
        Assert.Less(pile.GetPosition(top).y, rest + 20f, "시간이 지나면 다시 가라앉아야 한다");
    }

    [Test]
    public void 통이_오른쪽으로_가속하면_캡슐은_왼쪽으로_쏠렸다가_돌아온다()
    {
        GachaCapsulePile pile = CreateFullPile();
        pile.Simulate(2f);
        float rest = AverageX(pile);

        // 통을 오른쪽으로 홱 당긴 것과 같다. 통 안에서는 왼쪽으로 미는 힘이 된다.
        pile.FrameAcceleration = new Vector2(-6000f, 0f);
        pile.Simulate(0.3f);
        Assert.Less(AverageX(pile), rest - 15f, "캡슐이 왼쪽으로 쏠려야 한다");

        pile.FrameAcceleration = Vector2.zero;
        pile.Simulate(3f);
        for (int i = 0; i < pile.Count; i++)
        {
            if (!pile.IsVisible(i)) continue;

            Vector2 position = pile.GetPosition(i);
            Assert.LessOrEqual(Mathf.Abs(position.x), HalfSize.x - Radius + 0.5f, $"{i}번 x");
            Assert.LessOrEqual(Mathf.Abs(position.y), HalfSize.y - Radius + 0.5f, $"{i}번 y");
        }
    }

    private static float AverageX(GachaCapsulePile pile)
    {
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < pile.Count; i++)
        {
            if (!pile.IsVisible(i)) continue;

            sum += pile.GetPosition(i).x;
            count++;
        }

        return sum / count;
    }

    [Test]
    public void 쌓인_더미_위에는_굴러다닐_공간이_남는다()
    {
        GachaCapsulePile pile = CreateFullPile();
        pile.Simulate(2f);

        float highest = float.MinValue;
        for (int i = 0; i < pile.Count; i++)
        {
            if (pile.IsVisible(i)) highest = Mathf.Max(highest, pile.GetPosition(i).y + Radius);
        }

        // 캡슐 반지름의 1.5배 이상은 비어 있어야 흔들 때 출렁일 수 있다.
        Assert.Greater(HalfSize.y - highest, Radius * 1.5f, "통이 꽉 차면 흔들어도 캡슐이 움직일 자리가 없다");
    }
}
