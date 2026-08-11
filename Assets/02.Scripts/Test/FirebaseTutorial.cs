#if !UNITY_WEBGL || UNITY_EDITOR

using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirebaseTutorial : MonoBehaviour
{
    // _app : 파이어베이스에 접근할 수 있는 접근자
    private FirebaseApp _app = null;
    private FirebaseAuth _auth = null;
    private FirebaseFirestore _db = null;

    [SerializeField] private TextMeshProUGUI _progressText;
    private void Start()
    {
        // 과제.
        // 이 씬이 시작되면
        // 1. 파이어베이스 초기화
        // 2. 로그아웃
        // 3. 재로그인
        // 4. 강아지 추가 (전제조건 : 파이어베이스 규칙에 로그인한 사람만 글 쓰기 가능)
        //  ㄴ 위 내용이 에러 없이 잘 이어지게 수행
        //_ = AssingmentAsync();
    }

    private async UniTask AssingmentAsync()
    {

        await InitFirebaseAsync();
        _progressText.text = "파이어베이스 초기화";

        await LogoutAsync();

        await LoginAsync("1q@1q.1q", "1q1q1q1q");
        _progressText.text = "재로그인";

        await SaveDogAsync();
        _progressText.text = "강아지 추가";
    }

    private async UniTask InitFirebaseAsync()
    {
        try
        {
            var dependencyStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask();
            if (dependencyStatus == DependencyStatus.Available)
            {
                // 1. 파이어베이스 연결에 성공했다면
                _app = FirebaseApp.DefaultInstance;      // 파이어베이스 앱   모듈 가져오기
                _auth = FirebaseAuth.DefaultInstance;    // 파이어베이스 인증 모듈 가져오기
                _db = FirebaseFirestore.DefaultInstance; // 파이어베이스 DB   모듈 가져오기
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

    private void InitFirebase()
    {
        // CheckAndFixDependenciesAsync : 잘 체크 되었는가? 의존성이 잘 매칭 되어있는가? (Async : 비동기 처리)
        // ContinueWithOnMainThread : 콜백 함수 -> 특정 이벤트가 발생하고 나면 자동으로 호출되는 함수

        // Task : 비동기에 대한 진행사항과 완료됐을 때 결과값을 가지고 있는 객체
        // Task란 C# 비동기 작업을 의미한다.
        // 결과값을 Result로 가지고 있고
        // 진행사항 및 에러값을 또 가지고 있음.
        // DependencyStatus status = Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        // 이 작업은 유니티가 실행중 CPU 1에게 작업ㅇ르 시킬 수도 있고 CPU 2에게 작업을 시킬수도 았음.
        // 작업이 완료되고 나서 유니티가 실행중인 CPU1에서 작업을 이어가는게 아니라 CPU 2에서 Monobehaviour 작업을 이어나가려하면 유니티를 모르기 때문에 뻗을 수 있음.
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                // 1. 파이어베이스 연결에 성공했다면
                _app = FirebaseApp.DefaultInstance;      // 파이어베이스 앱   모듈 가져오기
                _auth = FirebaseAuth.DefaultInstance;    // 파이어베이스 인증 모듈 가져오기
                _db = FirebaseFirestore.DefaultInstance; // 파이어베이스 DB   모듈 가져오기
                Debug.Log("초기화 성공");
            }
            else
            {
                Debug.LogError("초기화 실패" + task.Result);
            }
        });
    }
    private void Register(string email, string password)
    {
        // 이메일과 패스워드로 회원가입을 시도
        _auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("회원가입이 취소 되었습니다.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("회원가입이 실패했습니다. " + task.Exception);
                return;
            }

            // Firebase user has been created.
            Firebase.Auth.AuthResult result = task.Result;
            Debug.LogFormat("회원가입에 성공했습니다. : {0} ({1})", result.User.DisplayName, result.User.UserId);
        });
    }

    private async UniTask LoginAsync(string email, string password)
    {
        try
        {
            var LoginStatus = await _auth.SignInWithEmailAndPasswordAsync(email, password).AsUniTask();

            // 로그인에 성공하면 반환되는 결과값의 유저와 auth 모듈의 CureentUser가 둘 다 로그인한 유저로 같다. (resultUser == user)
            FirebaseUser resultUser = LoginStatus.User;
            FirebaseUser user = _auth.CurrentUser;

            Firebase.Auth.AuthResult result = LoginStatus;
            Debug.LogFormat("로그인에 성공했습니다.: {0} ({1})", result.User.Email, result.User.UserId);
        }
        catch (FirebaseException e)
        {
            Debug.LogError("파이어베이스 로그인이 취소되었습니다." + e.Message);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("로그인이 실패했습니다. " + ex.Message);
        }
    }

    private void Login(string email, string password)
    {
        _auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("로그인이 취소되었습니다.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("로그인이 실패했습니다. " + task.Exception);
                return;
            }

            Firebase.Auth.AuthResult result = task.Result;

            // 로그인에 성공하면 반환되는 결과값의 유저와 auth 모듈의 CureentUser가 둘 다 로그인한 유저로 같다. (resultUser == user)
            FirebaseUser resultUser = task.Result.User;
            FirebaseUser user = _auth.CurrentUser;
            Debug.LogFormat("로그인에 성공했습니다.: {0} ({1})", result.User.Email, result.User.UserId);
        });
    }

    private async UniTask LogoutAsync()
    {
        _auth.SignOut();
        await UniTask.Yield();
        _progressText.text = "로그아웃.";
    }

    private void Logout()
    {
        _auth.SignOut();
        Debug.Log("로그아웃 성공");
    }

    private void CheckLoginStatus()
    {
        FirebaseUser user = _auth.CurrentUser;

        if (user == null)
        {
            Debug.Log("로그인 안됨");
        }
        else
        {
            Debug.LogFormat("로그인 중: {0} ({1})", user.Email, user.UserId);
        }
    }

    private async UniTask SaveDogAsync()
    {
        Dog dog = new Dog("별이", 3);

        try
        {
            var SaveStatus = await _db.Collection("Dogs").AddAsync(dog).AsUniTask();

            Debug.Log("저장 성공");
        }
        catch (FirebaseException e)
        {
            Debug.Log("파이어베이스 저장 실패했습니다." + e.Message);
        }
        catch (System.Exception ex)
        {
            Debug.Log("저장 실패했습니다." + ex.Message);
        }
    }

    private void SaveDog()
    {
        // 새로운 데이터 저장
        Dog dog = new Dog("콩이", 3);

        // Add vs Set
        // Add : 추가한다.
        // Set : 이미 아이디의 문서가 있다면 수정하고 없으면 추가한다.

        _db.Collection("Dogs").AddAsync(dog).ContinueWithOnMainThread(task =>
        //_db.Collection("Dogs").Document("가족").SetAsync(dog).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.Log("저장 실패" + task.Exception);
                return;
            }

            else if (task.IsCompletedSuccessfully)
            {
                Debug.Log("저장 성공");
            }
        });
    }


    private void LoadMyDog()
    {
        _db.Collection("Dogs").Document("가족").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.Log("로드 실패" + task.Exception);
                return;
            }

            else if (task.IsCompletedSuccessfully)
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    Dog myDog = snapshot.ConvertTo<Dog>();
                    Debug.Log($"{myDog.Name}({myDog.Age})");
                }
            }
        });
    }


    private void LoadDogs()
    {
        _db.Collection("Dogs").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                var snapshots = task.Result;
                foreach (DocumentSnapshot documentSnapshot in snapshots.Documents)
                {
                    Dog myDog = documentSnapshot.ConvertTo<Dog>();
                    Debug.Log($"{myDog.Name}({myDog.Age})");
                }
                Debug.Log("불러오기 성공!");
            }
            else
            {
                Debug.LogError("불러오기 실패!");
            }
        });
    }

    private void DeleteDogs()
    {
        // 목표 : 특정 이름을 가진 데이터 삭제 -> 필터를 거쳐 데이터를 정제할 수 있음 (모든 데이터를 다 가져와서 찾으면 시간, 비용이 매우 크기 때문)
        _db.Collection("Dogs").WhereEqualTo("Name", "콩이").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                var snapshots = task.Result;
                foreach (DocumentSnapshot documentSnapshot in snapshots.Documents)
                {
                    Dog myDog = documentSnapshot.ConvertTo<Dog>();
                    if (myDog.Name == "콩이")
                    {
                        // Document에 고유 Id 값을 알아와야함. -> Dog.cs에 [FirestoreDocumentId] 를 붙인 Id 프로퍼티를 만들면 고유 식별자가 붙게 됨. 
                        _db.Collection("Dogs").Document(myDog.Id).DeleteAsync().ContinueWithOnMainThread(task =>
                        {
                            if (task.IsCompletedSuccessfully)
                            {
                                Debug.Log("데이터가 삭제됐습니다");
                            }
                        });
                    }
                }
                Debug.Log("삭제 성공!");
            }
            else
            {
                Debug.LogError("삭제 실패!" + task.Exception);
            }
        });
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit9Key.wasPressedThisFrame)
        {
            _ = AssingmentAsync();
        }
        if (_app == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            Register("1q@1q.1q", "1q1q1q1q");
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            Login("1q@1q.1q", "1q1q1q1q");
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            Login("1q@1q.1q", "1q1q1q1");
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            Logout();
        }
        else if (keyboard.digit5Key.wasPressedThisFrame)
        {
            CheckLoginStatus();
        }
        else if (keyboard.digit6Key.wasPressedThisFrame)
        {
            SaveDog();
        }
        else if (keyboard.digit7Key.wasPressedThisFrame)
        {
            LoadMyDog();
        }
        else if (keyboard.digit8Key.wasPressedThisFrame)
        {
            LoadDogs();
        }

    }
}
#endif
