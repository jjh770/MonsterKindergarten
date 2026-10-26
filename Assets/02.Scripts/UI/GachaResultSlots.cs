using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 여러 개를 뽑았을 때 열린 슬라임이 머무는 자리들이다. 캡슐이 열린 자리에서 슬라임이 솟아 이름과 함께
// 남고, 모두 열리면 차례로 필드로 날아간다. 몇 번째를 언제 열고 보낼지는 GachaResultDirector가 정한다.
//
// 감독과 같은 오브젝트에 붙는다. 연출 화면은 닫힐 때 꺼지지만 이 컴포넌트는 꺼지지 않아야, 화면을 열기
// 전에 자리를 비워 둘 수 있다.
public sealed class GachaResultSlots : MonoBehaviour
{
    // 한 번 뽑은 결과가 열린 뒤 필드로 날아가기 전까지 머무는 자리다.
    [Serializable]
    private sealed class ResultSlot
    {
        public RectTransform Root;
        public Image Slime;
        public SlimeOutlineImage Outline;
        public GachaResultAura Aura;
        public TMP_Text Name;
    }

    [SerializeField] private ResultSlot[] _slots;
    [SerializeField, Min(0f)] private float _slotEmergeSeconds = 0.5f;
    [Tooltip("자리마다 필드로 출발하는 간격입니다. 잇따라 출발해 연달아 도착합니다.")]
    [SerializeField, Min(0f)] private float _flyStagger = 0.18f;

    // 다섯 마리가 함께 날아가므로 1회보다 호를 좁혀 서로 엉키지 않게 한다.
    private const float ArcScale = 0.6f;

    private bool[] _shimmer;
    private int _pendingEmerges;

    public int Capacity => _slots != null ? _slots.Length : 0;

    // 솟아오르는 중인 자리가 있다. 모두 끝나야 "모두 열었다"고 안내한다.
    public bool IsEmerging => _pendingEmerges > 0;

    private void Awake()
    {
        if (!IsValid())
        {
            Debug.LogError("뽑기 결과 자리의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _shimmer = new bool[_slots.Length];
    }

    private void Update()
    {
        if (_shimmer == null) return;

        for (int i = 0; i < _shimmer.Length; i++)
        {
            if (_shimmer[i]) _slots[i].Slime.color = RainbowTint.Shimmer();
        }
    }

    public bool IsValid()
    {
        if (_slots == null || _slots.Length == 0) return false;

        foreach (ResultSlot slot in _slots)
        {
            if (slot == null || slot.Root == null || slot.Slime == null || slot.Outline == null ||
                slot.Aura == null ||
                slot.Name == null)
            {
                return false;
            }
        }

        return true;
    }

    // 지난번 연출의 흔적이 남지 않게 모든 자리를 비운다.
    public void ResetAll()
    {
        if (_shimmer == null) return;

        foreach (ResultSlot slot in _slots)
        {
            slot.Aura.Hide();
            slot.Outline.Apply(null, false, false);
            slot.Slime.enabled = false;
            slot.Name.gameObject.SetActive(false);
        }

        StopShimmer();
        _pendingEmerges = 0;
    }

    public void StopShimmer()
    {
        if (_shimmer != null) Array.Clear(_shimmer, 0, _shimmer.Length);
    }

    // 이번에 쓸 count개 자리만 켜고 비워 둔다.
    public void Prepare(int count)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            ResultSlot slot = _slots[i];
            slot.Slime.enabled = false;
            slot.Name.gameObject.SetActive(false);
            slot.Root.gameObject.SetActive(i < count);
        }

        _pendingEmerges = 0;
    }

    public Vector2 GetRootPosition(int index)
    {
        return _slots[index].Root.anchoredPosition;
    }

    // 열린 캡슐 자리에서 슬라임이 솟아 이름과 함께 남는다. 다른 캡슐을 여는 것을 막지 않도록 기다리지
    // 않고 따로 돈다. 다 끝나면 IsEmerging이 꺼진다.
    public void StartEmerge(int index, Sprite sprite, string name, EGachaRarity rarity, CancellationToken token)
    {
        _pendingEmerges++;
        Emerge(index, sprite, name, rarity, token).Forget();
    }

    // index번 자리의 슬라임이 필드 자리로 날아가 endScale로 도착한다. 필드 자리(결과 화면 좌표)는 출발하는
    // 순간에 getFieldLocal로 묻는다. 순서를 기다리는 동안 슬라임이 움직였을 수 있기 때문이다. 도착하면
    // onArrive가 불린다. 그림은 그 자리에 남아 화면과 함께 사라진다. 진짜 슬라임은 어두운 배경 뒤에 있어서,
    // 그림을 바로 끄면 배경 뒤로 숨는 것처럼 보인다.
    public async UniTask<bool> Fly(
        int index,
        Func<Vector2> getFieldLocal,
        float endScale,
        GachaResultFlight flight,
        Color color,
        bool isSpecial,
        Action onArrive,
        CancellationToken token)
    {
        float delay = index * _flyStagger;
        if (delay > 0f && await Wait(delay, token)) return true;

        ResultSlot slot = _slots[index];
        slot.Aura.PlayTrail(color, isSpecial);
        RectTransform rect = slot.Slime.rectTransform;
        Vector2 destination = getFieldLocal() - slot.Root.anchoredPosition;
        Vector2 start = rect.anchoredPosition;
        float startScale = rect.localScale.x;
        GachaResultFlight narrow = flight.WithArcScale(ArcScale);
        _shimmer[index] = false;

        float elapsed = 0f;
        while (elapsed < flight.Duration)
        {
            if (await NextFrame(token)) return true;

            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, flight.Duration));
            narrow.Apply(rect, start, destination, startScale, endScale, ratio);
            slot.Slime.color = Color.white;
            slot.Name.alpha = 1f - Mathf.Clamp01(ratio * 2.5f);
        }

        rect.anchoredPosition = destination;
        rect.localRotation = Quaternion.identity;
        onArrive?.Invoke();
        slot.Name.gameObject.SetActive(false);
        slot.Aura.PlayBurst(color, isSpecial);

        // 1회와 같이 도착한 자리에서 한 번 튄다.
        float burst = slot.Aura.BurstSeconds;
        float pulseElapsed = 0f;
        while (pulseElapsed < burst)
        {
            if (await NextFrame(token)) return true;

            pulseElapsed += Time.unscaledDeltaTime;
            float ratio = Normalized(pulseElapsed, burst);
            rect.localScale = Vector3.one *
                              (endScale * (1f + Mathf.Sin(ratio * Mathf.PI) * GachaResultFlight.ArrivalPulse));
        }

        rect.localScale = Vector3.one * endScale;
        return false;
    }

    // 자리의 그림은 1회 결과 그림보다 작아서, 같은 배율로 줄이면 필드 슬라임보다 작게 도착한다.
    // 도착했을 때 너비가 arrivalWidth가 되는 배율을 돌려준다.
    public float GetEndScaleForWidth(int index, float arrivalWidth)
    {
        return arrivalWidth / Mathf.Max(1f, _slots[index].Slime.rectTransform.rect.width);
    }

    private async UniTaskVoid Emerge(
        int index,
        Sprite sprite,
        string name,
        EGachaRarity rarity,
        CancellationToken token)
    {
        try
        {
            ResultSlot slot = _slots[index];
            bool isSpecial = rarity == EGachaRarity.Special;
            slot.Aura.Show(rarity);
            slot.Outline.Apply(sprite, sprite != null, isSpecial);
            slot.Slime.enabled = slot.Slime.sprite != null;
            slot.Slime.color = Color.white;
            RectTransform rect = slot.Slime.rectTransform;
            rect.anchoredPosition = Vector2.down * 30f;
            rect.localScale = Vector3.one * 0.2f;
            slot.Name.text = name;
            slot.Name.alpha = 0f;
            slot.Name.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _slotEmergeSeconds)
            {
                if (await NextFrame(token)) return;

                elapsed += Time.unscaledDeltaTime;
                float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _slotEmergeSeconds));
                rect.anchoredPosition = Vector2.Lerp(Vector2.down * 30f, Vector2.zero, ratio);
                rect.localScale = Vector3.one * (Mathf.Lerp(0.2f, 1f, ratio) +
                                                 Mathf.Sin(ratio * Mathf.PI) * 0.15f);
                slot.Name.alpha = Mathf.InverseLerp(0.35f, 0.9f, ratio);
            }

            rect.localScale = Vector3.one;
            slot.Slime.color = Color.white;
            slot.Name.alpha = 1f;
            _shimmer[index] = isSpecial;
        }
        finally
        {
            _pendingEmerges--;
        }
    }

    private static float Normalized(float elapsed, float duration) =>
        duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

    private static async UniTask<bool> Wait(float seconds, CancellationToken token) =>
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime,
            cancellationToken: token).SuppressCancellationThrow();

    private static async UniTask<bool> NextFrame(CancellationToken token) =>
        await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
}
