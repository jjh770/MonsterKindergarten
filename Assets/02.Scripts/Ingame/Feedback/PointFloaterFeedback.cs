using UnityEngine;

public class PointFloaterFeedback : MonoBehaviour, IFeedback
{
    [SerializeField] private Vector3 _offset = new Vector3(0, 0.5f, 0);

    public void Play(ClickInfo clickInfo)
    {
        if (PointFloaterPool.Instance == null) return;

        Vector3 spawnPos = transform.position + _offset;
        PointFloater floater = PointFloaterPool.Instance.Spawn(spawnPos);
        floater.Play(clickInfo.Point, spawnPos);
    }
}
