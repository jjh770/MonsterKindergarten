using Cysharp.Threading.Tasks;
using Firebase;
using System;
using UnityEngine;

public class FirebaseInitializer : MonoBehaviour
{
    public static FirebaseInitializer Instance { get; set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 아래쪽에서 실행할 코드가 없기 때문에 await를 붙이지 않아도 됨.
        // Fire - and - Forget 패턴 : 비동기작업을 시작하기만 하고 결과를 기다리지는 않는다.
        // Forget() -> 이 비동기 처리를 기다리는 다음 작업이 없다.
        InitFirebaseAsync().Forget();
    }

    private async UniTask InitFirebaseAsync()
    {
        var dependencyStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask();
        try
        {
            if (dependencyStatus == DependencyStatus.Available)
            {
                Debug.Log("초기화 성공");
            }

        }
        catch (FirebaseException e)
        {
            Debug.LogError("파이어베이스 초기화 실패" + e.Message);
        }
        catch (Exception e)
        {
            Debug.LogError("초기화 실패" + e.Message);
        }
    }
}
