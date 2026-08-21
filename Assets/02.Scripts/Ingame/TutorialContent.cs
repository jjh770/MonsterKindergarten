using System;
using UnityEngine;

[Serializable]
public struct TutorialDialogueLine
{
    [SerializeField] private string _speaker;
    [TextArea(2, 4)]
    [SerializeField] private string _message;

    public string Speaker => _speaker;
    public string Message => _message;

    public TutorialDialogueLine(string speaker, string message)
    {
        _speaker = speaker;
        _message = message;
    }
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

    [Header("Dialogue")]
    [SerializeField] private TutorialDialogueLine[] _introductionDialogue;
    [SerializeField] private TutorialDialogueLine[] _pointDialogue;
    [SerializeField] private TutorialDialogueLine[] _movementDialogue;
    [SerializeField] private TutorialDialogueLine[] _mergeResultDialogue;
    [SerializeField] private TutorialDialogueLine[] _spawnUpgradeDialogue;
    [SerializeField] private TutorialDialogueLine[] _spawnGaugeDialogue;
    [SerializeField] private TutorialDialogueLine[] _finalDialogue;

    [Header("Stage")]
    [SerializeField] private string _stageButtonMessage;
    [SerializeField] private TutorialDialogueLine[] _skyIntroDialogue;

    public string ClickMessage => _clickMessage;
    public string PointMessage => _pointMessage;
    public string DragMessage => _dragMessage;
    public string MergeMessage => _mergeMessage;
    public string UpgradeMessage => _upgradeMessage;
    public string UpgradePanelMessage => _upgradePanelMessage;
    public TutorialDialogueLine[] IntroductionDialogue => _introductionDialogue;
    public TutorialDialogueLine[] PointDialogue => _pointDialogue;
    public TutorialDialogueLine[] MovementDialogue => _movementDialogue;
    public TutorialDialogueLine[] MergeResultDialogue => _mergeResultDialogue;
    public TutorialDialogueLine[] SpawnUpgradeDialogue => _spawnUpgradeDialogue;
    public TutorialDialogueLine[] SpawnGaugeDialogue => _spawnGaugeDialogue;
    public TutorialDialogueLine[] FinalDialogue => _finalDialogue;
    public string StageButtonMessage => _stageButtonMessage;
    public TutorialDialogueLine[] SkyIntroDialogue => _skyIntroDialogue;
}
