using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TutorialManager))]
public abstract class TutorialSequenceBase : MonoBehaviour
{
    public abstract string TutorialId { get; }

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

    // === 시퀀스 공통 인프라 헬퍼 ===
    // 가이드 구독, 게임플레이 hold, 종료 정리, 시작 트리거를 시퀀스마다 들고 있던 것을
    // 여기로 모았다. 스폰과 자동 생산을 멈추지 않는 CollectionMilestoneGuideSequence는
    // 쓰지 않는다. 참조는 인자로만 받고 베이스의 [SerializeField]로 올리지 않는다.
    // 각 시퀀스가 자기 참조를 계속 소유한다.

    // --- 헬퍼 A: 가이드 구독 관리 ---
    // 구독 형태가 시퀀스마다 인라인/래핑으로 미묘하게 달랐으므로(가드 순서 차이),
    // AdvanceRequested가 실제로 붙거나 떨어지는 시점이 기존과 관찰 동등하도록
    // 플래그로 관리한다. 핸들러 본문은 각 시퀀스가 그대로 소유한다.
    private bool _isGuideSubscribed;
    private Action _guideAdvanceHandler;

    protected void SubscribeGuideAdvance(Action handler)
    {
        if (_isGuideSubscribed || Spotlight == null) return;
        _guideAdvanceHandler = handler;
        Spotlight.AdvanceRequested += handler;
        _isGuideSubscribed = true;
    }

    protected void UnsubscribeGuideAdvance()
    {
        if (!_isGuideSubscribed) return;
        if (Spotlight != null && _guideAdvanceHandler != null)
        {
            Spotlight.AdvanceRequested -= _guideAdvanceHandler;
        }
        _guideAdvanceHandler = null;
        _isGuideSubscribed = false;
    }

    // --- 헬퍼 B: 게임플레이 hold 획득/해제 ---
    // spawn/auto는 hold 필수, clicker는 일부 경로에서만 쓰이므로 nullable이다.
    // PushMode는 단계마다 다르게 쓰이므로 여기에 포함하지 않는다.
    protected void AcquireGameplayHold(SpawnManager spawn, AutoClicker auto)
    {
        spawn?.PushSpawnPause(this);
        auto?.PushPause(this);
    }

    protected void ReleaseGameplayHold(
        SpawnManager spawn,
        AutoClicker auto,
        Clicker clicker = null)
    {
        spawn?.ReleaseSpawnPause(this);
        auto?.ReleasePause(this);
        clicker?.ReleaseMode(this);
    }

    // --- 헬퍼 C: 종료 정리 공통 시퀀스 ---
    // hold 3종 해제 + CompleteTutorial() + RefreshInteraction()만 캡슐화한다.
    // MarkCompleted와 _step 전환은 각 시퀀스 고유 enum이라 여기 넣지 않고,
    // 각 시퀀스가 이 헬퍼 호출 전에 수행한다.
    protected void FinishGameplayTeardown(
        SpawnManager spawn,
        AutoClicker auto,
        Clicker clicker,
        GameplaySpaceManager space)
    {
        ReleaseGameplayHold(spawn, auto, clicker);
        CompleteTutorial();
        space?.RefreshInteraction();
    }

    // --- 헬퍼 D: 시작 트리거 구독 ---
    // 공통 3종 트리거만 묶는다. UnlockPopupUI.PresentationCompleted 핸들러는
    // 시퀀스마다 본문이 다르므로 각 시퀀스가 개별 배선한다.
    // 주의: TutorialManager.Finished는 TutorialManager 타입의 static event Action이다.
    // 베이스의 protected 프로퍼티 이름도 TutorialManager지만, 기존 파생 시퀀스들이
    // 동일한 컨텍스트에서 `TutorialManager.Finished += TryStart;`로 이미 사용하고
    // 있으므로(타입 static 멤버로 해석됨) 여기서도 동일하게 안전하게 컴파일된다.
    protected void SubscribeStandardStartTriggers(
        GameManager game,
        SpawnManager spawn,
        Action onTry)
    {
        TutorialManager.Finished += onTry;
        game.OnGameplayActivated += onTry;
        spawn.Initialized += onTry;
    }

    protected void UnsubscribeStandardStartTriggers(
        GameManager game,
        SpawnManager spawn,
        Action onTry)
    {
        TutorialManager.Finished -= onTry;
        if (game != null) game.OnGameplayActivated -= onTry;
        if (spawn != null) spawn.Initialized -= onTry;
    }

    // --- 헬퍼 E: 시작 게이트 공통 조건 ---
    // 공통 조건만 판정한다. 각 시퀀스는 자기 _step 비교·해금 조건을 이 결과와
    // AND로 결합해 최종 시작 여부를 판정한다.
    protected bool IsCommonStartGateOpen(
        SpawnManager spawn,
        GameplaySpaceManager space,
        UnlockPopupUI popup,
        string tutorialId)
    {
        return GameplayGate.IsActive
            && spawn != null && spawn.IsInitialized
            && TutorialProgress.CanStart(tutorialId)
            && space != null && space.IsMainFieldActive
            && !space.IsTransitioning
            && (popup == null || !popup.IsPresenting);
    }
}
