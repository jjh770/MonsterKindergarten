using UnityEngine;

public sealed class MergeCandidateVfxController : MonoBehaviour
{
    private const int GradesPerTier = 5;

    [SerializeField] private GameObject _fallbackEffectPrefab;
    [Tooltip("Element 0: Grade 1-5, Element 1: Grade 6-10, ...")]
    [SerializeField] private GameObject[] _effectPrefabsByTier;
    [SerializeField] private Vector3 _localOffset;
    [SerializeField] private Vector3 _localEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField, Min(0f)] private float _localScale = 0.2f;
    [SerializeField] private int _sortingOrderOffset = 1;

    private SlimeController _target;
    private SpriteRenderer _targetRenderer;
    private Clicker _clicker;
    private GameObject _effectInstance;
    private GameObject _activeEffectPrefab;

    private void Awake()
    {
        _clicker = GetComponent<Clicker>();
    }

    private void OnEnable()
    {
        if (_clicker != null)
        {
            _clicker.MergeCandidateChanged += Show;
        }
    }

    private void LateUpdate()
    {
        if (_target == null || _effectInstance == null || !_effectInstance.activeSelf) return;

        if (!_target.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        FollowTarget();
    }

    private void OnDisable()
    {
        if (_clicker != null)
        {
            _clicker.MergeCandidateChanged -= Show;
        }

        Hide();
    }

    public void Show(SlimeController target)
    {
        if (target == null)
        {
            Hide();
            return;
        }

        _target = target;
        _targetRenderer = target.GetComponent<SpriteRenderer>();

        EnsureEffectInstance(target.Grade);
        if (_effectInstance == null) return;

        FollowTarget();
        ApplySorting();
        _effectInstance.SetActive(true);

        foreach (ParticleSystem particleSystem in
                 _effectInstance.GetComponentsInChildren<ParticleSystem>())
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    public void Hide()
    {
        StopEffect();
        _target = null;
        _targetRenderer = null;
    }

    private void EnsureEffectInstance(ESlimeGrade grade)
    {
        GameObject selectedEffectPrefab = GetEffectPrefab(grade);
        if (_effectInstance != null && _activeEffectPrefab == selectedEffectPrefab) return;

        ReleaseEffectInstance();
        if (selectedEffectPrefab == null) return;

        _activeEffectPrefab = selectedEffectPrefab;
        _effectInstance = Instantiate(selectedEffectPrefab, transform);
        _effectInstance.name = $"{selectedEffectPrefab.name} (Merge Candidate)";
        _effectInstance.transform.localScale = Vector3.one * _localScale;
        _effectInstance.SetActive(false);
    }

    private GameObject GetEffectPrefab(ESlimeGrade grade)
    {
        int gradeValue = (int)grade;
        if (gradeValue <= 0) return _fallbackEffectPrefab;

        int tierIndex = (gradeValue - 1) / GradesPerTier;
        if (_effectPrefabsByTier == null ||
            tierIndex < 0 ||
            tierIndex >= _effectPrefabsByTier.Length ||
            _effectPrefabsByTier[tierIndex] == null)
        {
            return _fallbackEffectPrefab;
        }

        return _effectPrefabsByTier[tierIndex];
    }

    private void FollowTarget()
    {
        if (_target == null || _effectInstance == null) return;

        _effectInstance.transform.position = _target.transform.TransformPoint(_localOffset);
        _effectInstance.transform.rotation = Quaternion.Euler(_localEulerAngles);
    }

    private void ApplySorting()
    {
        if (_targetRenderer == null || _effectInstance == null) return;

        foreach (Renderer effectRenderer in
                 _effectInstance.GetComponentsInChildren<Renderer>(true))
        {
            effectRenderer.sortingLayerID = _targetRenderer.sortingLayerID;
            effectRenderer.sortingOrder = _targetRenderer.sortingOrder + _sortingOrderOffset;
        }
    }

    private void StopEffect()
    {
        if (_effectInstance == null) return;

        foreach (ParticleSystem particleSystem in
                 _effectInstance.GetComponentsInChildren<ParticleSystem>())
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        _effectInstance.SetActive(false);
    }

    private void ReleaseEffectInstance()
    {
        if (_effectInstance != null)
        {
            StopEffect();
            Destroy(_effectInstance);
        }

        _effectInstance = null;
        _activeEffectPrefab = null;
    }
}
