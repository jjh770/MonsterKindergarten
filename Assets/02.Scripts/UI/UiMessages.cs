using UnityEngine;

// 코드가 화면에 띄우는 짧은 문장의 단일 창구. 문구 자체는 UiMessages 에셋(UiMessagesSO)에 있다.
// 용어는 Documentation/CODING_CONVENTION.md의 "화면 문구와 용어"를 따른다.
//
// 모으지 않는 것:
//  - 씬에 직접 쓴 라벨: 글자 길이와 줄바꿈을 화면에서 보며 맞춘다.
//  - 튜토리얼 대사와 안내: TutorialContent 에셋이 이미 한곳에 모은다.
//  - 숫자가 섞이는 문장(정보창, 크레딧, 오프라인 보상): 값을 채우는 코드와 함께 있어야 읽힌다.
//  - 로그인과 저장 복구 안내: 실패 원인에 따라 갈라지는 흐름 안에 있다.
//
// 에셋은 씬에 둔 UiMessagesProvider가 연결한다. 연결이 빠지면 UiMessagesSO의 초기값으로
// 대신 읽고 경고를 남기므로, 문구가 빈칸으로 나오지는 않는다.
public static class UiMessages
{
    private static UiMessagesSO _source;
    private static UiMessagesSO _fallback;

    private static UiMessagesSO Source
    {
        get
        {
            if (_source != null)
            {
                return _source;
            }

            if (_fallback == null)
            {
                _fallback = ScriptableObject.CreateInstance<UiMessagesSO>();
                _fallback.hideFlags = HideFlags.HideAndDontSave;
                Debug.LogWarning("UiMessages 에셋이 연결되지 않아 기본 문구를 씁니다. 씬에 UiMessagesProvider가 있는지 확인하세요.");
            }

            return _fallback;
        }
    }

    public static void Use(UiMessagesSO source)
    {
        _source = source;
    }

    // 씬이 내려갈 때 자기가 연결한 에셋만 놓는다. 다음 씬의 Provider가 먼저 연결했을 수 있다.
    public static void Release(UiMessagesSO source)
    {
        if (_source == source)
        {
            _source = null;
        }
    }

    // 상단 게이지 상태
    public static string FieldFull => Source.FieldFull;
    public static string AutoSpawnOff => Source.AutoSpawnOff;

    // 하단 메뉴
    public static string EnterDisplayRoom => Source.EnterDisplayRoom;
    public static string ExitDisplayRoom => Source.ExitDisplayRoom;

    // 장식장으로 보내기와 꺼내기
    public static string MainFieldFullCannotTakeOut => Source.MainFieldFullCannotTakeOut;
    public static string CannotTakeOutNow => Source.CannotTakeOutNow;
    public static string SameKindInDisplayRoom => Source.SameKindInDisplayRoom;
    public static string CannotSendToDisplayRoom => Source.CannotSendToDisplayRoom;

    // 놀이기구 배치
    public static string PlacementUnavailable => Source.PlacementUnavailable;
    public static string TooCloseToOther => Source.TooCloseToOther;
    public static string CannotMoveHere => Source.CannotMoveHere;
    public static string CannotPlaceHere => Source.CannotPlaceHere;
    public static string PlaceInsideRoom => Source.PlaceInsideRoom;

    // 상점
    public static string NotEnoughPoints => Source.NotEnoughPoints;
    public static string ShopObjectsEmpty => Source.ShopObjectsEmpty;
    public static string ShopThemesEmpty => Source.ShopThemesEmpty;

    // 자동 합성
    public static string NoMergePair => Source.NoMergePair;

    // 뽑기
    public static string NoTicket => Source.NoTicket;
    public static string NoRoomForPull => Source.NoRoomForPull;
    public static string MachineInsertTap => Source.MachineInsertTap;
    public static string MachineCapsuleTap => Source.MachineCapsuleTap;
    public static string SpecialSlimeSubtitle => Source.SpecialSlimeSubtitle;
    public static string TicketObtained => Source.TicketObtained;
}
