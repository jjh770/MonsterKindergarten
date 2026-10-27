// 최고 해금 등급으로 열리는 기능들의 경계를 한곳에 모은다.
// 새 해금 조건을 추가할 때는 호출부에 등급을 직접 쓰지 말고 여기에 상수를 만든 뒤
// SlimeManager의 IsXxxUnlocked 질의를 통해 사용한다.
public static class UnlockGrades
{
    // 기획서 §7.1 - 장식장 + 도감
    public const ESlimeGrade DisplayRoom = ESlimeGrade.Grade3;

    // 기획서 §11.1 - 뽑기권 드랍 + 뽑기 시스템 (Phase 4에서 사용 예정)
    public const ESlimeGrade Gacha = ESlimeGrade.Grade7;

    // 9레벨 달성 시 왼쪽 서랍(상점)의 손잡이가 나타난다. 상점의 물건(놀이터 오브젝트와 배경)은
    // 장식장과 배경 테마가 열린 뒤에 쓸 수 있고, 가장 싼 물건도 모으는 데 한참 걸려서
    // 포인트가 모일 즈음에 알려 주려고 이 등급에 둔다.
    public const ESlimeGrade Shop = ESlimeGrade.Grade9;

    // 최대 개체 수 업그레이드의 후반 31~50 구간은 기존처럼 Lv.11에서 열린다.
    public const ESlimeGrade MaxCountExpansion = ESlimeGrade.Grade11;

    // 상위 슬라임 등장(Lv.5)은 여기 두지 않는다.
    // SpawnWeightTable의 _spawnCaps에서 파생되는 값이므로 상수로 복제하면
    // 에셋을 조정할 때 조용히 어긋난다. SlimeManager.IsHigherGradeSpawnUnlocked를 쓴다.
}

// 후반 등급을 처음 만들면 뽑기권을 준다. Lv.13부터는 새로 열리는 기능이 거의 없고
// 한 등급에 걸리는 시간은 가장 길어서, 그 구간에 받을 것을 두려고 넣었다.
//
// 저장에서는 먼저 아직 줍지 않은 뽑기권(SlimeStatus.PendingTickets)에 더한다. 최고 등급과
// 같은 문서라 한 번의 저장에 함께 들어가므로, 둘 사이에 앱이 꺼져도 보상을 잃거나 두 번
// 받지 않는다. 지갑(Currency)에 바로 넣으면 문서가 둘로 갈라진다. 화면에서는 해금 팝업 뒤의
// 보상 팝업(UnlockPopupUI)이 보여 주고, 티켓이 상단 바에 닿을 때 지갑으로 옮긴다.
// 이미 이 등급을 지난 계정에는 소급해 주지 않는다. 받은 기록을 따로 저장하지 않기 때문이다.
public static class GradeReachTicketRewards
{
    public static int GetTicketCount(ESlimeGrade grade)
    {
        if (grade >= ESlimeGrade.Grade19) return 3;
        if (grade >= ESlimeGrade.Grade16) return 2;
        if (grade >= ESlimeGrade.Grade13) return 1;
        return 0;
    }

    // from보다 높고 to 이하인 등급의 보상을 더한다.
    public static int GetTicketCount(ESlimeGrade from, ESlimeGrade to)
    {
        int total = 0;
        for (ESlimeGrade grade = from + 1; grade <= to; grade++)
        {
            total += GetTicketCount(grade);
        }

        return total;
    }
}
