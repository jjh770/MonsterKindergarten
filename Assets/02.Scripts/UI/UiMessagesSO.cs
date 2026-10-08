using UnityEngine;

// 코드가 직접 화면에 띄우는 짧은 문장을 담는 에셋. 인스펙터에서 고치면 다시 컴파일하지 않고 바로 바뀐다.
// 코드는 이 에셋을 직접 읽지 않고 UiMessages를 거친다. 항목을 더할 때는 여기에 필드를 만들고
// UiMessages에 같은 이름의 프로퍼티를 더한다. 필드 초기값은 연결이 빠졌을 때의 대비 문구이기도 하다.
[CreateAssetMenu(fileName = "UiMessages", menuName = "Monster Kindergarten/Ui Messages")]
public sealed class UiMessagesSO : ScriptableObject
{
    [Header("상단 게이지 상태")]
    [SerializeField] private string _fieldFull = "유치원이 꽉 찼어요!";
    [SerializeField] private string _autoSpawnOff = "등장을 잠시 멈췄어요";

    [Header("하단 메뉴")]
    [SerializeField] private string _enterDisplayRoom = "장식장 가기";
    [SerializeField] private string _exitDisplayRoom = "유치원 가기";

    [Header("장식장으로 보내기와 꺼내기")]
    [SerializeField] private string _mainFieldFullCannotTakeOut = "메인 필드가 가득 차서 꺼낼 수 없어요.";
    [SerializeField] private string _cannotTakeOutNow = "이 슬라임은 지금 꺼낼 수 없어요.";
    [SerializeField] private string _sameKindInDisplayRoom = "같은 종류의 슬라임이 이미 장식장에 있어요.";
    [SerializeField] private string _cannotSendToDisplayRoom = "이 슬라임은 장식장에 보낼 수 없어요.";

    [Header("놀이기구 배치")]
    [SerializeField] private string _placementUnavailable = "지금은 배치할 수 없어요.";
    [SerializeField] private string _tooCloseToOther = "다른 것과 너무 가까워요.";
    [SerializeField] private string _cannotMoveHere = "여기에는 옮길 수 없어요.";
    [SerializeField] private string _cannotPlaceHere = "여기에는 놓을 수 없어요.";
    [SerializeField] private string _placeInsideRoom = "장식장 안쪽에 놓아 주세요.";

    [Header("상점")]
    [SerializeField] private string _notEnoughPoints = "포인트가 모자라요.";
    [SerializeField] private string _shopObjectsEmpty = "지금은 살 수 있는 물건이 없어요.";
    [SerializeField] private string _shopThemesEmpty = "새 배경은 준비 중이에요.";

    [Header("자동 합성")]
    [SerializeField] private string _noMergePair = "합성할 수 있는 슬라임이 없어요.";

    [Header("뽑기")]
    [SerializeField] private string _noTicket = "슬라임이 떨어뜨리는 뽑기권을 모아보세요.";
    [TextArea] [SerializeField] private string _noRoomForPull = "유치원이 가득 찼어요.\n슬라임을 합쳐 자리를 만들어 주세요.";
    [SerializeField] private string _machineChoose = "횟수를 골라 뽑기권을 넣어요";
    [SerializeField] private string _machineTicketCount = "보유 뽑기권 : {0}장";
    [TextArea] [SerializeField] private string _machineTutorialPull = "1회 뽑기 버튼을 눌러\n뽑기권을 넣어 보세요!";
    [TextArea] [SerializeField] private string _machineAllOpened = "모두 확인했어요!\n화면을 눌러 필드로 보내요";
    [SerializeField] private string _specialSlimeShort = "★ 특별한 슬라임";
    [TextArea] [SerializeField] private string _pullFailed = "뽑기를 하지 못했어요.\n잠시 뒤에 다시 시도해 주세요.";
    [SerializeField] private string _machineCapsuleTap = "캡슐을 톡 눌러 열어 보세요";
    [SerializeField] private string _specialSlimeSubtitle = "뭔가 특별해 보여요...!";
    [SerializeField] private string _ticketObtained = "뽑기권 획득!";

    [Header("광고")]
    [SerializeField] private string _adNotReady = "지금은 볼 수 있는 광고가 없어요.";
    [SerializeField] private string _adLimitReached = "오늘은 모두 봤어요.";
    [SerializeField] private string _adBoostFull = "이미 충분히 켜져 있어요.";
    [SerializeField] private string _adLocked = "뽑기가 열리면 볼 수 있어요.";
    [TextArea] [SerializeField] private string _adFailed = "광고를 보여 주지 못했어요.\n잠시 뒤에 다시 해 주세요.";
    [SerializeField] private string _adClosedEarly = "광고를 끝까지 봐야 보상을 받아요.";
    [SerializeField] private string _adBoostRewarded = "포인트 2배 시간이 늘었어요!";
    [SerializeField] private string _adTicketRewarded = "뽑기권을 받았어요!";

    public string FieldFull => _fieldFull;
    public string AutoSpawnOff => _autoSpawnOff;
    public string EnterDisplayRoom => _enterDisplayRoom;
    public string ExitDisplayRoom => _exitDisplayRoom;
    public string MainFieldFullCannotTakeOut => _mainFieldFullCannotTakeOut;
    public string CannotTakeOutNow => _cannotTakeOutNow;
    public string SameKindInDisplayRoom => _sameKindInDisplayRoom;
    public string CannotSendToDisplayRoom => _cannotSendToDisplayRoom;
    public string PlacementUnavailable => _placementUnavailable;
    public string TooCloseToOther => _tooCloseToOther;
    public string CannotMoveHere => _cannotMoveHere;
    public string CannotPlaceHere => _cannotPlaceHere;
    public string PlaceInsideRoom => _placeInsideRoom;
    public string NotEnoughPoints => _notEnoughPoints;
    public string ShopObjectsEmpty => _shopObjectsEmpty;
    public string ShopThemesEmpty => _shopThemesEmpty;
    public string NoMergePair => _noMergePair;
    public string NoTicket => _noTicket;
    public string NoRoomForPull => _noRoomForPull;
    public string MachineChoose => _machineChoose;
    public string MachineTicketCount => _machineTicketCount;
    public string MachineTutorialPull => _machineTutorialPull;
    public string MachineAllOpened => _machineAllOpened;
    public string SpecialSlimeShort => _specialSlimeShort;
    public string PullFailed => _pullFailed;
    public string MachineCapsuleTap => _machineCapsuleTap;
    public string SpecialSlimeSubtitle => _specialSlimeSubtitle;
    public string TicketObtained => _ticketObtained;
    public string AdNotReady => _adNotReady;
    public string AdLimitReached => _adLimitReached;
    public string AdBoostFull => _adBoostFull;
    public string AdLocked => _adLocked;
    public string AdFailed => _adFailed;
    public string AdClosedEarly => _adClosedEarly;
    public string AdBoostRewarded => _adBoostRewarded;
    public string AdTicketRewarded => _adTicketRewarded;
}
