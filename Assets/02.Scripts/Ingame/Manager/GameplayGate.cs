// 게임 씬에서 "지금 무엇을 해도 되는가"를 판정하는 자리.
//
// 같은 모양의 조건이 열두 파일에 흩어져 있었다. 조건이 하나 늘 때마다 흩어진
// 자리를 전부 찾아야 하고, 실제로는 새 UI를 만들 때마다 옆의 것을 복사해 왔다.
// 도감과 장식장 UI는 같은 함수를 글자 하나 다르지 않게 각자 갖고 있었다.
//
// 여기 모으는 것은 "판정에 무엇이 들어가는가"뿐이다. 그 결과로 무엇을 할지는
// 여전히 각자가 정한다. 넷으로 나눈 이유는 실제로 쓰는 조건이 넷이기 때문이고,
// 억지로 하나로 합치면 필요 없는 조건까지 지고 가게 된다.
//
// 매니저가 아직 없을 때는 모두 false다. 씬이 올라오는 중이거나 내려가는 중이니
// 아무것도 하지 않는 편이 맞다.
public static class GameplayGate
{
    // 조작과 진행이 살아 있는가. 오프라인 보상 팝업이 떠 있거나 진행도를
    // 초기화하는 중이면 false다. 매 프레임 도는 쪽이 주로 쓴다.
    public static bool IsActive =>
        GameManager.Instance != null &&
        GameManager.Instance.IsGameplayActive;

    // 위에 더해 세 저장 문서가 모두 올라온 뒤인가. 진행도를 읽어 그리는 UI가 쓴다.
    // 읽기 전에 그리면 기본값을 잠깐 보여 주게 된다.
    public static bool IsReady =>
        GameManager.Instance != null &&
        GameManager.Instance.IsAllDataInitialized &&
        GameManager.Instance.IsGameplayActive;

    // 필드 위에 얹히는 UI가 쓴다. 장식장을 보고 있으면 false다.
    public static bool IsMainStageReady =>
        IsReady &&
        StageManager.Instance != null &&
        StageManager.Instance.IsMainStageActive;

    // 장식장으로 가는 입구를 열어도 되는가. 해금은 도메인이 판단하고 여기서는
    // 그것을 지금 보여 줘도 되는지만 더한다.
    public static bool IsDisplayRoomAvailable =>
        IsReady &&
        SlimeManager.Instance != null &&
        SlimeManager.Instance.IsDisplayRoomUnlocked;
}
