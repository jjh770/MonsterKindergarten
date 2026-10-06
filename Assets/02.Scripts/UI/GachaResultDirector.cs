using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 이미 생성되고 저장된 뽑기 결과를 뽑기 기계로 공개한 뒤 필드로 넘긴다.
// 기계 그림과 캡슐 움직임은 GachaMachineView가 맡고, 여기서는 순서와 결과 슬라임의 등장을 정한다.
// 빛의 색은 뽑을 때 확정된 가중치 희귀도를 표현할 뿐, 터치 시 결과를 다시 뽑지 않는다.
public sealed class GachaResultDirector : MonoBehaviour
{
    private static string InsertTapMessage => UiMessages.MachineInsertTap;
    private static string CapsuleTapMessage => UiMessages.MachineCapsuleTap;

    private static readonly Color CommonColor = new(0.35f, 1f, 0.72f, 1f);
    private static readonly Color UncommonColor = new(0.32f, 0.78f, 1f, 1f);
    private static readonly Color RareColor = new(0.67f, 0.42f, 1f, 1f);
    private static readonly Color JackpotColor = new(1f, 0.76f, 0.22f, 1f);

    private static string SpecialSubtitle => UiMessages.SpecialSlimeSubtitle;

    // 파편이 퍼지는 모양이다. 거리는 캔버스 좌표 단위이고, 끝 거리는 파편마다
    // EndSpread씩 EndSpreadSteps 칸으로 엇갈려 한 줄로 늘어서지 않게 한다.
    private struct BurstShape
    {
        public float AngleJitter;
        public float StartDistance;
        public float EndDistance;
        public float EndSpread;
        public int EndSpreadSteps;
        public float StartScale;
        public float EndScale;
    }

    private static readonly BurstShape ArrivalBurstShape = new()
    {
        AngleJitter = 0.16f,
        StartDistance = 18f,
        EndDistance = 170f,
        EndSpread = 18f,
        EndSpreadSteps = 4,
        StartScale = 1.3f,
        EndScale = 0.2f,
    };

    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _resultImage;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private SlimeManager _slimeManager;

    [Header("Machine")]
    [SerializeField] private GachaMachineView _machine;
    [SerializeField] private Button _tapButton;
    [SerializeField] private TMP_Text _tapPrompt;
    [SerializeField] private TMP_Text _resultNameText;
    [SerializeField] private RectTransform _arrivalEffectRoot;
    [SerializeField] private Image _arrivalShockwave;
    [SerializeField] private RectTransform[] _arrivalSparks;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float _emergeDuration = 0.45f;
    [SerializeField, Min(0f)] private float _revealDuration = 0.6f;
    [SerializeField, Min(0f)] private float _holdDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.5f;
    [SerializeField, Min(0f)] private float _arrivalDuration = 0.45f;

    [Header("Look")]
    [SerializeField] private Color _silhouetteColor = Color.black;
    [SerializeField, Min(0f)] private float _emergeScale = 1.35f;
    [SerializeField, Min(0f)] private float _revealScale = 1f;
    [SerializeField, Min(0f)] private float _endScale = 0.25f;
    [SerializeField, Min(0f)] private float _nameSlideDistance = 180f;
    [SerializeField, Min(0f)] private float _moveArcRadius = 160f;
    [SerializeField, Min(0f)] private float _moveSpinTurns = 1f;

    // 결과 슬라임이 솟아오르는 세로 이동 거리. 캔버스 좌표(anchoredPosition) 단위다.
    [SerializeField, Min(0f)] private float _emergeRise = 80f;

    private RectTransform _resultRect;
    private RectTransform _resultParentRect;
    private RectTransform _resultNameRect;
    private Vector2 _resultNameRestPosition;
    private bool _isReady;
    private bool _isPlaying;
    private bool _tapped;
    private bool _isSpecialResult;
    // 결과 그림이 다 드러난 뒤에만 무지개 광택을 입힌다. 드러나는 중에는 실루엣 색이 먼저다.
    private bool _isResultImageShimmering;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        if (_root == null || _canvasGroup == null || _resultImage == null ||
            _machine == null || _tapButton == null || _tapPrompt == null ||
            _resultNameText == null || _arrivalEffectRoot == null ||
            _arrivalShockwave == null || _arrivalSparks == null ||
            _slimeManager == null)
        {
            Debug.LogError("뽑기 기계 연출의 필수 참조가 비어 있습니다.", this);
            return;
        }

        _resultRect = _resultImage.transform as RectTransform;
        _resultParentRect = _resultRect != null
            ? _resultRect.parent as RectTransform
            : null;
        _resultNameRect = _resultNameText.transform as RectTransform;
        _isReady = _resultRect != null && _resultParentRect != null && _resultNameRect != null;
        if (_resultNameRect != null)
        {
            _resultNameRestPosition = _resultNameRect.anchoredPosition;
        }
        _tapButton.onClick.AddListener(OnTapped);
        _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_tapButton != null) _tapButton.onClick.RemoveListener(OnTapped);
        AudioManager.Instance?.StopLoopingSFX();
        _clicker?.ReleaseMode(this);
    }

    public void Play(
        SlimeController target,
        EGachaRarity rarity,
        Action onCompleted)
    {
        if (!_isReady || _isPlaying || target == null)
        {
            onCompleted?.Invoke();
            return;
        }

        PlayAsync(target, rarity, onCompleted).Forget();
    }

    private async UniTaskVoid PlayAsync(
        SlimeController target,
        EGachaRarity rarity,
        Action onCompleted)
    {
        _isPlaying = true;
        CancellationToken token = this.GetCancellationTokenOnDestroy();
        target.SetLocationPresentationActive(false);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        bool isCancelled = false;
        try
        {
            isCancelled = await Present(
                target,
                rarity,
                token);
        }
        finally
        {
            AudioManager.Instance?.StopLoopingSFX();
            _clicker?.ReleaseMode(this);
            _isResultImageShimmering = false;
            _isPlaying = false;
        }

        if (isCancelled) return;
        target.SetLocationPresentationActive(true);
        onCompleted?.Invoke();
    }

    private async UniTask<bool> Present(
        SlimeController target,
        EGachaRarity rarity,
        CancellationToken token)
    {
        // 기계와 결과물은 결과 이미지의 부모 캔버스 로컬 좌표로만 움직인다. 화면 픽셀을
        // 그대로 position에 넣으면 CanvasScaler가 켜진 해상도에서 좌표계가 어긋난다.
        _isSpecialResult = rarity == EGachaRarity.Special;
        _isResultImageShimmering = false;
        Vector2 center = GetLocalCenter();
        PrepareMachine(center, target.Grade);
        _root.SetActive(true);

        if (await Fade(0f, 1f, _fadeDuration, token)) return true;
        if (await _machine.Appear(token)) return true;

        if (await WaitForTap(InsertTapMessage, _machine.Idle, token)) return true;
        if (await _machine.InsertTicket(token)) return true;
        if (await _machine.Dispense(token)) return true;

        if (await WaitForTap(CapsuleTapMessage, _machine.IdleCapsule, token)) return true;

        Color resultColor = GetResultColor(rarity);
        if (await _machine.Open(rarity, resultColor, token)) return true;
        if (await Emerge(GetSprite(target.Grade), resultColor, token)) return true;
        if (await Reveal(resultColor, token)) return true;
        if (await Wait(_holdDuration, token)) return true;

        Vector2 destination = GetFieldLocalPosition(target, center);
        AudioManager.Instance?.PlaySFX(EAudioSfx.GachaResult);
        if (await MoveTo(destination, token)) return true;
        if (await PlayArrivalEffect(destination, resultColor, token)) return true;
        if (await Fade(1f, 0f, _fadeDuration, token)) return true;

        CloseOverlay();
        return false;
    }

    private void PrepareMachine(Vector2 centerLocal, ESlimeGrade grade)
    {
        _tapped = false;
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        _machine.Prepare(_isSpecialResult);
        _tapButton.interactable = false;
        _tapPrompt.gameObject.SetActive(false);

        _resultRect.anchoredPosition = centerLocal + Vector2.down * _emergeRise;
        _resultRect.localScale = Vector3.one * 0.2f;
        _resultImage.enabled = false;
        _resultImage.color = _silhouetteColor;

        _resultNameText.text = _isSpecialResult
            ? GetName(grade) + "\n<size=65%>" + SpecialSubtitle + "</size>"
            : GetName(grade);
        _resultNameText.alpha = 0f;
        _resultNameText.gameObject.SetActive(true);
        _resultNameRect.anchoredPosition =
            _resultNameRestPosition + Vector2.right * _nameSlideDistance;

        _arrivalEffectRoot.gameObject.SetActive(false);
        _arrivalEffectRoot.localScale = Vector3.one;
        SetImageAlpha(_arrivalShockwave, 0f);
        HideSparks(_arrivalSparks);
    }

    // 안내 문구를 띄우고 화면을 누를 때까지 기다린다. 기다리는 동안의 움직임은 idle이 그린다.
    private async UniTask<bool> WaitForTap(
        string prompt,
        Action<float> idle,
        CancellationToken token)
    {
        _tapped = false;
        _tapButton.interactable = true;
        _tapPrompt.text = prompt;
        _tapPrompt.gameObject.SetActive(true);
        float elapsed = 0f;
        while (!_tapped)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            idle(elapsed);
            _tapPrompt.alpha = 0.68f + Mathf.Sin(elapsed * 3f) * 0.22f;
        }

        _tapButton.interactable = false;
        _tapPrompt.gameObject.SetActive(false);
        return false;
    }

    private async UniTask<bool> Emerge(
        Sprite resultSprite,
        Color resultColor,
        CancellationToken token)
    {
        _resultImage.sprite = resultSprite;
        _resultImage.enabled = resultSprite != null;
        _resultImage.color = _silhouetteColor;
        Vector2 start = _resultRect.anchoredPosition;
        Vector2 end = start + Vector2.up * _emergeRise;
        float elapsed = 0f;

        while (elapsed < _emergeDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _emergeDuration));
            _resultRect.anchoredPosition = Vector2.Lerp(start, end, ratio);
            _resultRect.localScale = Vector3.one * Mathf.Lerp(0.2f, _emergeScale, ratio);
            _machine.SetMachineAlpha(1f - ratio * 0.7f);
            _machine.Sustain(elapsed, Tint(resultColor));
        }
        return false;
    }

    private async UniTask<bool> Reveal(Color resultColor, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < _revealDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _revealDuration));
            float sparkle = Mathf.Sin(ratio * Mathf.PI * 3f) * (1f - ratio) * 0.08f;
            float nameRatio = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.12f, 0.72f, ratio));
            _resultImage.color = Color.Lerp(_silhouetteColor, Color.white, ratio);
            _resultRect.localScale = Vector3.one *
                                     (Mathf.Lerp(_emergeScale, _revealScale, ratio) + sparkle);
            _resultNameText.alpha = nameRatio;
            _resultNameRect.anchoredPosition = Vector2.Lerp(
                _resultNameRestPosition + Vector2.right * _nameSlideDistance,
                _resultNameRestPosition,
                nameRatio);
            _machine.SetMachineAlpha(0.3f * (1f - ratio));
            _machine.Sustain(_emergeDuration + elapsed, Tint(resultColor));
        }

        _resultImage.color = Color.white;
        _isResultImageShimmering = _isSpecialResult;
        _resultRect.localScale = Vector3.one * _revealScale;
        _resultNameText.alpha = 1f;
        _resultNameRect.anchoredPosition = _resultNameRestPosition;
        _machine.EndSustain();
        _machine.Hide();
        return false;
    }

    // 바깥으로 퍼지며 작아지고 옅어지는 파편이다. 거리, 크기, 엇갈림은 BurstShape이 든다.
    private static void AnimateSparkBurst(
        RectTransform[] sparks,
        in BurstShape shape,
        float ratio,
        Color color)
    {
        int count = sparks.Length;
        for (int i = 0; i < count; i++)
        {
            RectTransform spark = sparks[i];
            if (spark == null) continue;

            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) +
                          (i % 2) * shape.AngleJitter;
            float endDistance = shape.EndDistance + i % shape.EndSpreadSteps * shape.EndSpread;
            SetRadial(spark, angle, Mathf.Lerp(shape.StartDistance, endDistance, ratio));
            spark.localScale = Vector3.one * Mathf.Lerp(shape.StartScale, shape.EndScale, ratio);
            SetImageColor(spark.GetComponent<Image>(), color, 1f - ratio);
        }
    }

    // 중심에서 angle(라디안) 방향으로 distance만큼 떨어진 자리에 놓고 바깥을 향해 세운다.
    // 스프라이트가 위쪽을 보고 그려져 있어서 90도를 뺀다.
    private static void SetRadial(RectTransform spark, float angle, float distance)
    {
        spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        spark.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
    }

    private void HideSparks(RectTransform[] sparks)
    {
        foreach (RectTransform spark in sparks)
        {
            if (spark == null) continue;
            spark.anchoredPosition = Vector2.zero;
            spark.localScale = Vector3.zero;
            SetImageAlpha(spark.GetComponent<Image>(), 0f);
        }
    }

    private void AnimateArrivalSparks(float ratio, Color color)
    {
        AnimateSparkBurst(_arrivalSparks, ArrivalBurstShape, ratio, color);
    }

    private void OnTapped()
    {
        if (!_isPlaying || _tapped) return;

        _tapped = true;
    }

    // 특별한 결과는 정해진 색 대신 색상환을 도는 색을 쓴다. 나머지는 그대로 돌려준다.
    private Color Tint(Color color)
    {
        if (!_isSpecialResult) return color;

        return RainbowTint.Pure();
    }

    private void Update()
    {
        if (!_isResultImageShimmering) return;

        _resultImage.color = RainbowTint.Shimmer();
    }

    private static Color GetResultColor(EGachaRarity rarity)
    {
        return rarity switch
        {
            // 색은 Tint가 프레임마다 무지개로 덮는다. 여기서는 시작 색만 정한다.
            EGachaRarity.Special => Color.white,
            EGachaRarity.Jackpot => JackpotColor,
            EGachaRarity.Rare => RareColor,
            EGachaRarity.Uncommon => UncommonColor,
            _ => CommonColor
        };
    }

    private static void SetImageColor(Image image, Color color, float alpha)
    {
        if (image != null)
            image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static float Normalized(float elapsed, float duration) =>
        duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

    private Sprite GetSprite(ESlimeGrade grade)
    {
        Slime slime = _slimeManager.Get(grade);
        return slime?.SpecData?.Sprite;
    }

    private string GetName(ESlimeGrade grade)
    {
        Slime slime = _slimeManager.Get(grade);
        return slime?.SpecData?.Name ?? grade.ToString();
    }

    // 부모 rect의 중앙 로컬 좌표. pivot이 가운데가 아니어도 맞도록 rect.center를 쓴다.
    private Vector2 GetLocalCenter()
    {
        return _resultParentRect != null ? _resultParentRect.rect.center : Vector2.zero;
    }

    // 슬라임의 월드 위치를 화면 좌표로 옮긴 뒤 결과 이미지 부모의 로컬 좌표로 되돌린다.
    // 정상 파일들이 쓰는 표준 경로(WorldToScreenPoint -> ScreenPointToLocalPointInRectangle)다.
    private Vector2 GetFieldLocalPosition(SlimeController target, Vector2 fallbackLocal)
    {
        Camera camera = Camera.main;
        if (camera == null || _resultParentRect == null) return fallbackLocal;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            camera, target.transform.position);

        // 오버레이 캔버스면 카메라를 넘기지 않는다. 카메라 캔버스면 그 캔버스 카메라를 쓴다.
        Camera uiCamera = ResolveUiCamera();
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _resultParentRect, screenPoint, uiCamera, out Vector2 local)
            ? local
            : fallbackLocal;
    }

    private Camera ResolveUiCamera()
    {
        Canvas canvas = _resultParentRect != null
            ? _resultParentRect.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null) return null;

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    // destination은 결과 이미지 부모의 로컬 좌표다. 시작점도 anchoredPosition을 써서
    // 두 끝점이 같은 좌표계 위에 놓이게 한다.
    private async UniTask<bool> MoveTo(Vector2 destination, CancellationToken token)
    {
        Vector2 start = _resultRect.anchoredPosition;
        Vector2 path = destination - start;
        Vector2 forward = path.sqrMagnitude > 0.01f ? path.normalized : Vector2.up;
        Vector2 perpendicular = new(-forward.y, forward.x);
        float startScale = _resultRect.localScale.x;
        float elapsed = 0f;
        while (elapsed < _moveDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _moveDuration));
            float angle = ratio * Mathf.PI * 2f;
            float envelope = Mathf.Sin(ratio * Mathf.PI);
            Vector2 orbit =
                (perpendicular * Mathf.Sin(angle) +
                 forward * (1f - Mathf.Cos(angle)) * 0.45f) *
                (_moveArcRadius * envelope);
            _resultRect.anchoredPosition = Vector2.Lerp(start, destination, ratio) + orbit;
            _resultRect.localScale = Vector3.one * Mathf.Lerp(startScale, _endScale, ratio);
            _resultRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                -360f * _moveSpinTurns * ratio);
            _resultNameText.alpha = 1f - Mathf.Clamp01(ratio * 2.5f);
            _resultNameRect.anchoredPosition =
                _resultNameRestPosition + Vector2.left * _nameSlideDistance * ratio;
        }


        _resultRect.anchoredPosition = destination;
        _resultRect.localScale = Vector3.one * _endScale;
        _resultRect.localRotation = Quaternion.identity;
        _resultNameText.alpha = 0f;
        return false;
    }

    private async UniTask<bool> PlayArrivalEffect(
        Vector2 destination,
        Color color,
        CancellationToken token)
    {
        // 도착 이펙트 루트는 결과 이미지와 같은 부모를 공유하므로 로컬 좌표를 그대로 쓴다.
        _arrivalEffectRoot.anchoredPosition = destination;
        _arrivalEffectRoot.localScale = Vector3.one;
        _arrivalEffectRoot.gameObject.SetActive(true);
        HideSparks(_arrivalSparks);
        float elapsed = 0f;

        while (elapsed < _arrivalDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Normalized(elapsed, _arrivalDuration);
            float inverse = 1f - ratio;
            _arrivalShockwave.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(0.35f, 2.1f, ratio);
            SetImageColor(_arrivalShockwave, Tint(color), inverse * 0.85f);
            AnimateArrivalSparks(ratio, Tint(color));
            _resultRect.localScale = Vector3.one *
                                     (_endScale * (1f + Mathf.Sin(ratio * Mathf.PI) * 0.28f));
        }

        _resultRect.localScale = Vector3.one * _endScale;
        _arrivalEffectRoot.gameObject.SetActive(false);
        return false;
    }

    private async UniTask<bool> Fade(float from, float to, float duration, CancellationToken token)
    {
        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            return false;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Normalized(elapsed, duration));
        }
        _canvasGroup.alpha = to;
        return false;
    }

    private static async UniTask<bool> Wait(float seconds, CancellationToken token) =>
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime,
            cancellationToken: token).SuppressCancellationThrow();

    private void CloseOverlay()
    {
        _tapButton.interactable = false;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _root.SetActive(false);
    }

    private static async UniTask<bool> NextFrame(CancellationToken token) =>
        await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
}
