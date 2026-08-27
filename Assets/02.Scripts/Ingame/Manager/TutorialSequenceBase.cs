using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TutorialManager))]
public abstract class TutorialSequenceBase : MonoBehaviour
{
    protected TutorialManager TutorialManager { get; private set; }
    protected TutorialContent Content => TutorialManager.Content;
    protected DialoguePresentation Presentation => TutorialManager.Presentation;
    protected TutorialSpotlightView Spotlight => Presentation?.Spotlight;

    protected virtual void Awake()
    {
        TutorialManager = GetComponent<TutorialManager>();
        if (TutorialManager == null)
        {
            Debug.LogError("TutorialManager가 같은 오브젝트에 없습니다.", this);
            enabled = false;
        }
    }

    protected bool TryBeginTutorial()
    {
        return TutorialManager != null && TutorialManager.TryBegin(this);
    }

    protected void CompleteTutorial()
    {
        TutorialManager?.Complete(this);
    }

    protected void ShowDialogue(
        IReadOnlyList<DialogueLine> lines,
        Action onComplete,
        bool keepGuideVisible = false,
        DialoguePlacement placement = DialoguePlacement.Bottom)
    {
        Presentation.ShowDialogue(
            lines,
            onComplete,
            keepGuideVisible,
            placement);
    }
}
