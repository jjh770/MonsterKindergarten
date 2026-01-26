using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int AutoPoint;
    public int ManualPoint;

    private int _point;
    public int Point
    {
        get => _point;
        set
        {
            _point = value;
            OnPointChanged?.Invoke(_point);
        }
    }

    public event Action<int> OnPointChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
