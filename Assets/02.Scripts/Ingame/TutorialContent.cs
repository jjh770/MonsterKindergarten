using System;
using System.Collections.Generic;
using UnityEngine;

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
    SkyIntro = 9,
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
    [SerializeField] private string _upgradeMessage;
    [SerializeField] private string _upgradePanelMessage;
    [SerializeField] private string _systemUpgradeCarouselMessage;
    [SerializeField] private string _spawnPoolButtonMessage;

    [Header("Dialogue")]
    [SerializeField] private DialogueSequence[] _dialogues;

    [Header("Stage")]
    [SerializeField] private string _stageButtonMessage;

    public string ClickMessage => _clickMessage;
    public string PointMessage => _pointMessage;
    public string DragMessage => _dragMessage;
    public string MergeMessage => _mergeMessage;
    public string UpgradeMessage => _upgradeMessage;
    public string UpgradePanelMessage => _upgradePanelMessage;
    public string SystemUpgradeCarouselMessage => _systemUpgradeCarouselMessage;
    public string SpawnPoolButtonMessage => _spawnPoolButtonMessage;
    public string StageButtonMessage => _stageButtonMessage;

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
