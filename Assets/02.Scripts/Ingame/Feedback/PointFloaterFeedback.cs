using UnityEngine;

public class PointFloaterFeedback : MonoBehaviour, IFeedback
{
    public void Play(ClickInfo clickInfo)
    {
        PointFloaterSpawner.Instance.ShowFloater(clickInfo);
    }
}
