using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DisplayRoomUI : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _spaceButton;
    [SerializeField] private TextMeshProUGUI _spaceButtonText;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField, Min(0f)] private float _buttonMargin = 50f;

    private void Start()
    {
        if (_canvas == null ||
            _spaceButton == null ||
            _spaceButtonText == null ||
            _gameExitManager == null ||
            StageManager.Instance == null)
        {
            Debug.LogError("장식장 UI의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _spaceButton.onClick.AddListener(OnSpaceButtonClicked);
        StageManager.Instance.SpaceChanged += OnSpaceChanged;
        GameManager.OnAllDataInitialized += Refresh;
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;

        RefreshLayout();
        Refresh();
    }

    private void OnDestroy()
    {
        _spaceButton?.onClick.RemoveListener(OnSpaceButtonClicked);

        if (StageManager.Instance != null)
        {
            StageManager.Instance.SpaceChanged -= OnSpaceChanged;
        }

        GameManager.OnAllDataInitialized -= Refresh;
        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
        _gameExitManager?.UnregisterBackHandler(this);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_spaceButton != null && _canvas != null)
        {
            RefreshLayout();
        }
    }

    private void OnSpaceButtonClicked()
    {
        StageManager stageManager = StageManager.Instance;
        if (stageManager == null) return;

        if (stageManager.IsMainStageActive)
        {
            stageManager.TryEnterDisplayRoom();
        }
        else
        {
            stageManager.TryExitDisplayRoom();
        }
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        if (space == EGameplaySpace.DisplayRoom)
        {
            _gameExitManager.RegisterBackHandler(this, TryExitDisplayRoom);
        }
        else
        {
            _gameExitManager.UnregisterBackHandler(this);
        }

        Refresh();
    }

    private bool TryExitDisplayRoom()
    {
        return StageManager.Instance != null &&
               StageManager.Instance.TryExitDisplayRoom();
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_spaceButton == null || _spaceButtonText == null) return;

        StageManager stageManager = StageManager.Instance;
        bool isDisplayRoom = stageManager != null &&
                             !stageManager.IsMainStageActive;
        bool isUnlocked = GameManager.Instance != null &&
                          GameManager.Instance.IsAllDataInitialized &&
                          SlimeManager.Instance != null &&
                          SlimeManager.Instance.HighestGrade >= ESlimeGrade.Grade3;

        _spaceButton.gameObject.SetActive(isDisplayRoom || isUnlocked);
        _spaceButtonText.text = isDisplayRoom ? "돌아가기" : "장식장";
    }

    private void RefreshLayout()
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;
        RectTransform buttonRect = _spaceButton.transform as RectTransform;
        if (canvasRect == null || buttonRect == null) return;

        float leftInset = GetCanvasInset(
            Screen.safeArea.xMin,
            Screen.width,
            canvasRect.rect.width);
        float topInset = GetCanvasInset(
            Screen.height - Screen.safeArea.yMax,
            Screen.height,
            canvasRect.rect.height);
        buttonRect.anchoredPosition = new Vector2(
            leftInset + _buttonMargin,
            -topInset - _buttonMargin);
    }

    private static float GetCanvasInset(
        float pixelInset,
        int screenSize,
        float canvasSize)
    {
        if (screenSize <= 0 || canvasSize <= 0f) return 0f;

        return Mathf.Max(0f, pixelInset / screenSize * canvasSize);
    }
}
