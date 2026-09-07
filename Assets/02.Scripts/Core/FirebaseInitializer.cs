
using Cysharp.Threading.Tasks;
using Firebase;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class FirebaseInitializer : MonoBehaviour
{
    public static FirebaseInitializer Instance { get; private set; }

    private Task<bool> _initializationTask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _initializationTask = InitFirebaseAsync();
    }

    public UniTask<bool> WaitForInitializationAsync()
    {
        return _initializationTask.AsUniTask();
    }

    private static async Task<bool> InitFirebaseAsync()
    {
        try
        {
            DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"Firebase 초기화 실패: {dependencyStatus}");
                return false;
            }

            return true;
        }
        catch (FirebaseException e)
        {
            Debug.LogError("파이어베이스 초기화 실패" + e.Message);
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError("초기화 실패" + e.Message);
            return false;
        }
    }
}
