using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum DialogueId
{
    Introduction = 0,
    Point = 1,
    Movement = 2,
    MergeResult = 3,
    SpawnUpgrade = 4,
    SpawnGauge = 5,
    Final = 6,
    HigherGradeSpawn = 7,
    HigherGradeSpawnUpgrade = 8,
    DisplayRoomUnlocked = 10,
    DisplayRoomFinal = 11,
    HigherGradeSpawnPool = 12,
    Gacha = 15,
    GachaResult = 16,
    CollectionAutoMerge = 17,
    CollectionTicketCollect = 18,
    Shop = 19,
    ShopTabs = 20,
    CollectionAutoMergeDone = 21,
    CollectionAutoMergeNoPair = 22,
    CollectionOfflineTicket = 23,
    ShopObjects = 24,
    ShopFinal = 25,
    AdBonus = 26,
    AdBonusPopup = 27,
}

[Serializable]
public struct DialogueLine
{
    [SerializeField] private string _speaker;
    [TextArea(2, 4)]
    [SerializeField] private string _message;

    public string Speaker => _speaker;
    public string Message => _message;

    public DialogueLine(string speaker, string message)
    {
        _speaker = speaker;
        _message = message;
    }
}

[Serializable]
public sealed class DialogueSequence
{
    [SerializeField] private DialogueId _id;
    [SerializeField] private DialogueLine[] _lines;

    public DialogueId Id => _id;
    public IReadOnlyList<DialogueLine> Lines => _lines;
}

[CreateAssetMenu(
    fileName = "TutorialContent",
    menuName = "Monster Kindergarten/Tutorial Content")]
public sealed class TutorialContent : ScriptableObject
{
    [Header("Guide Messages")]
    [SerializeField] private string _clickMessage;
    [SerializeField] private string _pointMessage;
    [SerializeField] private string _dragMessage;
    [SerializeField] private string _mergeMessage;
    [SerializeField] private string _systemUpgradeCarouselMessage;
    [SerializeField] private string _spawnPoolButtonMessage;
    [SerializeField] private string _gachaMakeRoomMessage;
    [SerializeField] private string _gachaButtonMessage;
    [SerializeField] private string _autoMergeButtonMessage;
    [SerializeField] private string _displayRoomButtonMessage;
    [SerializeField] private string _displayRoomSendButtonMessage;
    [SerializeField] private string _displayRoomSelectSlimeMessage;
    [SerializeField] private string _displayRoomEnterMessage;
    [SerializeField] private string _displayRoomInfoMessage;
    [SerializeField] private string _displayRoomInfoSummaryMessage;
    [SerializeField] private string _displayRoomObserveMessage;
    [SerializeField] private string _displayRoomTakeOutMessage;
    [SerializeField] private string _displayRoomCloseMessage;
    [SerializeField] private string _shopButtonMessage;
    [SerializeField] private string _shopObjectsTabMessage;
    [SerializeField] private string _adBonusButtonMessage;

    [Header("Dialogue")]
    [SerializeField] private DialogueSequence[] _dialogues;

    [Header("Background Theme")]
    [FormerlySerializedAs("_stageMenuButtonMessage")]
    [SerializeField] private string _backgroundMenuButtonMessage;
    [FormerlySerializedAs("_stageButtonMessage")]
    [SerializeField] private string _backgroundButtonMessage;

    public string ClickMessage => _clickMessage;
    public string PointMessage => _pointMessage;
    public string DragMessage => _dragMessage;
    public string MergeMessage => _mergeMessage;
    public string SystemUpgradeCarouselMessage => _systemUpgradeCarouselMessage;
    public string SpawnPoolButtonMessage => _spawnPoolButtonMessage;
    public string GachaMakeRoomMessage => _gachaMakeRoomMessage;
    public string GachaButtonMessage => _gachaButtonMessage;
    public string AutoMergeButtonMessage => _autoMergeButtonMessage;
    public string DisplayRoomButtonMessage => _displayRoomButtonMessage;
    public string DisplayRoomSendButtonMessage => _displayRoomSendButtonMessage;
    public string DisplayRoomSelectSlimeMessage => _displayRoomSelectSlimeMessage;
    public string DisplayRoomEnterMessage => _displayRoomEnterMessage;
    public string DisplayRoomInfoMessage => _displayRoomInfoMessage;
    public string DisplayRoomInfoSummaryMessage => _displayRoomInfoSummaryMessage;
    public string DisplayRoomObserveMessage => _displayRoomObserveMessage;
    public string DisplayRoomTakeOutMessage => _displayRoomTakeOutMessage;
    public string DisplayRoomCloseMessage => _displayRoomCloseMessage;
    public string ShopButtonMessage => _shopButtonMessage;
    public string ShopObjectsTabMessage => _shopObjectsTabMessage;
    public string AdBonusButtonMessage => _adBonusButtonMessage;
    public string BackgroundMenuButtonMessage => _backgroundMenuButtonMessage;
    public string BackgroundButtonMessage => _backgroundButtonMessage;

    public IReadOnlyList<DialogueLine> GetDialogue(DialogueId id)
    {
        if (_dialogues != null)
        {
            foreach (DialogueSequence dialogue in _dialogues)
            {
                if (dialogue != null && dialogue.Id == id)
                {
                    return dialogue.Lines ?? Array.Empty<DialogueLine>();
                }
            }
        }

        Debug.LogWarning($"대화 콘텐츠를 찾을 수 없습니다: {id}", this);
        return Array.Empty<DialogueLine>();
    }
}
