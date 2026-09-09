using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// 가챠 결과를 전체 화면으로 보여 준 뒤 필드로 넘긴다.
//
// 결과 슬라임은 이 연출이 시작되기 전에 이미 만들어져 저장까지 끝나 있다. 연출이
// 도는 동안 앱이 내려가도 슬라임은 필드에 남고, 티켓만 사라지는 일이 없다. 여기서
// 하는 일은 그 슬라임을 잠깐 숨겼다가 연출 끝에 드러내는 것뿐이다.
//
// 결과가 지금 보는 스테이지에 속하지 않으면 드러내지 않고 그 방향으로 날려 보낸다.
// 하늘이면 위로, 땅이면 아래로. 화면에서 사라지는 이유를 연출이 대신 말해 준다.
// 이 처리가 없으면 높은 등급일수록 결과가 그냥 안 보인다. 가챠 후보는 최고 등급
// -1~-4이므로 Lv.15부터는 땅에서 뽑은 결과가 전부 하늘 소속이 된다.
public sealed class GachaResultDirector : MonoBehaviour
{
    private const string SkyMessage = "새 친구가 하늘로 올라갔어요!";
    private const string GroundMessage = "새 친구가 땅으로 내려갔어요!";

    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _reelViewport;
    [SerializeField] private RectTransform _reelContent;
    [SerializeField] private Image[] _reelImages;
    [SerializeField] private Image _resultImage;
    [SerializeField] private ToastMessageUI _toast;
    [SerializeField] private Clicker _clicker;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float _cycleDuration = 1.2f;
    [SerializeField, Min(0.01f)] private float _cycleInterval = 0.08f;
    [SerializeField, Min(0.01f)] private float _finalCycleInterval = 0.3f;
    [SerializeField, Min(0f)] private float _revealDuration = 0.45f;
    [SerializeField, Min(0f)] private float _holdDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.5f;

    [Header("Look")]
    [Tooltip("실루엣이 도는 동안 입힐 색입니다.")]
    [SerializeField] private Color _silhouetteColor = Color.black;
    [SerializeField, Min(1f)] private float _reelSpacing = 260f;
    [SerializeField, Min(0f)] private float _startScale = 1f;
    [SerializeField, Min(0f)] private float _revealScale = 1.2f;
    [SerializeField, Min(0f)] private float _endScale = 0.25f;

    [Tooltip("다른 스테이지로 보낼 때 화면 밖으로 이만큼 더 나갑니다.")]
    [SerializeField, Min(0f)] private float _offScreenMargin = 300f;

    private readonly List<Sprite> _silhouettes = new();
    private RectTransform _resultRect;
    private bool _isReady;
    private bool _isPlaying;

    private void Awake()
    {
        if (_root == null || _canvasGroup == null || _reelViewport == null ||
            _reelContent == null || _reelImages == null || _reelImages.Length == 0 ||
            _resultImage == null || _toast == null)
        {
            Debug.LogError("가챠 연출의 필수 참조가 비어 있습니다.", this);
            return;
        }

        _resultRect = _resultImage.transform as RectTransform;
        _isReady = _resultRect != null;
        _root.SetActive(false);
    }

    // 연출이 끝난 뒤에 onCompleted를 부른다. 참조가 없거나 이미 재생 중이면 연출을
    // 건너뛰고 즉시 부른다. 결과 슬라임은 어느 경우에도 제자리에 남아야 한다.
    public void Play(SlimeController target, Action onCompleted)
    {
        if (!_isReady || _isPlaying || target == null)
        {
            onCompleted?.Invoke();
            return;
        }

        PlayAsync(target, onCompleted).Forget();
    }

    private async UniTaskVoid PlayAsync(SlimeController target, Action onCompleted)
    {
        _isPlaying = true;
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        EGameStage resultStage = GameStageRules.GetStage(target.Grade);
        bool isSameStage = StageManager.Instance != null &&
                           StageManager.Instance.CurrentStage == resultStage;

        target.SetStagePresentationActive(false);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);

        bool isCancelled = await Present(target, isSameStage, resultStage, token);

        _clicker?.ReleaseMode(this);
        _isPlaying = false;

        // 씬이 내려가는 중이면 화면을 되돌릴 대상도 알릴 사람도 없다.
        if (isCancelled) return;

        if (isSameStage)
        {
            target.SetStagePresentationActive(true);
        }
        else
        {
            _toast.Show(resultStage == EGameStage.Sky ? SkyMessage : GroundMessage);
        }

        onCompleted?.Invoke();
    }

    private async UniTask<bool> Present(
        SlimeController target,
        bool isSameStage,
        EGameStage resultStage,
        CancellationToken token)
    {
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        _resultRect.position = center;
        _resultRect.localScale = Vector3.one * _startScale;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = true;
        PrepareReel();
        _root.SetActive(true);

        if (await Fade(0f, 1f, _fadeDuration, token)) return true;
        Sprite resultSprite = GetSprite(target.Grade);
        if (await CycleSilhouettes(resultSprite, token)) return true;

        float revealStartScale = GetRevealStartScale();
        _resultRect.localScale = Vector3.one * revealStartScale;
        _resultImage.sprite = resultSprite;
        _resultImage.enabled = resultSprite != null;
        _resultImage.color = _silhouetteColor;
        _reelViewport.SetActive(false);
        if (await RevealResult(revealStartScale, token)) return true;
        if (await Wait(_holdDuration, token)) return true;

        Vector2 destination = isSameStage
            ? GetFieldScreenPosition(target, center)
            : GetOffScreenPosition(resultStage, center);
        if (await MoveTo(destination, token)) return true;
        if (await Fade(1f, 0f, _fadeDuration, token)) return true;

        _canvasGroup.blocksRaycasts = false;
        _root.SetActive(false);
        return false;
    }

    // 해금된 일반 등급 전체를 돌린다. 후보 넷만 돌리면 결과를 미리 좁혀 보여 준다.
    private async UniTask<bool> CycleSilhouettes(
        Sprite resultSprite,
        CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < _cycleDuration)
        {
            float progress = _cycleDuration > 0f
                ? Mathf.Clamp01(elapsed / _cycleDuration)
                : 1f;
            float interval = Mathf.Lerp(
                _cycleInterval,
                Mathf.Max(_cycleInterval, _finalCycleInterval),
                progress * progress);
            bool isFinalStep = elapsed + interval >= _cycleDuration;
            if (isFinalStep)
            {
                SetIncomingWinningSilhouette(resultSprite);
            }

            if (await RollOneStep(
                    interval,
                    resetPosition: !isFinalStep,
                    token: token))
            {
                return true;
            }

            elapsed += interval;
            if (isFinalStep) break;

            FillReel();
        }

        return await Wait(
            Mathf.Max(_cycleInterval, _finalCycleInterval),
            token);
    }

    // 루트를 켜기 전에 릴 전체를 준비한다. 그렇지 않으면 Image의 빈 흰 사각형이나
    // 직전 가챠 결과가 한 프레임 먼저 보인다.
    private void PrepareReel()
    {
        CollectSilhouettes();
        _resultImage.enabled = false;
        _reelContent.anchoredPosition = Vector2.zero;
        _reelViewport.SetActive(_silhouettes.Count > 0);
        FillReel();
    }

    private void FillReel()
    {
        for (int i = 0; i < _reelImages.Length; i++)
        {
            Image image = _reelImages[i];
            if (image == null) continue;

            bool hasSilhouette = _silhouettes.Count > 0;
            image.enabled = hasSilhouette;
            image.color = _silhouetteColor;
            image.sprite = hasSilhouette
                ? _silhouettes[UnityEngine.Random.Range(0, _silhouettes.Count)]
                : null;
        }
    }

    // 릴이 위로 한 칸 이동하므로 현재 중앙 바로 아래 칸이 다음 중앙 칸이 된다.
    // 마지막 이동 전에 여기에 결과를 넣어야 보이던 실루엣과 공개 결과가 일치한다.
    private void SetIncomingWinningSilhouette(Sprite resultSprite)
    {
        if (_reelImages.Length == 0 || resultSprite == null) return;

        int incomingIndex = Mathf.Max(0, _reelImages.Length / 2 - 1);
        Image incomingImage = _reelImages[incomingIndex];
        if (incomingImage == null) return;

        incomingImage.enabled = true;
        incomingImage.color = _silhouetteColor;
        incomingImage.sprite = resultSprite;
    }

    private async UniTask<bool> RollOneStep(
        float duration,
        bool resetPosition,
        CancellationToken token)
    {
        Vector2 start = Vector2.zero;
        Vector2 end = Vector2.up * _reelSpacing;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (await NextFrame(token)) return true;

            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            _reelContent.anchoredPosition = Vector2.Lerp(start, end, ratio);
        }

        if (resetPosition)
        {
            _reelContent.anchoredPosition = Vector2.zero;
        }

        return false;
    }

    private float GetRevealStartScale()
    {
        if (_reelImages.Length == 0) return _startScale;

        int incomingIndex = Mathf.Max(0, _reelImages.Length / 2 - 1);
        RectTransform reelRect = _reelImages[incomingIndex]?.rectTransform;
        if (reelRect == null || _resultRect.rect.width <= 0f ||
            _resultRect.rect.height <= 0f)
        {
            return _startScale;
        }

        return Mathf.Min(
            reelRect.rect.width / _resultRect.rect.width,
            reelRect.rect.height / _resultRect.rect.height);
    }

    private async UniTask<bool> RevealResult(
        float revealStartScale,
        CancellationToken token)
    {
        if (_revealDuration <= 0f)
        {
            _resultImage.color = Color.white;
            _resultRect.localScale = Vector3.one * _revealScale;
            return false;
        }

        float elapsed = 0f;
        while (elapsed < _revealDuration)
        {
            if (await NextFrame(token)) return true;

            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / _revealDuration));
            _resultImage.color = Color.Lerp(_silhouetteColor, Color.white, ratio);
            _resultRect.localScale = Vector3.one * Mathf.Lerp(
                revealStartScale,
                _revealScale,
                ratio);
        }

        _resultImage.color = Color.white;
        _resultRect.localScale = Vector3.one * _revealScale;
        return false;
    }

    private void CollectSilhouettes()
    {
        _silhouettes.Clear();

        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return;

        for (int grade = (int)ESlimeGrade.Grade1;
             grade <= (int)manager.HighestGrade;
             grade++)
        {
            Sprite sprite = GetSprite((ESlimeGrade)grade);
            if (sprite == null) continue;

            _silhouettes.Add(sprite);
        }
    }

    private static Sprite GetSprite(ESlimeGrade grade)
    {
        Slime slime = SlimeManager.Instance != null
            ? SlimeManager.Instance.Get(grade)
            : null;
        return slime?.SpecData?.Sprite;
    }

    private static Vector2 GetFieldScreenPosition(
        SlimeController target,
        Vector2 fallback)
    {
        Camera camera = Camera.main;
        return camera != null
            ? (Vector2)camera.WorldToScreenPoint(target.transform.position)
            : fallback;
    }

    private Vector2 GetOffScreenPosition(EGameStage resultStage, Vector2 center)
    {
        float y = resultStage == EGameStage.Sky
            ? Screen.height + _offScreenMargin
            : -_offScreenMargin;
        return new Vector2(center.x, y);
    }

    private async UniTask<bool> MoveTo(Vector2 destination, CancellationToken token)
    {
        Vector2 start = _resultRect.position;
        float startScale = _resultRect.localScale.x;
        float elapsed = 0f;

        while (elapsed < _moveDuration)
        {
            if (await NextFrame(token)) return true;

            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / _moveDuration);
            _resultRect.position = Vector2.Lerp(start, destination, ratio);
            _resultRect.localScale =
                Vector3.one * Mathf.Lerp(startScale, _endScale, ratio);
        }

        return false;
    }

    private async UniTask<bool> Fade(
        float from,
        float to,
        float duration,
        CancellationToken token)
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
            _canvasGroup.alpha =
                Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
        }

        _canvasGroup.alpha = to;
        return false;
    }

    // 일시정지 연출과 무관해야 하므로 스케일되지 않은 시간을 쓴다.
    private static async UniTask<bool> Wait(float seconds, CancellationToken token)
    {
        return await UniTask
            .Delay(
                TimeSpan.FromSeconds(seconds),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token)
            .SuppressCancellationThrow();
    }

    private static async UniTask<bool> NextFrame(CancellationToken token)
    {
        return await UniTask.Yield(PlayerLoopTiming.Update, token)
            .SuppressCancellationThrow();
    }
}
