using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 장식장 놀이터의 대포. 다가온 슬라임을 한 마리 빨아들여 잠시 물고 있다가
// 무작위 방향으로 쏜다.
//
// 콜라이더는 범퍼와 같은 이유로 트리거다. 단단한 콜라이더면 슬라임이 먼저
// 물리적으로 튕겨 나가 들어올 수가 없다.
//
// 다 빨려 들어간 슬라임은 SpriteRenderer를 끈다. 색의 알파를 내리는 것으로는
// 안 된다. 슬라임의 머티리얼이 테두리를 따로 그리는 셰이더(Sprites/Outline)라,
// 알파를 내리면 몸통만 사라지고 흰 테두리만 남는다.
//
// 대신 켜는 순서를 지켜야 한다. SlimeController는 잠금을 풀 때 "그림이 켜져
// 있는가"로 상호작용 여부를 정하므로, 그림을 먼저 켜고 잠금을 푼다. 순서가
// 바뀌면 보이지만 만질 수 없는 슬라임이 남는다.
public class DisplayRoomCannon : MonoBehaviour, IPlaygroundObject
{
    private enum State
    {
        Ready,
        Charging,
        Reloading,
    }

    [Header("Fire")]
    [Tooltip("쏘는 속도입니다. 슬라임의 평소 배회 속도는 1, 범퍼는 8입니다.")]
    [SerializeField, Min(0.1f)] private float _launchSpeed = 14f;

    [Tooltip("물고 나서 쏠 때까지 기다리는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float _chargeDuration = 3f;

    [Tooltip("쏜 뒤 다시 물 수 있을 때까지입니다. 없으면 나간 슬라임이 곧바로 다시 빨려 들어갑니다.")]
    [SerializeField, Min(0f)] private float _reloadDuration = 0.6f;

    [Header("References")]
    [Tooltip("장전 중 회전해서 발사 방향을 보여 줄 포신입니다. 이 오브젝트의 오른쪽(+X)이 총구 방향입니다.")]
    [SerializeField] private Transform _barrel;

    [Tooltip("슬라임이 튀어나올 총구 위치입니다. 비우면 대포 중심에서 나갑니다.")]
    [SerializeField] private Transform _muzzle;

    [Header("Load")]
    [Tooltip("슬라임이 빨려 들어가는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.05f)] private float _loadDuration = 0.25f;

    [Tooltip("물고 있는 동안 줄어드는 크기 비율입니다.")]
    [SerializeField, Range(0.01f, 1f)] private float _loadedScale = 0.15f;

    [Tooltip("포신이 멈추기 전에 몇 바퀴 돌지입니다.")]
    [SerializeField, Min(0f)] private float _aimSpins = 3f;

    [Header("Feedback")]
    [SerializeField] private AudioClip _loadSound;
    [SerializeField] private AudioClip _fireSound;
    [SerializeField, Min(0f)] private float _firePunchScale = 0.35f;

    private Collider2D _collider;
    private State _state = State.Ready;
    private SlimeController _held;
    private Vector3 _heldBaseScale;

    // 내가 끈 것만 담는다. 원래 꺼져 있던 그림을 켜 주면 없던 슬라임이 나타난다.
    private readonly List<SpriteRenderer> _hiddenRenderers = new();
    private Sequence _sequence;
    private Tween _punchTween;
    private Vector3 _baseScale;
    private float _reloadRemaining;

    private Vector3 MuzzlePosition => _muzzle != null ? _muzzle.position : transform.position;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _baseScale = transform.localScale;

        if (_collider == null)
        {
            Debug.LogError("대포에 Collider2D가 없습니다. 원형 콜라이더를 붙이세요.", this);
            enabled = false;
            return;
        }

        if (!_collider.isTrigger)
        {
            Debug.LogError(
                "대포의 콜라이더는 트리거여야 합니다. 단단한 콜라이더면 슬라임이 " +
                "먼저 튕겨 나가 들어올 수 없습니다.",
                this);
            enabled = false;
            return;
        }

        // Clicker.TrySelect가 레이어 마스크 없이 맨 앞의 것 하나만 집는다. 대포가
        // 슬라임보다 앞에 걸리면 그 탭이 사라져 관찰이 열리지 않는다.
        if ((Physics2D.DefaultRaycastLayers & (1 << gameObject.layer)) != 0)
        {
            Debug.LogWarning(
                "대포가 기본 레이캐스트 대상 레이어에 있습니다. 슬라임을 누르는 탭을 " +
                "가로챌 수 있습니다. Ignore Raycast 레이어로 옮기세요.",
                this);
        }
    }

    // 배치 모드처럼 놀이터를 만지는 동안 기능을 세운다. 컴포넌트를 끄면 OnDisable이
    // 돌아 물고 있던 슬라임까지 되돌려 준다. 반환 경로를 새로 만들 필요가 없다.
    public void SetInteractive(bool isInteractive)
    {
        enabled = isInteractive;

        if (_collider != null) _collider.enabled = isInteractive;
    }

    private void OnDisable()
    {
        // 공간이 바뀌면 통째로 꺼진다. 물고 있던 슬라임을 놓아 주지 않으면
        // 작아지고 잠긴 채로 영영 남는다.
        ReleaseHeld();

        _sequence?.Kill();
        _sequence = null;
        _punchTween?.Kill();
        _punchTween = null;
        transform.localScale = _baseScale;
        _state = State.Ready;
        _reloadRemaining = 0f;
    }

    private void Update()
    {
        if (_state == State.Reloading)
        {
            _reloadRemaining -= Time.deltaTime;
            if (_reloadRemaining <= 0f) _state = State.Ready;
            return;
        }

        // 물고 있던 슬라임이 사라지면(합성, 디스폰) 대포가 영영 잠긴다.
        if (_state == State.Charging && _held == null) Abort();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_state != State.Ready) return;

        SlimeController slime = other.GetComponent<SlimeController>();
        if (slime == null || !slime.IsPlaygroundTarget) return;

        Load(slime);
    }

    private void Load(SlimeController slime)
    {
        _state = State.Charging;
        _held = slime;

        // 콜라이더와 물리를 끈다. 터치도 닿지 않고, 물리가 대포 안 위치를 밀어내지도
        // 않는다. 그림은 그대로 두고 크기만 줄인다.
        //
        // 크기를 읽는 것은 반드시 이 뒤다. 잠금이 돌고 있던 펀치를 정리하고 원래
        // 크기로 되돌려 놓으므로, 앞에서 읽으면 부푼 값을 기본 크기로 기억한다.
        slime.SetPresentationLocked(true);
        slime.transform.DOKill();
        _heldBaseScale = slime.transform.localScale;

        if (AudioManager.Instance != null && _loadSound != null)
        {
            AudioManager.Instance.PlaySFX(_loadSound);
        }

        // 발사 방향을 먼저 정하고, 포신이 그 각도에 멈추도록 돌린다.
        // 3초를 기다리는 동안 어디로 갈지 보이는 편이 낫다.
        float angle = Random.Range(0f, 360f);

        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(slime.transform.DOMove(transform.position, _loadDuration)
            .SetEase(Ease.InQuad));
        _sequence.Join(slime.transform.DOScale(_heldBaseScale * _loadedScale, _loadDuration)
            .SetEase(Ease.InQuad));

        // 다 들어간 순간에 감춘다. 처음부터 끄면 빨려 들어가는 것이 보이지 않는다.
        _sequence.AppendCallback(() => HideHeld(slime));

        if (_barrel != null)
        {
            _barrel.localRotation = Quaternion.identity;
            _sequence.Append(_barrel
                .DOLocalRotate(
                    new Vector3(0f, 0f, angle + 360f * _aimSpins),
                    _chargeDuration,
                    RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic));
        }
        else
        {
            _sequence.AppendInterval(_chargeDuration);
        }

        _sequence.AppendCallback(() => Fire(angle));
    }

    private void Fire(float angle)
    {
        _sequence = null;

        SlimeController slime = _held;
        if (slime == null)
        {
            Abort();
            return;
        }

        RestoreHeld(slime, MuzzlePosition);
        _held = null;

        float radians = angle * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        slime.Launch(direction * _launchSpeed);

        PlayFireFeedback();

        _state = State.Reloading;
        _reloadRemaining = _reloadDuration;
    }

    // 쏘지 못하고 끝날 때. 물고 있던 것이 있으면 제자리에 되돌린다.
    private void Abort()
    {
        ReleaseHeld();
        _sequence?.Kill();
        _sequence = null;
        _state = State.Reloading;
        _reloadRemaining = _reloadDuration;
    }

    private void ReleaseHeld()
    {
        if (_held == null)
        {
            // 물고 있던 슬라임이 사라졌어도 꺼 둔 그림은 되돌려야 한다. 풀에서
            // 같은 오브젝트가 다시 나오므로, 두면 보이지 않는 슬라임이 태어난다.
            ShowHeld();
            return;
        }

        RestoreHeld(_held, _held.transform.position);
        _held = null;
    }

    // 그림과 크기를 먼저 되돌리고 잠금을 푼다. 순서가 바뀌면 작아진 채로 물리가
    // 살아나 한 프레임 동안 다른 크기로 부딪히고, 그림이 꺼진 채로 잠금을 풀면
    // 만질 수 없는 슬라임이 남는다.
    //
    // 되돌리기를 빠뜨리면 안 된다. 보이지 않는 슬라임이 계속 돌아다니고, 위치는
    // 저장하지 않으므로 앱을 껐다 켜야 돌아온다.
    private void RestoreHeld(SlimeController slime, Vector3 position)
    {
        ShowHeld();

        slime.transform.DOKill();
        slime.transform.localScale = _heldBaseScale;
        slime.transform.position = position;
        slime.SetPresentationLocked(false);
    }

    private void HideHeld(SlimeController slime)
    {
        if (slime == null) return;

        slime.GetComponentsInChildren(includeInactive: true, result: _hiddenRenderers);
        for (int i = _hiddenRenderers.Count - 1; i >= 0; --i)
        {
            if (_hiddenRenderers[i].enabled)
            {
                _hiddenRenderers[i].enabled = false;
            }
            else
            {
                // 원래 꺼져 있던 것은 되돌릴 대상이 아니다.
                _hiddenRenderers.RemoveAt(i);
            }
        }
    }

    private void ShowHeld()
    {
        foreach (SpriteRenderer renderer in _hiddenRenderers)
        {
            if (renderer != null) renderer.enabled = true;
        }

        _hiddenRenderers.Clear();
    }

    private void PlayFireFeedback()
    {
        if (AudioManager.Instance != null && _fireSound != null)
        {
            AudioManager.Instance.PlaySFXRandomPitch(_fireSound);
        }

        if (_firePunchScale <= 0f) return;

        _punchTween?.Kill();
        transform.localScale = _baseScale;
        _punchTween = transform
            .DOPunchScale(_baseScale * _firePunchScale, 0.25f)
            .OnComplete(() =>
            {
                _punchTween = null;
                transform.localScale = _baseScale;
            });
    }
}
