using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 학자 슬라임을 눌렀을 때 HUD를 비우고 게임 정보를 고르는 전면 안내를 보여 준다.
// 실제 정보 계산은 기존 팝업과 공유하고, 이 컴포넌트는 표시와 입력 흐름만 소유한다.
public sealed class ScholarGuideUI : MonoBehaviour
{
    [Header("Owners")]
    [SerializeField] private HudVisibility _hudVisibility;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private GameplaySpaceManager _spaceManager;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private UpgradeManager _upgradeManager;

    [Header("Presentation")]
    [SerializeField] private RectTransform _root;
    [SerializeField] private Image _sourceImage;
    [SerializeField] private Image _scholarImage;
    [SerializeField] private RectTransform _scholarDestination;
    [SerializeField] private CanvasGroup _portraitBackgroundGroup;
    [SerializeField] private CanvasGroup _dialogueGroup;
    [SerializeField, Min(0f)] private float _moveDuration = 0.6f;
    [SerializeField, Min(1f)] private float _centerScale = 1.5f;

    [Header("Motion")]
    [Tooltip("학자가 제자리로 돌아가는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float _returnDuration = 0.5f;
    [Tooltip("날아가는 길이 휘는 정도입니다. 이동 거리에 대한 비율이고 0이면 일직선입니다.")]
    [SerializeField, Range(0f, 0.6f)] private float _arcBend = 0.22f;
    [Tooltip("날아오는 동안 기울어지는 각도(도)입니다. 도착하며 똑바로 섭니다.")]
    [SerializeField, Range(0f, 40f)] private float _flyTilt = 14f;
    [Tooltip("돌아갈 때 흔들리는 각도(도)의 최댓값입니다.")]
    [SerializeField, Range(0f, 40f)] private float _returnTilt = 10f;
    [Tooltip("도착했을 때 통통 튀는 정도입니다. 0이면 튀지 않습니다.")]
    [SerializeField, Range(0f, 0.4f)] private float _landingPunch = 0.1f;

    [Header("Panels")]
    [Tooltip("메뉴와 상세가 바뀔 때 쓰는 그룹입니다. 비워 두면 연출 없이 바로 바뀝니다.")]
    [SerializeField] private CanvasGroup _menuGroup;
    [SerializeField] private CanvasGroup _detailGroup;

    [Header("Menu")]
    [SerializeField] private GameObject _menuRoot;
    [SerializeField] private Button _probabilityButton;
    [SerializeField] private Button _upgradeStatusButton;
    [SerializeField] private Button _gachaProbabilityButton;
    [SerializeField] private Button _closeButton;

    [Header("Detail")]
    [SerializeField] private GameObject _detailRoot;
    [SerializeField] private RectTransform _detailPanel;
    [SerializeField] private TextMeshProUGUI _detailTitle;
    [SerializeField] private TextMeshProUGUI _detailText;
    [SerializeField] private Button _backButton;

    private Tween _scholarTween;
    private Tween _landingTween;
    private Tween _dialogueTween;
    private Tween _swapTween;
    private bool _isSwapping;
    private Vector3 _dialogueBaseScale = Vector3.one;
    private Vector3 _menuBaseScale = Vector3.one;
    private Vector3 _detailBaseScale = Vector3.one;
    private readonly System.Collections.Generic.Dictionary<Transform, Vector3> _buttonBaseScales = new();
    private Vector2 _sourcePosition;
    private Vector2 _sourceSize;
    private bool _isOpen;
    private bool _isTransitioning;
    private bool _presentationRestored;

    public static bool IsAnyOpen { get; private set; }
    public bool IsOpen => _isOpen;
    public RectTransform ProbabilityButtonTarget =>
        _probabilityButton != null ? _probabilityButton.transform as RectTransform : null;
    public RectTransform DetailTarget => _detailPanel;

    public event Action MenuOpened;
    public event Action ProbabilityOpened;
    public event Action Closed;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("학자 안내 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _probabilityButton.onClick.AddListener(ShowProbability);
        _upgradeStatusButton.onClick.AddListener(ShowUpgradeStatus);
        _gachaProbabilityButton.onClick.AddListener(ShowGachaProbability);
        _closeButton.onClick.AddListener(Close);
        _backButton.onClick.AddListener(ShowMenu);
        CacheBaseScales();
        _root.gameObject.SetActive(false);
    }

    // 연출이 크기를 줄였다 키우므로, 돌아올 기준 크기는 연출 전에 읽어 둔다.
    private void CacheBaseScales()
    {
        _dialogueBaseScale = _dialogueGroup.transform.localScale;
        _menuBaseScale = _menuRoot.transform.localScale;
        _detailBaseScale = _detailRoot.transform.localScale;
        foreach (Button button in MenuButtons())
        {
            _buttonBaseScales[button.transform] = button.transform.localScale;
        }
    }

    private Button[] MenuButtons()
    {
        return new[] { _probabilityButton, _upgradeStatusButton, _gachaProbabilityButton, _closeButton };
    }

    private bool HasRequiredReferences()
    {
        return _root != null && _sourceImage != null && _scholarImage != null &&
            _scholarDestination != null && _portraitBackgroundGroup != null &&
            _dialogueGroup != null && _menuRoot != null &&
            _probabilityButton != null && _upgradeStatusButton != null &&
            _gachaProbabilityButton != null &&
            _closeButton != null && _detailRoot != null &&
            _detailPanel != null && _detailTitle != null &&
            _detailText != null && _backButton != null &&
            _hudVisibility != null && _clicker != null &&
            _upgradeUI != null && _gameExitManager != null &&
            _spaceManager != null && _spawnManager != null &&
            _slimeManager != null && _upgradeManager != null;
    }

    private void OnDestroy()
    {
        _probabilityButton?.onClick.RemoveListener(ShowProbability);
        _upgradeStatusButton?.onClick.RemoveListener(ShowUpgradeStatus);
        _gachaProbabilityButton?.onClick.RemoveListener(ShowGachaProbability);
        _closeButton?.onClick.RemoveListener(Close);
        _backButton?.onClick.RemoveListener(ShowMenu);
        Cleanup(animated: false, notify: false);
    }

    public void Open()
    {
        if (!enabled || _isOpen || _isTransitioning || !GameplayGate.IsActive) return;
        if (_spaceManager.IsTransitioning) return;

        _isOpen = true;
        IsAnyOpen = true;
        _isTransitioning = true;
        _presentationRestored = false;
        _upgradeUI.PushStandDown(this);
        _hudVisibility.PushHide(this, EHudParts.All);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        _gameExitManager.RegisterBackHandler(this, TryHandleBack);

        PrepareScholarImage();
        _root.gameObject.SetActive(true);
        _root.SetAsLastSibling();
        RefreshMenuAvailability();
        _menuRoot.SetActive(true);
        _detailRoot.SetActive(false);
        _portraitBackgroundGroup.alpha = 1f;
        _dialogueGroup.alpha = 0f;
        _dialogueGroup.interactable = false;
        _dialogueGroup.blocksRaycasts = false;
        ResetPanels();
        _dialogueGroup.transform.localScale = _dialogueBaseScale * 0.85f;

        Vector2 destination = GetLocalPosition(_scholarDestination);
        _scholarTween?.Kill();
        _scholarTween = FlyScholar(
            _sourcePosition,
            destination,
            _centerScale,
            _moveDuration,
            Ease.OutCubic,
            Ease.OutBack,
            _flyTilt,
            wobble: false,
            () =>
            {
                _isTransitioning = false;
                _dialogueGroup.interactable = true;
                _dialogueGroup.blocksRaycasts = true;
                PlayLanding();
                PlayDialogueIn();
                PlayMenuEntrance();
                MenuOpened?.Invoke();
            });
    }

    // 곡선을 그리며 날아가고, 도착할 때까지 기울어졌다가 똑바로 선다. 직선으로 같은 곡선에 위치와 크기가
    // 함께 붙으면 기계적으로 보여서, 위치는 휘는 길을 따르고 크기는 따로 튀게 한다.
    private Sequence FlyScholar(
        Vector2 from,
        Vector2 to,
        float targetScale,
        float duration,
        Ease positionEase,
        Ease scaleEase,
        float tilt,
        bool wobble,
        TweenCallback onComplete)
    {
        RectTransform rect = _scholarImage.rectTransform;
        Vector2 delta = to - from;
        // 길은 위쪽으로 휘게 한다. 아래로 휘면 땅으로 파고드는 것처럼 보인다.
        Vector2 side = new Vector2(-delta.y, delta.x).normalized;
        if (side.y < 0f) side = -side;
        Vector2 control = (from + to) * 0.5f + side * (delta.magnitude * _arcBend);
        // 가는 쪽으로 몸을 기울인다. 돌아올 때는 흔들렸다 선다.
        float lean = -Mathf.Sign(delta.x) * tilt;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(DOVirtual.Float(0f, 1f, duration, t =>
        {
            float u = 1f - t;
            rect.anchoredPosition = u * u * from + 2f * u * t * control + t * t * to;
            float angle = wobble ? lean * Mathf.Sin(Mathf.PI * t) : lean * (1f - t);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }).SetEase(positionEase));
        sequence.Join(rect.DOScale(targetScale, duration).SetEase(scaleEase, 1.2f));
        sequence.OnComplete(onComplete);
        return sequence;
    }

    // 도착한 학자가 통통 튄다. 대화창이 나타나는 것과 같은 순간에 시작한다.
    private void PlayLanding()
    {
        _landingTween?.Kill();
        if (_landingPunch <= 0f) return;

        _landingTween = _scholarImage.rectTransform
            .DOPunchScale(Vector3.one * _landingPunch, 0.35f, 6, 0.6f);
    }

    private void PlayDialogueIn()
    {
        _dialogueTween?.Kill();
        _dialogueTween = DOTween.Sequence()
            .Join(_dialogueGroup.DOFade(1f, 0.2f))
            .Join(_dialogueGroup.transform
                .DOScale(_dialogueBaseScale, 0.35f)
                .SetEase(Ease.OutBack));
    }

    // 메뉴 버튼이 위에서부터 차례로 튀어나온다.
    private void PlayMenuEntrance()
    {
        int order = 0;
        foreach (Button button in MenuButtons())
        {
            if (!button.gameObject.activeSelf) continue;

            Transform target = button.transform;
            Vector3 baseScale = _buttonBaseScales.TryGetValue(target, out Vector3 stored) ? stored : Vector3.one;
            target.DOKill();
            target.localScale = baseScale * 0.5f;
            target.DOScale(baseScale, 0.35f)
                .SetEase(Ease.OutBack)
                .SetDelay(order * 0.06f);
            order++;
        }
    }

    private void ResetPanels()
    {
        // 씬이 내려가는 중에는 화면 오브젝트가 이미 파괴됐을 수 있다.
        if (_menuRoot == null || _detailRoot == null) return;

        _swapTween?.Kill();
        _swapTween = null;
        _isSwapping = false;
        _menuRoot.transform.localScale = _menuBaseScale;
        _detailRoot.transform.localScale = _detailBaseScale;
        ResetGroup(_menuGroup);
        ResetGroup(_detailGroup);
        foreach (Button button in MenuButtons())
        {
            if (button == null) continue;

            button.transform.DOKill();
            if (_buttonBaseScales.TryGetValue(button.transform, out Vector3 baseScale))
            {
                button.transform.localScale = baseScale;
            }
        }
    }

    private static void ResetGroup(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = 1f;
        group.interactable = true;
    }

    public void Close()
    {
        if (!_isOpen || _isTransitioning) return;

        _isTransitioning = true;
        _dialogueGroup.interactable = false;
        _dialogueGroup.blocksRaycasts = false;
        RestorePresentation(animated: true);
        _swapTween?.Kill();
        _isSwapping = false;
        _landingTween?.Kill();
        _scholarImage.rectTransform.localScale = Vector3.one * _centerScale;

        // 대화창은 살짝 움츠러들며 사라지고, 학자는 그동안 곡선을 그리며 제자리로 돌아간다.
        _dialogueTween?.Kill();
        _dialogueTween = DOTween.Sequence()
            .Join(_dialogueGroup.DOFade(0f, 0.2f))
            .Join(_dialogueGroup.transform
                .DOScale(_dialogueBaseScale * 0.88f, 0.22f)
                .SetEase(Ease.InBack))
            .Join(_portraitBackgroundGroup.DOFade(0f, 0.3f));
        _scholarTween?.Kill();
        _scholarTween = FlyScholar(
            GetLocalPositionOfScholar(),
            _sourcePosition,
            1f,
            _returnDuration,
            Ease.InOutCubic,
            Ease.InOutBack,
            _returnTilt,
            wobble: true,
            () => Cleanup(animated: true, notify: true));
    }

    private Vector2 GetLocalPositionOfScholar()
    {
        return _scholarImage.rectTransform.anchoredPosition;
    }

    public void ShowProbability()
    {
        if (!_isOpen) return;

        bool isUnlocked = _slimeManager.IsHigherGradeSpawnUnlocked;
        _detailTitle.text = "자연 등장 슬라임 확률";
        _detailText.text = GameplayInfoTextBuilder.BuildSpawnProbabilityText(
            _spawnManager.GetCurrentSpawnProbabilities(),
            isUnlocked ? _spawnManager.GetSpawnWeightUpgradeLevel() : -1,
            includeTitle: false);
        ShowDetail();
        ProbabilityOpened?.Invoke();
    }

    public void ShowUpgradeStatus()
    {
        if (!_isOpen) return;

        _detailTitle.text = "시스템 업그레이드 현황";
        _detailText.text = GameplayInfoTextBuilder.BuildSystemUpgradeText(
            _upgradeManager,
            _slimeManager,
            _spawnManager,
            includeTitle: false);
        ShowDetail();
    }

    public void ShowGachaProbability()
    {
        if (!_isOpen || !_slimeManager.IsGachaUnlocked) return;

        _detailTitle.text = "슬라임 뽑기 확률";
        _detailText.text = GameplayInfoTextBuilder.BuildNormalGachaProbabilityText(
            _slimeManager.HighestGrade,
            includeTitle: false);
        ShowDetail();
    }

    public void ShowMenu()
    {
        if (!_isOpen || _isTransitioning || _isSwapping) return;

        RefreshMenuAvailability();
        SwapPanels(_detailRoot, _detailGroup, _detailBaseScale, _menuRoot, _menuGroup, _menuBaseScale, menuEntrance: true);
    }

    private void RefreshMenuAvailability()
    {
        _gachaProbabilityButton.gameObject.SetActive(_slimeManager.IsGachaUnlocked);
    }

    private void ShowDetail()
    {
        if (_isSwapping) return;

        SwapPanels(_menuRoot, _menuGroup, _menuBaseScale, _detailRoot, _detailGroup, _detailBaseScale, menuEntrance: false);
        _detailPanel.SetAsLastSibling();
    }

    // 나가는 쪽이 살짝 줄며 빠르게 사라지고, 들어오는 쪽이 작은 크기에서 통통 튀며 나타난다.
    // 연출 중에는 두 쪽 모두 입력을 받지 않아 버튼을 연달아 눌러도 꼬이지 않는다.
    private void SwapPanels(
        GameObject from,
        CanvasGroup fromGroup,
        Vector3 fromScale,
        GameObject to,
        CanvasGroup toGroup,
        Vector3 toScale,
        bool menuEntrance)
    {
        _swapTween?.Kill();
        if (fromGroup == null || toGroup == null)
        {
            from.SetActive(false);
            to.SetActive(true);
            if (menuEntrance) PlayMenuEntrance();
            return;
        }

        _isSwapping = true;
        fromGroup.interactable = false;
        Transform fromTransform = from.transform;
        Transform toTransform = to.transform;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(fromGroup.DOFade(0f, 0.12f).SetEase(Ease.InQuad));
        sequence.Join(fromTransform.DOScale(fromScale * 0.92f, 0.12f).SetEase(Ease.InQuad));
        sequence.AppendCallback(() =>
        {
            from.SetActive(false);
            fromGroup.alpha = 1f;
            fromGroup.interactable = true;
            fromTransform.localScale = fromScale;

            to.SetActive(true);
            toGroup.alpha = 0f;
            toGroup.interactable = false;
            toTransform.localScale = toScale * 0.85f;
            if (menuEntrance) PlayMenuEntrance();
        });
        sequence.Append(toGroup.DOFade(1f, 0.15f));
        sequence.Join(toTransform.DOScale(toScale, 0.3f).SetEase(Ease.OutBack));
        sequence.OnComplete(() =>
        {
            toGroup.interactable = true;
            _isSwapping = false;
            _swapTween = null;
        });
        _swapTween = sequence;
    }

    private bool TryHandleBack()
    {
        if (!_isOpen) return false;
        // 패널이 바뀌는 중에는 뒤로가기를 소비만 하고 아무것도 하지 않는다.
        if (_isSwapping) return true;

        if (_detailRoot.activeSelf)
        {
            ShowMenu();
        }
        else
        {
            Close();
        }

        return true;
    }

    private void PrepareScholarImage()
    {
        _scholarImage.rectTransform.localRotation = Quaternion.identity;
        _scholarImage.sprite = _sourceImage.sprite;
        _scholarImage.overrideSprite = _sourceImage.overrideSprite;
        _scholarImage.color = _sourceImage.color;
        _scholarImage.material = _sourceImage.material;
        _scholarImage.preserveAspect = _sourceImage.preserveAspect;

        RectTransform sourceRect = _sourceImage.rectTransform;
        _sourcePosition = GetLocalPosition(sourceRect);
        _sourceSize = sourceRect.rect.size;
        _scholarImage.rectTransform.anchoredPosition = _sourcePosition;
        _scholarImage.rectTransform.sizeDelta = _sourceSize;
        _scholarImage.rectTransform.localScale = Vector3.one;
        _sourceImage.enabled = false;
    }

    private Vector2 GetLocalPosition(RectTransform target)
    {
        return _root.InverseTransformPoint(target.position);
    }

    private void Cleanup(bool animated, bool notify)
    {
        _scholarTween?.Kill();
        _scholarTween = null;
        _landingTween?.Kill();
        _landingTween = null;
        _dialogueTween?.Kill();
        _dialogueTween = null;
        ResetPanels();

        if (!_isOpen && !_isTransitioning) return;

        _isOpen = false;
        _isTransitioning = false;
        IsAnyOpen = false;
        if (_sourceImage != null) _sourceImage.enabled = true;
        if (_root != null) _root.gameObject.SetActive(false);
        _gameExitManager.UnregisterBackHandler(this);
        _clicker.ReleaseMode(this);
        RestorePresentation(animated);

        if (notify) Closed?.Invoke();
    }

    private void RestorePresentation(bool animated)
    {
        if (_presentationRestored) return;

        _presentationRestored = true;
        _hudVisibility.Release(this, animated);
        _upgradeUI.ReleaseStandDown(this, animated);
    }
}
