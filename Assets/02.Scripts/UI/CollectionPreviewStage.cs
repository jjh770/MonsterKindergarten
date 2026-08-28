using UnityEngine;

// 도감 상세에 보여줄 슬라임 미리보기 두 마리와 각각을 찍는 카메라 두 대.
//
// 애니메이션 클립이 SpriteRenderer.m_Sprite에 묶여 있어 UI의 Image로는 재생할 수
// 없다. 그래서 화면 밖 월드에 스프라이트를 두고 전용 카메라로 RenderTexture에
// 그린 뒤 도감에서 RawImage로 받는다. 클립과 오버라이드 컨트롤러를 그대로 쓴다.
//
// 기본 모션과 이동 모션을 각각 다른 칸에 보여주므로 카메라와 RenderTexture를
// 따로 둔다. 두 슬라임은 서로의 시야에 들어오지 않도록 떨어뜨려 배치한다.
//
// 본체 Slime 프리팹은 쓰지 않는다. 물리·입력·피드백과 SlimeManager 바인딩까지
// 딸려 오는데 미리보기에는 스프라이트와 애니메이터만 있으면 된다.
public sealed class CollectionPreviewStage : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera _idleCamera;
    [SerializeField] private SpriteRenderer _idleRenderer;
    [SerializeField] private Animator _idleAnimator;
    [SerializeField] private Camera _movingCamera;
    [SerializeField] private SpriteRenderer _movingRenderer;
    [SerializeField] private Animator _movingAnimator;

    // Slime 프리팹의 SlimeAnimator와 같은 순서로 등급별 컨트롤러를 넣는다.
    [SerializeField] private AnimatorOverrideController[] _levelAnimators;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsDragging = Animator.StringToHash("IsDragging");

    private bool _isInitialized;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _isInitialized = true;
        SetVisible(false);
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _idleCamera != null &&
                             _idleRenderer != null &&
                             _idleAnimator != null &&
                             _movingCamera != null &&
                             _movingRenderer != null &&
                             _movingAnimator != null &&
                             _levelAnimators != null &&
                             _levelAnimators.Length > 0;
        if (!hasReferences)
        {
            Debug.LogError("도감 미리보기 무대의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }

    // 도감을 닫으면 카메라를 끈다. 평상시 렌더 비용을 남기지 않는다.
    public void SetVisible(bool isVisible)
    {
        if (!_isInitialized) return;

        _idleCamera.enabled = isVisible;
        _movingCamera.enabled = isVisible;
        _idleRenderer.gameObject.SetActive(isVisible);
        _movingRenderer.gameObject.SetActive(isVisible);
    }

    // 등록 전에는 실루엣만 보여준다.
    public void Show(SlimeSpecData specData, bool isRegistered)
    {
        if (!_isInitialized) return;

        // Animator 파라미터는 오브젝트가 꺼져 있으면 조용히 버려진다.
        // 호출 순서에 기대지 않도록 여기서 먼저 켠다.
        _idleRenderer.gameObject.SetActive(true);
        _movingRenderer.gameObject.SetActive(true);

        Color tint = isRegistered
            ? Color.white
            : new Color(0.08f, 0.08f, 0.1f, 0.92f);

        Apply(_idleRenderer, _idleAnimator, specData, tint, isMoving: false);
        Apply(_movingRenderer, _movingAnimator, specData, tint, isMoving: true);
    }

    // 페이지를 넘겨 선택이 풀렸을 때 두 칸 모두 같은 기본 그림으로 되돌린다.
    public void ShowPlaceholder(Sprite sprite)
    {
        if (!_isInitialized) return;

        SetVisible(true);
        ApplyPlaceholder(_idleRenderer, _idleAnimator, sprite);
        ApplyPlaceholder(_movingRenderer, _movingAnimator, sprite);
    }

    private static void ApplyPlaceholder(
        SpriteRenderer targetRenderer,
        Animator targetAnimator,
        Sprite sprite)
    {
        targetAnimator.enabled = false;
        targetRenderer.color = Color.white;
        targetRenderer.sprite = sprite;
    }

    private void Apply(
        SpriteRenderer targetRenderer,
        Animator targetAnimator,
        SlimeSpecData specData,
        Color tint,
        bool isMoving)
    {
        targetRenderer.color = tint;
        targetRenderer.sprite = specData?.Sprite;

        if (specData == null)
        {
            targetAnimator.enabled = false;
            return;
        }

        // SlimeAnimator와 같은 규칙이다. 전용 애니메이션이 없는 등급은
        // 애니메이터를 끄고 등록된 정지 스프라이트를 그대로 둔다.
        int index = Mathf.Clamp(
            (int)specData.Grade - 1,
            0,
            _levelAnimators.Length - 1);
        AnimatorOverrideController controller = _levelAnimators[index];
        bool usesBaseAnimation = index == 0;
        if (controller == null ||
            (!usesBaseAnimation && !HasCustomAnimation(controller)))
        {
            targetAnimator.enabled = false;
            return;
        }

        targetAnimator.runtimeAnimatorController = controller;
        targetAnimator.enabled = true;
        targetAnimator.SetBool(IsMoving, isMoving);
        targetAnimator.SetBool(IsDragging, false);
    }

    private static bool HasCustomAnimation(AnimatorOverrideController controller)
    {
        var overrides =
            new System.Collections.Generic.List<
                System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>(
                controller.overridesCount);
        controller.GetOverrides(overrides);

        foreach (var pair in overrides)
        {
            if (pair.Value != null && pair.Value != pair.Key)
            {
                return true;
            }
        }

        return false;
    }

}
