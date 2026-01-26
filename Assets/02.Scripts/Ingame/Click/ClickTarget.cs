using UnityEngine;

public class ClickTarget : MonoBehaviour, IClickable
{
    [SerializeField] private string _name;

    public bool OnClick(ClickInfo clickInfo)
    {
        Debug.Log($"{_name} : 히트");
        return true;
    }
}
