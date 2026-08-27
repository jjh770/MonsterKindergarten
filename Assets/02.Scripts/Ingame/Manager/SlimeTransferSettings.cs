using System;
using DG.Tweening;
using UnityEngine;

// 슬라임 한 마리가 화면 밖으로 빠져나가는 연출 값.
// 카메라 전환 값(_cameraTransitionDuration, _cameraTravelDistance)과는 별개다.
[Serializable]
public sealed class SlimeTransferSettings
{
    [Tooltip("화면 밖으로 나가기 위한 이동 거리. 세로 이동은 카메라 세로 반경보다 커야 한다.")]
    [Min(0f)] public float Distance = 6f;

    [Min(0f)] public float Duration = 0.5f;

    public Ease Ease = Ease.InQuad;
}
