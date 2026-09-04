using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class HybridRepository<T> : IRepository<T> where T : class, ISaveData
{
    private readonly IRepository<T> _playerprefsRepository;
    private readonly IRepository<T> _firebaseRepository;
    private const float FIREBASE_INTERVAL = 0.6f;
    public HybridRepository(IRepository<T> playerprefs, IRepository<T> firebase)
    {
        _playerprefsRepository = playerprefs;
        _firebaseRepository = firebase;
    }

    private CancellationTokenSource _firebaseSaveToken;

    public async UniTask Save(T saveData)
    {
        if (GameplaySaveGate.IsResetting) return;
        int resetGeneration = GameplaySaveGate.ResetGeneration;
        // 로컬 저장 - 즉시 수행
        saveData.LastSaveTime = DateTime.UtcNow.ToString("O");
        await _playerprefsRepository.Save(saveData);
        if (GameplaySaveGate.IsResetting ||
            resetGeneration != GameplaySaveGate.ResetGeneration) return;

        // 서버 저장 - 이전 0.6초간 대기 작업이 있다면 취소 요청
        if (_firebaseSaveToken != null)
        {
            _firebaseSaveToken.Cancel();
            _firebaseSaveToken.Dispose();
        }
        // 새로운 취소 토큰 생성
        _firebaseSaveToken = new CancellationTokenSource();
        // 서버 저장 실행
        SaveToFirebase(saveData, _firebaseSaveToken.Token, resetGeneration).Forget();
    }

    private async UniTaskVoid SaveToFirebase(T saveData, CancellationToken token, int resetGeneration)
    {
        try
        {
            // 0.6초간 대기 실행
            await UniTask.Delay(TimeSpan.FromSeconds(FIREBASE_INTERVAL), cancellationToken: token);
            // 취소 요청이 떨어지지 않았다면 넘어가기
            if (token.IsCancellationRequested || GameplaySaveGate.IsResetting ||
                resetGeneration != GameplaySaveGate.ResetGeneration) return;
            // 모든 분기 통과 시 서버에 저장
            await _firebaseRepository.Save(saveData);
        }
        catch (OperationCanceledException)
        {
            // _firebaseSaveToken.Cancel()이 실행되면 여기로 들어옴 (이전 요청 폐기)
        }
        catch (Exception e)
        {
            Debug.LogWarning($"파이어베이스 저장 실패 : {e.Message}");
        }
    }


    public async UniTask<SaveLoadResult<T>> Load()
    {
        var playerprefsTask = _playerprefsRepository.Load();
        var firebaseTask = _firebaseRepository.Load();

        var (playerprefsResult, firebaseResult) = await UniTask.WhenAll(playerprefsTask, firebaseTask);

        return ResolveConflict(playerprefsResult, firebaseResult);
    }

    // 어느 쪽이든 읽지 못했으면 진행하지 않는다.
    //
    // 읽지 못한 저장소에 무엇이 들어 있는지 알 수 없는데 게임을 시작하면,
    // 첫 저장이 확인하지 못한 원본을 덮어써 복구할 수 없게 만든다.
    // 로그인이 이미 네트워크를 요구하므로 클라우드 실패는 드문 상태다.
    private SaveLoadResult<T> ResolveConflict(
        SaveLoadResult<T> playerprefs,
        SaveLoadResult<T> firebase)
    {
        // 로컬에 현재 앱보다 높은 버전이 있으면 앱 업데이트가 먼저다.
        // 클라우드의 낮은 버전을 채택하면 이 기기의 최신 진행도를 되돌린다.
        if (playerprefs.IsFailed &&
            playerprefs.Failure == ESaveLoadFailure.UnsupportedVersion)
        {
            return playerprefs;
        }

        if (firebase.IsFailed)
        {
            return firebase;
        }

        if (playerprefs.IsFailed)
        {
            // 로컬이 손상돼도 클라우드를 읽었다면 그것으로 복구한다.
            if (firebase.IsLoaded)
            {
                return AdoptFirebase(firebase);
            }

            // 클라우드가 비어 있으면 복구할 원본이 없다. 새 게임으로 덮지 않는다.
            return playerprefs;
        }

        if (playerprefs.IsNotFound && firebase.IsNotFound)
        {
            return SaveLoadResult<T>.NotFound();
        }

        if (playerprefs.IsNotFound)
        {
            return AdoptFirebase(firebase);
        }

        if (firebase.IsNotFound)
        {
            return playerprefs;
        }

        // LastSaveTime이 null이거나 파싱 실패 시 DateTime.MinValue 사용
        DateTime playerprefsTime = ParseSaveTime(playerprefs.Data.LastSaveTime);
        DateTime firebaseTime = ParseSaveTime(firebase.Data.LastSaveTime);

        if (playerprefsTime > firebaseTime)
        {
            return playerprefs;
        }

        return AdoptFirebase(firebase);
    }

    // 클라우드를 채택하면 로컬 사본도 같은 내용으로 맞춘다.
    private SaveLoadResult<T> AdoptFirebase(SaveLoadResult<T> firebase)
    {
        _playerprefsRepository.Save(firebase.Data).Forget();
        return firebase;
    }

    private DateTime ParseSaveTime(string saveTime)
    {
        if (string.IsNullOrEmpty(saveTime))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(saveTime, out DateTime result))
        {
            return result;
        }

        return DateTime.MinValue;
    }
}
