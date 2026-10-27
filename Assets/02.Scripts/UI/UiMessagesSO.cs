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

    [Header("소품 배치")]
    [SerializeField] private string _placementUnavailable = "지금은 배치할 수 없어요.";
    [Tooltip("소품 놓기에서 아직 없는 상점 소품을 눌렀을 때의 안내입니다.")]
    [SerializeField] private string _placementFromShop = "상점의 소품 탭에서 살 수 있어요.";
    [Tooltip("소품 놓기에서 아직 없는 탐험 소품을 눌렀을 때의 안내입니다.")]
    [SerializeField] private string _placementFromExpedition = "탐험을 보내면 발견할 수 있어요.";
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
    [SerializeField] private string _noTicket = "슬라임이 떨어뜨리는 뽑기권을 모아 보세요.";
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
    [SerializeField] private string _gradeReachReward = "해금 보상!";

    [Header("탐험")]
    [Tooltip("탐험이 끝났을 때 띄우는 토스트입니다. {0}에 탐험지 이름이 들어갑니다.")]
    [SerializeField] private string _expeditionReturned = "{0}에서 슬라임들이 돌아왔어요!";
    [SerializeField] private string _expeditionChooseParty = "아래 목록에서 함께 떠날 슬라임 3종을 골라 주세요.";
    [Tooltip("탐험 화면에서 고른 탐험지가 떠나 있을 때의 안내입니다. {0}에 탐험지 이름이 들어갑니다.")]
    [SerializeField] private string _expeditionInProgress = "{0} 중이에요. 돌아오면 보상을 받을 수 있어요.";
    [SerializeField] private string _expeditionReadyToClaim = "슬라임들이 돌아왔어요! 보상을 받아 주세요.";
    [Tooltip("돌아온 탐험지 탭에 시간 대신 쓰는 짧은 말입니다.")]
    [SerializeField] private string _expeditionArrived = "도착!";
    [Tooltip("보상을 받을 때 돌아온 슬라임들 위에 잠깐 뜨는 말입니다.")]
    [SerializeField] private string _expeditionHomecoming = "다녀왔어요!";
    [Tooltip("탐험 보상 팝업에서 장식을 처음 얻었을 때의 줄입니다. {0}에 장식 이름이 들어갑니다.")]
    [SerializeField] private string _expeditionDecorationFound = "{0} 발견!";
    [Tooltip("탐험 보상 팝업에서 이미 가진 장식이 나왔을 때의 줄입니다. {0}에 장식 이름이 들어갑니다.")]
    [SerializeField] private string _expeditionDecorationOwned = "{0} 발견! (이미 가지고 있어요)";
    [Tooltip("탐험 화면을 열 때 지급 기록을 맞추지 못했을 때의 안내입니다.")]
    [SerializeField] private string _expeditionSettlementUnavailable = "탐험 지급 기록을 확인할 수 없어 출발과 수령을 잠시 멈췄어요.";
    [Tooltip("이미 편성한 종류를 또 고르거나 자리가 없을 때의 안내입니다.")]
    [SerializeField] private string _expeditionPickDifferentKinds = "서로 다른 3종을 골라 주세요. 탐험지의 슬라임을 누르면 다시 빼요.";
    [SerializeField] private string _expeditionDeparted = "탐험을 떠났어요. 돌아오면 보상을 받을 수 있어요.";
    [SerializeField] private string _expeditionCannotDepart = "지금은 출발할 수 없어요. 편성과 탐험 상태를 다시 확인해 주세요.";
    [SerializeField] private string _expeditionCannotClaim = "아직 도착 전이거나 지급 기록을 확인할 수 없어요.";
    [Tooltip("편성 목록 위의 제목입니다. 가운데점(·)은 글꼴에 없어 |를 씁니다.")]
    [SerializeField] private string _expeditionMemberHeading = "장식장 슬라임 | 좌우로 밀어 골라요";
    [Tooltip("장식장에 슬라임이 없을 때 편성 목록 제목 대신 쓰는 말입니다.")]
    [SerializeField] private string _expeditionNoMembers = "장식장에 슬라임을 먼저 보관해 주세요.";
    [Tooltip("편성 목록에서 이미 고른 슬라임 이름 아래에 붙는 말입니다.")]
    [SerializeField] private string _expeditionMemberChosen = "편성됨";
    [Tooltip("편성 목록에서 탐험을 떠난 슬라임 이름 아래에 붙는 말입니다.")]
    [SerializeField] private string _expeditionMemberAway = "파견 중";
    [Tooltip("고른 탐험지가 비어 있을 때 아래 버튼의 글자입니다.")]
    [SerializeField] private string _expeditionSend = "탐험 보내기";
    [Tooltip("탐험이 돌아왔을 때 아래 버튼의 글자입니다.")]
    [SerializeField] private string _expeditionClaim = "보상 받기";
    [Tooltip("편성 칸과 목록에서 특별한 슬라임의 이름입니다. {0}에 슬라임 이름이 들어갑니다.")]
    [SerializeField] private string _expeditionSpecialName = "특별 {0}";
    [Tooltip("탐험 화면 제목입니다. 고른 탐험지가 떠나 있을 때 씁니다. {0}에 탐험지 이름이 들어갑니다.")]
    [SerializeField] private string _expeditionTitleInProgress = "{0}\n<size=35>탐험 중</size>";
    [Tooltip("탐험 화면 제목입니다. 고른 탐험지가 돌아왔을 때 씁니다. {0}에 탐험지 이름이 들어갑니다.")]
    [SerializeField] private string _expeditionTitleArrived = "{0}\n<size=35>도착!</size>";

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
    public string PlacementFromShop => _placementFromShop;
    public string PlacementFromExpedition => _placementFromExpedition;
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
    public string GradeReachReward => _gradeReachReward;
    public string ExpeditionReturned => _expeditionReturned;
    public string ExpeditionChooseParty => _expeditionChooseParty;
    public string ExpeditionInProgress => _expeditionInProgress;
    public string ExpeditionReadyToClaim => _expeditionReadyToClaim;
    public string ExpeditionArrived => _expeditionArrived;
    public string ExpeditionHomecoming => _expeditionHomecoming;
    public string ExpeditionDecorationFound => _expeditionDecorationFound;
    public string ExpeditionDecorationOwned => _expeditionDecorationOwned;
    public string ExpeditionSettlementUnavailable => _expeditionSettlementUnavailable;
    public string ExpeditionPickDifferentKinds => _expeditionPickDifferentKinds;
    public string ExpeditionDeparted => _expeditionDeparted;
    public string ExpeditionCannotDepart => _expeditionCannotDepart;
    public string ExpeditionCannotClaim => _expeditionCannotClaim;
    public string ExpeditionMemberHeading => _expeditionMemberHeading;
    public string ExpeditionNoMembers => _expeditionNoMembers;
    public string ExpeditionMemberChosen => _expeditionMemberChosen;
    public string ExpeditionMemberAway => _expeditionMemberAway;
    public string ExpeditionSend => _expeditionSend;
    public string ExpeditionClaim => _expeditionClaim;
    public string ExpeditionSpecialName => _expeditionSpecialName;
    public string ExpeditionTitleInProgress => _expeditionTitleInProgress;
    public string ExpeditionTitleArrived => _expeditionTitleArrived;
    public string AdNotReady => _adNotReady;
    public string AdLimitReached => _adLimitReached;
    public string AdBoostFull => _adBoostFull;
    public string AdLocked => _adLocked;
    public string AdFailed => _adFailed;
    public string AdClosedEarly => _adClosedEarly;
    public string AdBoostRewarded => _adBoostRewarded;
    public string AdTicketRewarded => _adTicketRewarded;
}
