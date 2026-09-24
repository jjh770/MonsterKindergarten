using DG.Tweening;
using UnityEngine;

// 장식장 놀이터의 범퍼. 닿은 슬라임을 중심에서 바깥으로 밀어낸다.
//
// 콜라이더를 트리거로 쓴다. 단단한 콜라이더로 두면 슬라임의 물리 머티리얼(탄성 1)이
// 먼저 튕긴 위에 이쪽 힘이 얹혀, 한 번의 충돌을 두 규칙이 나눠 정하게 된다.
// 그러면 인스펙터에 적은 세기와 눈에 보이는 속도가 서로 달라져 조정할 수가 없다.
// 트리거로 두면 적은 값이 그대로 출발 속도가 된다.
// RequireComponent은 쓰지 않는다. Collider2D는 추상 타입이라 Unity가 대신 붙이지
// 못하고, 모양은 놓는 사람이 정하는 편이 낫다. 대신 Awake에서 확인한다.
public class DisplayRoomBumper : MonoBehaviour
{
    [Tooltip("밀어낼 때의 속도입니다. 슬라임의 평소 배회 속도는 1입니다.")]
    [SerializeField, Min(0.1f)] private float _launchSpeed = 8f;

    [Tooltip("슬라임이 정확히 중심에 겹쳤을 때 밀어낼 방향입니다.")]
    [SerializeField] private Vector2 _fallbackDirection = Vector2.up;

    [Header("Feedback")]
    [SerializeField] private float _punchScale = 0.25f;
    [SerializeField, Min(0.05f)] private float _punchDuration = 0.2f;
    [SerializeField] private AudioClip _bumpSound;

    private Collider2D _collider;
    private Tween _punchTween;
    private Vector3 _baseScale;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _baseScale = transform.localScale;

        if (_collider == null)
        {
            Debug.LogError("범퍼에 Collider2D가 없습니다. 원형 콜라이더를 붙이세요.", this);
            enabled = false;
            return;
        }

        if (!_collider.isTrigger)
        {
            Debug.LogError(
                "범퍼의 콜라이더는 트리거여야 합니다. 단단한 콜라이더면 물리가 먼저 " +
                "튕겨 인스펙터의 세기가 실제 속도와 달라집니다.",
                this);
            enabled = false;
            return;
        }

        // Clicker.TrySelect는 레이어 마스크 없이 Physics2D.Raycast로 맨 앞의 것 하나만
        // 집는다. 범퍼가 슬라임보다 앞에 걸리면 그 탭은 아무것도 고르지 못하고 사라져,
        // 장식장에서 슬라임을 눌러도 관찰이 열리지 않는다. 가챠권을 월드 콜라이더로
        // 줍지 않는 이유와 같은 함정이다.
        //
        // 기본 레이캐스트 대상에서 빠진 레이어(Ignore Raycast)에 두면 충돌은 그대로
        // 하면서 탭만 통과시킨다.
        if ((Physics2D.DefaultRaycastLayers & (1 << gameObject.layer)) != 0)
        {
            Debug.LogWarning(
                "범퍼가 기본 레이캐스트 대상 레이어에 있습니다. 슬라임을 누르는 탭을 " +
                "가로채 관찰이 열리지 않을 수 있습니다. Ignore Raycast 레이어로 옮기세요.",
                this);
        }
    }

    private void OnDisable()
    {
        // 공간이 바뀌면 통째로 꺼진다. 연출 도중이면 크기가 커진 채로 남는다.
        _punchTween?.Kill();
        _punchTween = null;
        transform.localScale = _baseScale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        SlimeController slime = other.GetComponent<SlimeController>();
        if (slime == null) return;

        Vector2 outward = (Vector2)(slime.transform.position - transform.position);
        outward = outward.sqrMagnitude > 0.0001f
            ? outward.normalized
            : _fallbackDirection.normalized;
        if (outward == Vector2.zero) return;

        // 이미 바깥으로 나가는 중이면 건드리지 않는다. 가장자리를 스치고 지나가는
        // 슬라임을 안쪽으로 다시 끌어들이지 않기 위해서다.
        Rigidbody2D body = other.attachedRigidbody;
        if (body != null && Vector2.Dot(body.linearVelocity, outward) > 0f) return;

        // 장식장 소속인지, 잡혀 있거나 관찰 중은 아닌지는 SlimeController가 판단한다.
        // 밀지 못했으면 연출도 내지 않는다.
        if (!slime.Launch(outward * _launchSpeed)) return;

        PlayBumpFeedback();
    }

    private void PlayBumpFeedback()
    {
        if (AudioManager.Instance != null && _bumpSound != null)
        {
            // 여러 마리가 잇따라 맞으면 같은 소리가 겹쳐 기계처럼 들린다.
            AudioManager.Instance.PlaySFXRandomPitch(_bumpSound);
        }

        if (_punchScale <= 0f) return;

        // 연달아 맞을 때 펀치가 겹치면 크기가 제자리로 돌아오지 못한다.
        _punchTween?.Kill();
        transform.localScale = _baseScale;
        _punchTween = transform
            .DOPunchScale(_baseScale * _punchScale, _punchDuration)
            .OnComplete(() =>
            {
                _punchTween = null;
                transform.localScale = _baseScale;
            });
    }
}
