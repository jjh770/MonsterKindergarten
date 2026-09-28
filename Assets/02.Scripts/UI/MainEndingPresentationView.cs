using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// 메인 엔딩의 런타임 uGUI 계층과 각 화면의 시각 연출을 소유한다.
// 언제 재생할지와 게임 입력을 잠글지는 MainEndingUI가 결정한다.
public sealed class MainEndingPresentationView
{
    private readonly Sprite _celebrationImageSprite;
    private readonly UnityAction _backgroundPressed;
    private readonly UnityAction _continuePressed;

    private RectTransform _safeAreaRoot;
    private CanvasGroup _presentationCanvasGroup;
    private UnityEngine.UI.Button _backgroundButton;

    private GameObject _classGroup;
    private CanvasGroup _classCanvasGroup;
    private RectTransform _slimeGrid;
    private TextMeshProUGUI _classTitle;
    private readonly UnityEngine.UI.Image[] _slimeImages =
        new UnityEngine.UI.Image[SlimeStatusSaveData.NormalCollectionSize];

    private GameObject _creditsGroup;
    private CanvasGroup _creditsCanvasGroup;
    private RectTransform _creditsContent;
    private TextMeshProUGUI _creditsText;

    private GameObject _teaserGroup;
    private CanvasGroup _teaserCanvasGroup;

    private GameObject _celebrationGroup;
    private CanvasGroup _celebrationCanvasGroup;
    private RectTransform _celebrationArtworkRect;
    private UnityEngine.UI.Image _celebrationArtwork;
    private TextMeshProUGUI _celebrationFallbackTitle;
    private UnityEngine.UI.Button _endingContinueButton;

    public GameObject Root { get; }

    public MainEndingPresentationView(
        Transform host,
        GameObject fontSource,
        UnityEngine.UI.Button buttonStyle,
        Sprite celebrationImageSprite,
        UnityAction backgroundPressed,
        UnityAction continuePressed)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        if (fontSource == null) throw new ArgumentNullException(nameof(fontSource));
        if (buttonStyle == null) throw new ArgumentNullException(nameof(buttonStyle));

        _celebrationImageSprite = celebrationImageSprite;
        _backgroundPressed = backgroundPressed;
        _continuePressed = continuePressed;

        TMP_FontAsset font = fontSource
            .GetComponentInChildren<TextMeshProUGUI>(true)?.font;

        Root = CreateRectObject("EndingPresentation", host);
        Stretch(Root.GetComponent<RectTransform>());
        _presentationCanvasGroup = Root.AddComponent<CanvasGroup>();

        UnityEngine.UI.Image background =
            Root.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color(0.035f, 0.025f, 0.02f, 0.97f);
        background.raycastTarget = true;
        _backgroundButton = Root.AddComponent<UnityEngine.UI.Button>();
        _backgroundButton.targetGraphic = background;
        _backgroundButton.transition = UnityEngine.UI.Selectable.Transition.None;
        _backgroundButton.onClick.AddListener(_backgroundPressed);

        GameObject safeObject = CreateRectObject("SafeAreaRoot", Root.transform);
        _safeAreaRoot = safeObject.GetComponent<RectTransform>();
        Stretch(_safeAreaRoot);

        BuildClassGroup(font);
        BuildCreditsGroup(font);
        BuildTeaserGroup(font);
        BuildCelebrationGroup(font, buttonStyle);
        Root.SetActive(false);
    }

    public void Dispose()
    {
        if (_backgroundPressed != null)
        {
            _backgroundButton.onClick.RemoveListener(_backgroundPressed);
        }

        if (_continuePressed != null)
        {
            _endingContinueButton.onClick.RemoveListener(_continuePressed);
        }
    }

    public void SetActive(bool active)
    {
        Root.SetActive(active);
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

    private void BuildClassGroup(TMP_FontAsset font)
    {
        _classGroup = CreateRectObject("ClassGroup", _safeAreaRoot);
        Stretch(_classGroup.GetComponent<RectTransform>());
        _classCanvasGroup = _classGroup.AddComponent<CanvasGroup>();

        _classTitle = CreateText(
            "GraduationTitle",
            _classGroup.transform,
            font,
            64f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.88f, 0.45f, 1f));
        RectTransform titleRect = _classTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0.08f, 0.78f);
        titleRect.anchorMax = new Vector2(0.92f, 0.96f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        GameObject gridObject = CreateRectObject(
            "SlimeClassGrid",
            _classGroup.transform);
        _slimeGrid = gridObject.GetComponent<RectTransform>();
        _slimeGrid.anchorMin = new Vector2(0.5f, 0.44f);
        _slimeGrid.anchorMax = new Vector2(0.5f, 0.44f);
        _slimeGrid.pivot = new Vector2(0.5f, 0.5f);
        _slimeGrid.sizeDelta = new Vector2(900f, 760f);

        UnityEngine.UI.GridLayoutGroup layout =
            gridObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        layout.cellSize = new Vector2(150f, 150f);
        layout.spacing = new Vector2(30f, 30f);
        layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        layout.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < _slimeImages.Length; i++)
        {
            GameObject imageObject = CreateRectObject(
                $"Slime{i + 1:00}",
                _slimeGrid);
            UnityEngine.UI.Image image =
                imageObject.AddComponent<UnityEngine.UI.Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            _slimeImages[i] = image;
        }
    }

    private void BuildCreditsGroup(TMP_FontAsset font)
    {
        _creditsGroup = CreateRectObject("CreditsGroup", _safeAreaRoot);
        Stretch(_creditsGroup.GetComponent<RectTransform>());
        _creditsCanvasGroup = _creditsGroup.AddComponent<CanvasGroup>();

        GameObject content = CreateRectObject(
            "CreditsContent",
            _creditsGroup.transform);
        _creditsContent = content.GetComponent<RectTransform>();
        _creditsContent.anchorMin = new Vector2(0.08f, 0.5f);
        _creditsContent.anchorMax = new Vector2(0.92f, 0.5f);
        _creditsContent.sizeDelta = new Vector2(0f, 2100f);

        _creditsText = CreateText(
            "CreditsText",
            content.transform,
            font,
            46f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.93f, 0.75f, 1f));
        Stretch(_creditsText.rectTransform);
        _creditsText.lineSpacing = 12f;
    }

    private void BuildTeaserGroup(TMP_FontAsset font)
    {
        _teaserGroup = CreateRectObject("SpecialSlimeTeaser", _safeAreaRoot);
        Stretch(_teaserGroup.GetComponent<RectTransform>());
        _teaserCanvasGroup = _teaserGroup.AddComponent<CanvasGroup>();

        TextMeshProUGUI teaser = CreateText(
            "TeaserText",
            _teaserGroup.transform,
            font,
            50f,
            TextAlignmentOptions.Center,
            new Color(0.82f, 0.92f, 1f, 1f));
        teaser.text = "그런데 말이에요...\n\n" +
                      "가챠권 속에는\n평범한 슬라임과는 조금 다른\n\n" +
                      "특별한 빛을 가진 슬라임이\n숨어 있다는데...?";
        teaser.rectTransform.anchorMin = new Vector2(0.08f, 0.2f);
        teaser.rectTransform.anchorMax = new Vector2(0.92f, 0.8f);
        teaser.rectTransform.offsetMin = Vector2.zero;
        teaser.rectTransform.offsetMax = Vector2.zero;
    }

    private void BuildCelebrationGroup(
        TMP_FontAsset font,
        UnityEngine.UI.Button buttonStyle)
    {
        _celebrationGroup = CreateRectObject("CelebrationGroup", Root.transform);
        Stretch(_celebrationGroup.GetComponent<RectTransform>());
        _celebrationCanvasGroup = _celebrationGroup.AddComponent<CanvasGroup>();

        GameObject artworkObject = CreateRectObject(
            "CelebrationArtwork",
            _celebrationGroup.transform);
        _celebrationArtworkRect = artworkObject.GetComponent<RectTransform>();
        _celebrationArtworkRect.anchorMin = new Vector2(0.5f, 0.5f);
        _celebrationArtworkRect.anchorMax = new Vector2(0.5f, 0.5f);
        _celebrationArtworkRect.sizeDelta = new Vector2(1080f, 1920f);
        _celebrationArtwork = artworkObject.AddComponent<UnityEngine.UI.Image>();
        _celebrationArtwork.preserveAspect = true;
        _celebrationArtwork.raycastTarget = false;
        UnityEngine.UI.AspectRatioFitter aspect =
            artworkObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 9f / 16f;

        _celebrationFallbackTitle = CreateText(
            "FallbackTitle",
            _celebrationGroup.transform,
            font,
            72f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.87f, 0.42f, 1f));
        _celebrationFallbackTitle.text =
            "몬스터 유치원 졸업!\n\n<size=44>새로운 친구를 만나는 시작이에요.</size>";
        _celebrationFallbackTitle.rectTransform.anchorMin = new Vector2(0.08f, 0.3f);
        _celebrationFallbackTitle.rectTransform.anchorMax = new Vector2(0.92f, 0.72f);
        _celebrationFallbackTitle.rectTransform.offsetMin = Vector2.zero;
        _celebrationFallbackTitle.rectTransform.offsetMax = Vector2.zero;

        _endingContinueButton = CreateButton(
            "ContinueButton",
            _celebrationGroup.transform,
            font,
            "계속 플레이",
            buttonStyle);
        RectTransform buttonRect =
            _endingContinueButton.transform as RectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.08f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.08f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(480f, 120f);
        _endingContinueButton.onClick.AddListener(_continuePressed);
    }

    private static UnityEngine.UI.Button CreateButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        UnityEngine.UI.Button buttonStyle)
    {
        GameObject buttonObject = CreateRectObject(name, parent);
        UnityEngine.UI.Image image =
            buttonObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = buttonStyle.image != null
            ? buttonStyle.image.sprite
            : null;
        image.type = buttonStyle.image != null
            ? buttonStyle.image.type
            : UnityEngine.UI.Image.Type.Simple;
        image.color = Color.white;

        UnityEngine.UI.Button button =
            buttonObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        buttonObject.AddComponent<ButtonSFX>();

        TextMeshProUGUI text = CreateText(
            "Label",
            buttonObject.transform,
            font,
            42f,
            TextAlignmentOptions.Center,
            Color.white);
        text.text = label;
        Stretch(text.rectTransform);
        return button;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateRectObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(22f, fontSize * 0.7f);
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }
}
