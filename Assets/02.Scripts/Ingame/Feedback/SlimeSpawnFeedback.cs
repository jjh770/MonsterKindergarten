using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(SlimeController))]
[DisallowMultipleComponent]
public sealed class SlimeSpawnFeedback : MonoBehaviour
{
    [SerializeField] private float _dropHeight = 8f;
    [SerializeField, Min(0f)] private float _dropDuration = 0.5f;
    [SerializeField] private Ease _dropEase = Ease.OutBounce;

    private SlimeController _slimeController;
    private Tween _dropTween;

    private void Awake()
    {
        _slimeController = GetComponent<SlimeController>();
    }

    private void OnEnable()
    {
        _slimeController.OnSpawned += Play;
    }

    private void OnDisable()
    {
        if (_slimeController != null)
        {
            _slimeController.OnSpawned -= Play;
        }

        CleanupTween();
    }

    private void OnDestroy()
    {
        CleanupTween();
    }

    private void Play()
    {
        CleanupTween();

        float targetY = transform.position.y;
        transform.position += Vector3.up * _dropHeight;
        _dropTween = transform
            .DOMoveY(targetY, _dropDuration)
            .SetEase(_dropEase)
            .OnComplete(() => _dropTween = null);
    }

    private void CleanupTween()
    {
        _dropTween?.Kill();
        _dropTween = null;
    }
}
