using UnityEngine;

public class DamageFloaterFeedback : MonoBehaviour, IFeedback
{
    [SerializeField] private DamageFloater _floaterPrefab;
    [SerializeField] private Vector3 _offset = new Vector3(0, 0.5f, 0);

    public void Play(ClickInfo clickInfo)
    {
        if (_floaterPrefab == null) return;

        Vector3 spawnPos = transform.position + _offset;
        DamageFloater floater = Instantiate(_floaterPrefab, spawnPos, Quaternion.identity);
        floater.Play(clickInfo.Damage, spawnPos);
    }
}
