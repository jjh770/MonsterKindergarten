using UnityEngine;

// 특별한 슬라임을 처음 장식장에 넣은 뒤 장식장에 들어가면 특별 도감을 알려 준다.
// 기획서 §9.4.
//
// 흐름은 대화 -> 도감 버튼 누르기 -> 도감 제목 누르기 -> 대화다. 제목을 누르면 도감이
// 특별 도감으로 넘어가는 것은 CollectionBookUI가 맡고, 이 시퀀스는 그 끝을 알리는
// 이벤트를 보고 다음으로 넘어간다.
//
// 다른 안내의 순서에 묶이지 않는다. 특별한 슬라임을 아직 못 만난 사람에게 이 안내는
// 시작조차 하지 않는데, 순서에 넣으면 그 사람의 뒤 순서 안내가 전부 막힌다.
//
// 완료 표시는 끝에서만 남긴다. 도중에 앱이 끝나면 다음에 장식장에 들어갈 때 처음부터
// 다시 나온다. 도중에 장식장을 나가면 그 자리에서 접고 표시 없이 물러난다.
public sealed class SpecialCollectionTutorialSequence : TutorialSequenceBase
{
    private enum Step
    {
        None,
        Dialogue,
        OpenBook,
        Title,
        Final,
    }

    [SerializeField] private CollectionBookUI _book;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private AutoClicker _autoClicker;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameplaySpaceManager _gameplaySpaceManager;
    [SerializeField] private UnlockPopupUI _unlockPopupUI;
    [SerializeField] private GachaResultDirector _gachaResultDirector;

    private Step _step;

    public override string TutorialId => TutorialIds.SpecialCollection;

    protected override void Awake()
    {
        base.Awake();
        if (!enabled) return;

        if (_book == null || _slimeManager == null || _spawnManager == null ||
            _autoClicker == null || _clicker == null || _gameplaySpaceManager == null)
        {
            Debug.LogError("특별 도감 안내의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _book.Opened += OnBookOpened;
        _book.Closed += OnBookClosed;
        _book.CategoryChanged += OnCategoryChanged;
        _gameplaySpaceManager.SpaceChanged += OnSpaceChanged;
    }

    private void OnDestroy()
    {
        if (_book != null)
        {
            _book.Opened -= OnBookOpened;
            _book.Closed -= OnBookClosed;
            _book.CategoryChanged -= OnCategoryChanged;
        }

        if (_gameplaySpaceManager != null)
        {
            _gameplaySpaceManager.SpaceChanged -= OnSpaceChanged;
        }

        if (_step != Step.None)
        {
            ReleaseGameplayHold(_spawnManager, _autoClicker, _clicker);
        }
    }

    private void Update()
    {
        if (_step != Step.None || !TutorialProgress.IsInitialized) return;

        // 한 번 본 안내는 다시 나오지 않는다. 매 프레임 확인할 이유도 없다.
        if (TutorialProgress.IsCompleted(TutorialIds.SpecialCollection))
        {
            enabled = false;
            return;
        }

        if (_slimeManager.SpecialCollectionCount <= 0 || !CanPresent()) return;

        Begin();
    }

    // 장식장 안에서 도감 버튼이 보이고, 다른 전면 연출이 없는 상태인가. 장식장 안내가
    // 끝나기 전에는 도감 버튼 자체가 없다.
    private bool CanPresent()
    {
        if (!GameplayGate.IsReady ||
            !TutorialProgress.IsCompleted(TutorialIds.DisplayRoom) ||
            TutorialManager.IsRunning ||
            _gameplaySpaceManager.IsMainFieldActive ||
            _gameplaySpaceManager.IsTransitioning ||
            _book.IsOpen ||
            (_unlockPopupUI != null && _unlockPopupUI.IsPresenting) ||
            (_gachaResultDirector != null && _gachaResultDirector.IsPlaying))
        {
            return false;
        }

        RectTransform target = _book.OpenButtonTarget;
        return target != null && target.gameObject.activeInHierarchy;
    }

    private void Begin()
    {
        if (!TryBeginTutorial()) return;

        _step = Step.Dialogue;
        AcquireGameplayHold(_spawnManager, _autoClicker);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Tutorial);
        Spotlight.Hide();
        ShowDialogue(Content.GetDialogue(DialogueId.SpecialCollection), ShowOpenStep);
    }

    private void ShowOpenStep()
    {
        RectTransform target = _book.OpenButtonTarget;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Abort();
            return;
        }

        _step = Step.OpenBook;
        Spotlight.ShowUiTarget(
            Content.SpecialCollectionOpenMessage,
            target,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void ShowTitleStep()
    {
        _step = Step.Title;
        Spotlight.ShowUiTarget(
            Content.SpecialCollectionTitleMessage,
            _book.TitleTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void OnBookOpened()
    {
        if (_step != Step.OpenBook) return;

        ShowTitleStep();
    }

    // 제목을 누르기 전에 도감이 닫히면 가리킬 대상이 사라진다. 도감 버튼부터 다시 안내한다.
    private void OnBookClosed()
    {
        if (_step != Step.Title) return;

        ShowOpenStep();
    }

    private void OnCategoryChanged()
    {
        if (_step != Step.Title || !_book.IsSpecialCategory) return;

        _step = Step.Final;
        Spotlight.Hide();
        ShowDialogue(Content.GetDialogue(DialogueId.SpecialCollectionFinal), Complete);
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        if (_step == Step.None || space == EGameplaySpace.DisplayRoom) return;

        Abort();
    }

    private void Complete()
    {
        if (_step == Step.None) return;

        TutorialProgress.MarkCompleted(TutorialIds.SpecialCollection);
        Finish();
    }

    private void Abort()
    {
        if (_step == Step.None) return;

        Presentation?.CancelDialogue();

        Finish();
    }

    private void Finish()
    {
        _step = Step.None;
        FinishGameplayTeardown(_spawnManager, _autoClicker, _clicker, _gameplaySpaceManager);
    }
}
