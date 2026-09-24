// 등록 순서와 무관한 우선순위. 팝업·연출 차단은 튜토리얼 선택보다 우선한다.
public enum ClickerInputPriority
{
    Space,
    Selection,
    Tutorial,
    Modal,
}

// Clicker의 월드 입력 상태 한 벌.
// 값 타입이라 소유자별로 쌓아 두었다가 그대로 꺼내 쓸 수 있다.
public readonly struct ClickerInputMode
{
    public bool ClickEnabled { get; }
    public bool DragEnabled { get; }
    public bool InvokeClickAction { get; }
    public SlimeController RestrictedTarget { get; }
    public SlimeController SecondaryRestrictedTarget { get; }

    // 누르고 있기만 해도 드래그로 넘어갈지. 메인 필드는 연타하는 곳이라 이 편이
    // 집기 쉽지만, 탭과 집기가 서로 다른 일을 하는 곳에서는 신중하게 누른 탭이
    // 시간 기준에 걸려 엉뚱한 쪽으로 간다.
    public bool HoldStartsDrag { get; }

    public ClickerInputMode(
        bool clickEnabled,
        bool dragEnabled,
        SlimeController restrictedTarget = null,
        SlimeController secondaryRestrictedTarget = null,
        bool invokeClickAction = true,
        bool holdStartsDrag = true)
    {
        ClickEnabled = clickEnabled;
        DragEnabled = dragEnabled;
        RestrictedTarget = restrictedTarget;
        SecondaryRestrictedTarget = secondaryRestrictedTarget;
        InvokeClickAction = invokeClickAction;
        HoldStartsDrag = holdStartsDrag;
    }

    // 대사, 팝업, 연출 중 월드 입력을 완전히 막는다.
    public static ClickerInputMode Blocked => new(false, false);

    // 메인 필드 평상시. 터치 포인트와 드래그 합성을 모두 허용한다.
    public static ClickerInputMode Free => new(true, true);

    // 선택만 허용한다. 클릭 포인트는 지급하지 않는다.
    // 장식장(기획서 §7.2)과 장식장 이동 선택 모드(§7.4)가 쓴다.
    public static ClickerInputMode SelectOnly(SlimeController restrictedTarget = null)
    {
        return new ClickerInputMode(
            clickEnabled: true,
            dragEnabled: false,
            restrictedTarget: restrictedTarget,
            invokeClickAction: false);
    }

    // 장식장 평상시(기획서 §7.2). 탭하면 관찰, 끌면 집어서 던진다.
    // 클릭 포인트는 주지 않고, 합성은 CanMergeWith가 양쪽 MainField를 요구해 막힌다.
    //
    // 누르고 있는 시간으로는 드래그를 시작하지 않는다. 관찰은 신중하게 누르는
    // 동작이라 0.2초 기준을 자주 넘기고, 그러면 탭인데 슬라임이 딸려 온다.
    // 여기서는 손가락이 실제로 움직여야 집힌다.
    public static ClickerInputMode ObserveAndThrow()
    {
        return new ClickerInputMode(
            clickEnabled: true,
            dragEnabled: true,
            invokeClickAction: false,
            holdStartsDrag: false);
    }

    // 지정한 슬라임만 클릭할 수 있다. 튜토리얼 안내용.
    public static ClickerInputMode ClickOnly(SlimeController target)
    {
        return new ClickerInputMode(true, false, target);
    }

    // 지정한 슬라임만 드래그할 수 있다. 합성 안내는 두 번째 대상까지 허용한다.
    public static ClickerInputMode DragOnly(
        SlimeController target,
        SlimeController secondaryTarget = null)
    {
        return new ClickerInputMode(false, true, target, secondaryTarget);
    }
}
