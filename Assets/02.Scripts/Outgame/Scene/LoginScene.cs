using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginScene : MonoBehaviour
{
    // 로그인을 기다리는 동안 버튼에 띄우는 문구.
    // 버튼은 화면 전체를 덮으므로 "어디를 눌러도 된다"를 문구가 대신 말한다.
    private const string IdleLabel = "눌러서 다음으로";

    // 게임 씬에서 저장 데이터를 읽지 못해 돌아왔을 때 안내할 원인.
    // 문구는 UI 경계인 이곳에서 만든다.
    public static ESaveLoadFailure PendingLoadFailure { get; set; }

    [SerializeField] private Button _loginButton;

    [Tooltip("저장 데이터를 읽지 못해 게임에 들어갈 수 없을 때만 여는 확인 패널입니다.")]
    [SerializeField] private SaveRecoveryUI _recoveryUI;

    [Tooltip("씬 전환 때 화면을 덮는 커튼입니다. 비워 두면 즉시 전환합니다.")]
    [SerializeField] private FadeCurtainUI _curtain;

    private string _popupText;
    private TMP_Text _loginButtonText;
    private bool _isLoggingIn;

    private void Start()
    {
        GameplaySaveGate.EndReset();
        // 덮인 채로 시작해 GameScene에서 돌아올 때의 하드 컷을 가린다.
        if (_curtain != null) _curtain.RevealAsync().Forget();
        // 게임 세션이 없는 이 화면에서만 저장 데이터 잠금을 푼다.
        SaveDataLoadGuard.Clear();
        CloudSaveGuard.Clear();
        // 계정이 바뀌면 이전 서버 시각 보정값은 의미가 없다.
        ServerClock.Clear();
        _loginButtonText = _loginButton.GetComponentInChildren<TMP_Text>(true);
        _loginButtonText.text = IdleLabel;
        _loginButton.onClick.AddListener(() => Login(true).Forget());

        if (_recoveryUI != null)
        {
            _recoveryUI.ConfirmRequested += OnRecoveryConfirmed;
        }

        ESaveLoadFailure loadFailure = PendingLoadFailure;
        PendingLoadFailure = ESaveLoadFailure.None;
        if (loadFailure != ESaveLoadFailure.None)
        {
            // 복구 패널이 더 자세히 안내하므로 그때는 토스트를 겹치지 않는다.
            if (!TryOfferRecovery(loadFailure))
            {
                _popupText = BuildLoadFailureMessage(loadFailure);
                ShowLobbyPopup();
            }

            return;
        }

        // 자동 로그인은 하지 않는다. 화면을 덮은 버튼을 눌러야 시작한다.
        //
        // 로그인 전에는 설정을 열어 계정을 확인할 수 있어야 하는데, 자동으로
        // 진행하면 그 틈이 없다. 실패 안내와 복구 패널도 눌러 보기 전에 지나간다.
    }

    private void OnDestroy()
    {
        if (_recoveryUI != null)
        {
            _recoveryUI.ConfirmRequested -= OnRecoveryConfirmed;
        }
    }

    private void OnRecoveryConfirmed()
    {
        Recover().Forget();
    }

    // 초기화로 복구할 수 있는 실패에만 수단을 연다.
    //
    // 네트워크 실패는 재시도가 답이고, 상위 저장 버전은 앱 업데이트가 답이다.
    // 그 두 경우에 초기화를 열어주면 멀쩡한 진행도를 지우게 된다.
    private bool TryOfferRecovery(ESaveLoadFailure failure)
    {
        if (failure != ESaveLoadFailure.Unreadable) return false;

        if (_recoveryUI == null || !_recoveryUI.IsReady)
        {
            Debug.LogError("복구 확인 패널이 없어 초기화 수단을 열 수 없습니다.", this);
            return false;
        }

        _recoveryUI.Show(
            "저장된 진행도가 손상돼 게임을 시작할 수 없어요.\n\n" +
            "초기화하면 재화, 슬라임, 업그레이드, 도감과\n튜토리얼 기록이 모두 지워지고 처음부터 시작합니다.\n" +
            "지워진 진행도는 복구할 수 없어요.\n\n" +
            "계정과 음량 설정은 유지됩니다.");
        return true;
    }

    // 게임 씬에서 돌아올 때 로그아웃되므로 UID가 없다. 로그인으로 소유자를 확인한 뒤
    // 초기화한다.
    //
    // useManualSignIn은 콜드 스타트에서만 실제로 창을 띄운다. PGS v2는 프로세스가
    // 이미 인증돼 있으면 Java 쪽 signIn()을 부르지 않고 바로 성공을 돌려주므로,
    // 게임 씬을 거쳐 온 세션에서는 계정 선택 화면이 나오지 않는다. Firebase 쪽
    // 로그아웃은 플러그인의 인증 상태를 건드리지 않는다.
    //
    // 그래도 위험하지는 않다. 이 플러그인에는 다른 계정으로 갈아탈 수단이 없어,
    // 선택 화면이 떠도 고를 수 있는 계정은 하나뿐이다. 되돌릴 수 없는 동작에 대한
    // 확인은 SaveRecoveryUI 패널이 맡는다.
    private async UniTask Recover()
    {
        if (_isLoggingIn) return;

        _isLoggingIn = true;
        SetRecoveryInteractable(false);
        _recoveryUI.SetMessage("계정을 확인하고 있어요...");

        AccountResult result = await AccountManager.Instance.TryLogin(useManualSignIn: true);
        if (!result.Success)
        {
            _recoveryUI.SetMessage($"로그인하지 못했어요.\n{result.ErrorMessage}");
            _isLoggingIn = false;
            SetRecoveryInteractable(true);
            return;
        }

        _recoveryUI.SetMessage("진행도를 초기화하고 있어요.\n인터넷 연결을 유지해 주세요.");
        try
        {
            await GameDataResetService.ResetAsync(result.UserId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"손상된 진행도를 초기화하지 못했습니다: {e.Message}");
            _recoveryUI.SetMessage(
                "초기화를 완료하지 못했어요.\n인터넷 연결을 확인하고 다시 시도해 주세요.");
            _isLoggingIn = false;
            SetRecoveryInteractable(true);
            return;
        }

        GameplaySaveGate.EndReset();
        if (!await TrySyncServerClock(result.UserId))
        {
            _recoveryUI.SetMessage(
                "서버 시각을 확인하지 못했어요.\n인터넷 연결을 확인하고 다시 시도해 주세요.");
            _isLoggingIn = false;
            SetRecoveryInteractable(true);
            return;
        }

        await EnterGameScene();
    }

    // 오프라인 보상이 경과 시간으로 정해지므로, 게임에 들어가기 전에 서버 시각을
    // 확인해 둔다. 확인하지 못한 채로 들어가면 기기 시계를 돌린 만큼 그대로 준다.
    // 콜드 스타트는 어차피 로그인에 네트워크가 필요해 새 실패 지점이 아니다.
    private async UniTask<bool> TrySyncServerClock(string userId)
    {
        if (await ServerClock.TrySync(userId)) return true;

        _popupText = "서버 시각을 확인하지 못했어요.\n인터넷 연결을 확인하고\n다시 시도해 주세요.";
        ShowLobbyPopup();
        return false;
    }

    private void SetRecoveryInteractable(bool isInteractable)
    {
        _recoveryUI.SetInteractable(isInteractable);
        _loginButton.interactable = isInteractable;
    }

    private static string BuildLoadFailureMessage(ESaveLoadFailure failure)
    {
        switch (failure)
        {
            case ESaveLoadFailure.UnsupportedVersion:
                return "저장된 진행도가 현재 앱 버전보다 최신이에요.\n스토어에서 앱을 업데이트한 뒤\n다시 로그인해 주세요.";
            case ESaveLoadFailure.Unreadable:
                return "저장된 진행도를 읽지 못했어요.\n다시 로그인해 주세요.\n\n진행도를 덮어쓰지 않으려고\n게임을 시작하지 않았어요.";
            default:
                return "저장된 진행도를 불러오지 못했어요.\n인터넷 연결을 확인하고\n다시 로그인해 주세요.\n\n진행도를 덮어쓰지 않으려고\n게임을 시작하지 않았어요.";
        }
    }

    private async UniTask Login(bool useManualSignIn)
    {
        if (_isLoggingIn)
        {
            return;
        }

        _isLoggingIn = true;
        _loginButton.interactable = false;
        _loginButtonText.text = "로그인 중...";

        AccountResult result = await AccountManager.Instance.TryLogin(useManualSignIn);
        if (result.Success)
        {
            if (GameAccountDeletionService.HasPendingDeletion(result.UserId))
            {
                _loginButtonText.text = "계정 삭제 마무리 중...";
                try
                {
                    await GameAccountDeletionService.DeleteAsync(result.UserId);
                    GameplaySaveGate.EndReset();
                    _isLoggingIn = false;
                    _loginButton.interactable = true;
                    _loginButtonText.text = IdleLabel;
                    _popupText = "게임 계정을 삭제했습니다.";
                    ShowLobbyPopup();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"게임 계정 삭제를 완료하지 못했습니다: {e.Message}");
                    _isLoggingIn = false;
                    _loginButton.interactable = true;
                    _loginButtonText.text = "계정 삭제 다시 시도";
                    _popupText = "계정 삭제를 완료하지 못했어요. 인터넷 연결을 확인하고 다시 시도해 주세요.";
                    ShowLobbyPopup();
                }
                return;
            }

            if (GameDataResetService.HasPendingReset(result.UserId))
            {
                _loginButtonText.text = "초기화 마무리 중...";
                try
                {
                    await GameDataResetService.ResetAsync(result.UserId);
                }
                catch (Exception e)
                {
                    // 초기화 결과가 불명확한 계정은 기존 진행도로 게임에 진입하지 않는다.
                    Debug.LogWarning($"게임 데이터 초기화를 완료하지 못했습니다: {e.Message}");
                    _isLoggingIn = false;
                    _loginButton.interactable = true;
                    _loginButtonText.text = "초기화 다시 시도";
                    _popupText = "초기화를 완료하지 못했어요. 인터넷 연결을 확인하고 다시 시도해 주세요.";
                    ShowLobbyPopup();
                    return;
                }

                GameplaySaveGate.EndReset();
            }
            if (!await TrySyncServerClock(result.UserId))
            {
                _isLoggingIn = false;
                _loginButton.interactable = true;
                _loginButtonText.text = "다시 시도";
                return;
            }

            await EnterGameScene();
            return;
        }

        _isLoggingIn = false;
        _loginButton.interactable = true;
        _loginButtonText.text = IdleLabel;

        if (useManualSignIn)
        {
            _popupText = result.ErrorMessage;
            ShowLobbyPopup();
        }
    }

    // 화면을 덮은 뒤 넘어간다. GameScene의 로딩 오버레이가 이미 덮여 있어야
    // 이음매가 보이지 않으므로 두 배경색을 맞춰 둔다.
    private async UniTask EnterGameScene()
    {
        if (_curtain != null) await _curtain.CoverAsync();

        SceneManagerEx.Instance.LoadGameScene();
    }

    private void ShowLobbyPopup()
    {
        MessagePopupUI.Instance.Show(_popupText);
    }
}
