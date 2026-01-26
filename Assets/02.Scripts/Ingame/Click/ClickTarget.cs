using UnityEngine;

public class ClickTarget : MonoBehaviour, IClickable
{
    [SerializeField] private string _name;

    public bool OnClick()
    {
        Debug.Log($"{_name} : È÷Æ®");
        return true;
    }
}
