using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// 메인 엔딩 각 화면의 시각 연출을 소유한다. 화면 계층은 씬에 작성되어 있고,
// 이 컴포넌트는 그 참조를 들고 보이고 사라지는 움직임만 만든다.
// 언제 재생할지와 게임 입력을 잠글지는 MainEndingUI가 결정한다.
public sealed class MainEndingPresentationView : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("엔딩 전체의 안전 영역 루트입니다. 노치 여백은 코드가 맞춥니다.")]
    [SerializeField] private RectTransform _safeAreaRoot;
    [SerializeField] private CanvasGroup _presentationCanvasGroup;
    [Tooltip("배경 전체를 덮는 버튼입니다. 누르면 지금 단계를 건너뜁니다.")]
    [SerializeField] private UnityEngine.UI.Button _backgroundButton;

    [Header("Class")]
    [SerializeField] private GameObject _classGroup;
    [SerializeField] private CanvasGroup _classCanvasGroup;
    [SerializeField] private RectTransform _slimeGrid;
    [SerializeField] private TextMeshProUGUI _classTitle;
    [Tooltip("일반 슬라임 20종의 그림입니다. 등급 순서대로 연결합니다.")]
    [SerializeField] private UnityEngine.UI.Image[] _slimeImages =
        new UnityEngine.UI.Image[SlimeStatusSaveData.NormalCollectionSize];

    [Header("Credits")]
    [SerializeField] private GameObject _creditsGroup;
    [SerializeField] private CanvasGroup _creditsCanvasGroup;
    [SerializeField] private RectTransform _creditsContent;
    [SerializeField] private TextMeshProUGUI _creditsText;

    [Header("Teaser")]
    [SerializeField] private GameObject _teaserGroup;
    [SerializeField] private CanvasGroup _teaserCanvasGroup;

    [Header("Celebration")]
    [SerializeField] private GameObject _celebrationGroup;
    [SerializeField] private CanvasGroup _celebrationCanvasGroup;
    [SerializeField] private RectTransform _celebrationArtworkRect;
    [SerializeField] private UnityEngine.UI.Image _celebrationArtwork;
    [SerializeField] private TextMeshProUGUI _celebrationFallbackTitle;
    [SerializeField] private UnityEngine.UI.Button _endingContinueButton;

    private Sprite _celebrationImageSprite;
    private UnityAction _backgroundPressed;
    private UnityAction _continuePressed;

    // MainEndingUI가 Awake에서 한 번 부른다. 참조가 비어 있으면 false를 돌려주고
    // 엔딩 전체가 꺼진다. 일부만 비어 있는 채로 재생하면 중간에 멈춘다.
    public bool Initialize(
        Sprite celebrationImageSprite,
        UnityAction backgroundPressed,
        UnityAction continuePressed)
    {
        if (!HasRequiredReferences()) return false;

        _celebrationImageSprite = celebrationImageSprite;
        _backgroundPressed = backgroundPressed;
        _continuePressed = continuePressed;

        if (_backgroundPressed != null)
        {
            _backgroundButton.onClick.AddListener(_backgroundPressed);
        }

        if (_continuePressed != null)
        {
            _endingContinueButton.onClick.AddListener(_continuePressed);
        }

        gameObject.SetActive(false);
        return true;
    }

    public void Dispose()
    {
        if (_backgroundPressed != null && _backgroundButton != null)
        {
            _backgroundButton.onClick.RemoveListener(_backgroundPressed);
        }

        if (_continuePressed != null && _endingContinueButton != null)
        {
            _endingContinueButton.onClick.RemoveListener(_continuePressed);
        }
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public void RefreshSafeArea(RectTransform hostRoot)
    {
        if (hostRoot == null) return;

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(hostRoot);
        _safeAreaRoot.offsetMin = new Vector2(insets.Left, insets.Bottom);
        _safeAreaRoot.offsetMax = new Vector2(-insets.Right, -insets.Top);
    }

    public void RefreshSlimeSprites(SlimeManager manager)
    {
        if (manager == null) return;

        for (int i = 0; i < _slimeImages.Length; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)((int)ESlimeGrade.Grade1 + i);
            _slimeImages[i].sprite = manager.Get(grade)?.SpecData.Sprite;
        }
    }

    public void ResetVisuals()
    {
        _presentationCanvasGroup.alpha = 0f;
        _classGroup.SetActive(false);
        _creditsGroup.SetActive(false);
        _teaserGroup.SetActive(false);
        _celebrationGroup.SetActive(false);
    }

    public Tween BuildOpeningFade(float fadeDuration)
    {
        _presentationCanvasGroup.alpha = 0f;
        return _presentationCanvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.OutQuad);
    }

    public Tween BuildSlimeIntroduction(
        float fadeDuration,
        float introHoldDuration)
    {
        _classGroup.SetActive(true);
        _classCanvasGroup.alpha = 1f;
        _classTitle.text = "우리들의 스무 번째 친구가 도착했어요";
        _slimeGrid.localScale = Vector3.one;

        Sequence sequence = DOTween.Sequence();
        for (int i = 0; i < _slimeImages.Length; i++)
        {
            UnityEngine.UI.Image image = _slimeImages[i];
            image.color = new Color(1f, 1f, 1f, 0f);
            image.rectTransform.localScale = Vector3.one * 0.35f;
            float delay = i * 0.09f;
            sequence.Insert(delay, image.DOFade(1f, 0.25f));
            sequence.Insert(
                delay,
                image.rectTransform
                    .DOScale(1f, 0.38f)
                    .SetEase(Ease.OutBack));
        }

        sequence.AppendInterval(introHoldDuration);
        sequence.Append(_classCanvasGroup.DOFade(0f, fadeDuration));
        sequence.OnComplete(() => _classGroup.SetActive(false));
        return sequence;
    }

    public Tween BuildCredits(
        string creditsText,
        float scrollDuration,
        float fadeDuration)
    {
        _creditsText.text = creditsText;
        _creditsGroup.SetActive(true);
        _creditsCanvasGroup.alpha = 1f;

        float halfHeight = _safeAreaRoot.rect.height * 0.5f;
        _creditsContent.anchoredPosition = new Vector2(0f, -halfHeight - 1050f);
        Sequence sequence = DOTween.Sequence();
        sequence.Append(_creditsContent.DOAnchorPosY(
            halfHeight + 1050f,
            scrollDuration).SetEase(Ease.Linear));
        sequence.Append(_creditsCanvasGroup.DOFade(0f, fadeDuration));
        sequence.OnComplete(() => _creditsGroup.SetActive(false));
        return sequence;
    }

    public Tween BuildSpecialSlimeTeaser(
        float fadeDuration,
        float holdDuration)
    {
        _teaserGroup.SetActive(true);
        _teaserCanvasGroup.alpha = 0f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_teaserCanvasGroup.DOFade(1f, fadeDuration));
        sequence.AppendInterval(holdDuration);
        sequence.Append(_teaserCanvasGroup.DOFade(0f, fadeDuration));
        sequence.OnComplete(() => _teaserGroup.SetActive(false));
        return sequence;
    }

    public Tween BuildGraduationFinale(
        float fadeDuration,
        Action playUnlockSound)
    {
        _classGroup.SetActive(true);
        _classCanvasGroup.alpha = 0f;
        _classTitle.text = "몬스터 유치원 졸업!";
        _slimeGrid.localScale = Vector3.one * 0.9f;
        foreach (UnityEngine.UI.Image image in _slimeImages)
        {
            image.color = Color.white;
            image.rectTransform.localScale = Vector3.one;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_classCanvasGroup.DOFade(1f, fadeDuration));
        sequence.Join(_slimeGrid.DOScale(1f, 0.7f).SetEase(Ease.OutBack));
        sequence.AppendCallback(() => playUnlockSound?.Invoke());
        sequence.AppendInterval(1.8f);
        sequence.Append(_classCanvasGroup.DOFade(0f, fadeDuration));
        sequence.OnComplete(() => _classGroup.SetActive(false));
        return sequence;
    }

    public Tween BuildCelebrationReveal()
    {
        _classGroup.SetActive(false);
        _creditsGroup.SetActive(false);
        _teaserGroup.SetActive(false);
        _celebrationGroup.SetActive(true);
        _celebrationCanvasGroup.alpha = 0f;

        bool hasArtwork = _celebrationImageSprite != null;
        _celebrationArtwork.sprite = _celebrationImageSprite;
        _celebrationArtwork.color = hasArtwork
            ? Color.white
            : new Color(0.22f, 0.16f, 0.08f, 1f);
        _celebrationFallbackTitle.gameObject.SetActive(!hasArtwork);
        _endingContinueButton.gameObject.SetActive(false);
        _celebrationArtworkRect.localScale = Vector3.one;

        return _celebrationCanvasGroup
            .DOFade(1f, 0.8f)
            .SetEase(Ease.OutQuad);
    }

    public void ShowFinalScreen()
    {
        _endingContinueButton.gameObject.SetActive(true);
        _endingContinueButton.interactable = true;
    }

    public void SetContinueInteractable(bool interactable)
    {
        _endingContinueButton.interactable = interactable;
    }

    public Tween BuildCelebrationZoom()
    {
        return _celebrationArtworkRect
            .DOScale(1.04f, 6f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public Tween BuildClose(float fadeDuration)
    {
        return _presentationCanvasGroup.DOFade(0f, fadeDuration);
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _safeAreaRoot != null &&
                             _presentationCanvasGroup != null &&
                             _backgroundButton != null &&
                             _classGroup != null &&
                             _classCanvasGroup != null &&
                             _slimeGrid != null &&
                             _classTitle != null &&
                             _creditsGroup != null &&
                             _creditsCanvasGroup != null &&
                             _creditsContent != null &&
                             _creditsText != null &&
                             _teaserGroup != null &&
                             _teaserCanvasGroup != null &&
                             _celebrationGroup != null &&
                             _celebrationCanvasGroup != null &&
                             _celebrationArtworkRect != null &&
                             _celebrationArtwork != null &&
                             _celebrationFallbackTitle != null &&
                             _endingContinueButton != null &&
                             _slimeImages != null &&
                             _slimeImages.Length == SlimeStatusSaveData.NormalCollectionSize;

        for (int i = 0; hasReferences && i < _slimeImages.Length; i++)
        {
            hasReferences = _slimeImages[i] != null;
        }

        if (!hasReferences)
        {
            Debug.LogError("메인 엔딩 연출의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }
}
