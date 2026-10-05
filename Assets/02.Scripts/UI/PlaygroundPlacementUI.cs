using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 장식장 오브젝트 배치 모드.
//
// 보내기 모드(기획서 §7.4)와 같은 뼈대다. 하단 버튼으로 들어가고, 월드 입력을
// 가져오고, 뒤로 가기로 나간다. 다른 점은 고르는 대상이 슬라임이 아니라 자리라는 것이다.
//
// 월드 입력을 Clicker에 얹지 않고 직접 읽는다. Clicker는 슬라임을 고르는 일만
// 알고 있어서, 빈 자리를 집는 규칙을 그쪽에 넣으면 두 가지 일이 섞인다.
public sealed class PlaygroundPlacementUI : MonoBehaviour
{
    [SerializeField] private PlaygroundObjectField _field;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private ToastMessageUI _toast;
    // 보내기 모드와 같다. 자리를 고르는 동안에는 HUD가 자리를 비켜 준다.
    [SerializeField] private HudVisibility _hudVisibility;
    [SerializeField] private GameplaySpaceManager _spaceManager;
    [SerializeField] private SlimeManager _slimeManager;

    [Header("UI")]
    [SerializeField] private Button _enterButton;
    [SerializeField] private GameObject _modeRoot;
    [SerializeField] private CanvasGroup _modeCanvasGroup;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Button _removeButton;
    [SerializeField] private Button _bumperButton;
    [SerializeField] private Button _cannonButton;
    [SerializeField] private TextMeshProUGUI _bumperText;
    [SerializeField] private TextMeshProUGUI _cannonText;
    [SerializeField] private TextMeshProUGUI _guideText;

    [Header("Input")]
    [Tooltip("이만큼 끌어야 옮기는 것으로 본다. 그 아래는 고르기로 본다.")]
    [SerializeField, Min(0.01f)] private float _dragThreshold = 0.25f;

    [Tooltip("여기를 누른 손가락은 자리를 고르는 것으로 보지 않습니다.")]
    [SerializeField] private RectTransform[] _pointerBlockers = Array.Empty<RectTransform>();

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _modeAnimationDuration = 0.35f;

    private Camera _camera;
    private GameplayModeSession _modeSession;
    private bool _isPointerBlocked;
    private bool _isActive;
    private bool _hasSelectedType;
    private EPlaygroundObjectType _selectedType;
    private int _heldIndex = -1;
    private int _selectedIndex = -1;
    private bool _isDragging;
    private Vector2 _pointerDownWorld;

    private void Awake()
    {
        if (_field == null || _clicker == null || _upgradeUI == null ||
            _gameExitManager == null || _modeRoot == null ||
            _modeCanvasGroup == null || _hudVisibility == null ||
            _spaceManager == null || _slimeManager == null)
        {
            Debug.LogError("배치 모드의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _camera = Camera.main;
        _modeSession = new GameplayModeSession(
            _upgradeUI,
            _clicker,
            _gameExitManager,
            _hudVisibility,
            EHudParts.All,
            _modeRoot,
            _modeCanvasGroup,
            _modeAnimationDuration);
        _modeSession.ResetPresentation();
    }

    private void Start()
    {
        if (!enabled) return;

        if (_enterButton != null) _enterButton.onClick.AddListener(Begin);
        if (_exitButton != null) _exitButton.onClick.AddListener(End);
        if (_removeButton != null) _removeButton.onClick.AddListener(RemoveSelected);
        if (_bumperButton != null)
            _bumperButton.onClick.AddListener(() => SelectType(EPlaygroundObjectType.Bumper));
        if (_cannonButton != null)
            _cannonButton.onClick.AddListener(() => SelectType(EPlaygroundObjectType.Cannon));

        _slimeManager.PlaygroundChanged += Refresh;

        _spaceManager.SpaceChanged += OnSpaceChanged;

        RefreshEnterButton();
    }

    private void OnDestroy()
    {
        DisplayRoomCameraInputGate.Release(this);
        _modeSession?.Dispose();
        _slimeManager.PlaygroundChanged -= Refresh;

        // 종료 순서는 보장되지 않아 매니저가 먼저 사라질 수 있다.
        if (_spaceManager != null) _spaceManager.SpaceChanged -= OnSpaceChanged;
    }

    private void OnSpaceChanged(EGameplaySpace space)
    {
        // 장식장을 벗어나면 배치할 것이 없다.
        if (space != EGameplaySpace.DisplayRoom && _isActive) End();

        RefreshEnterButton();
    }

    private void RefreshEnterButton()
    {
        if (_enterButton == null) return;

        bool isDisplayRoom = !_spaceManager.IsMainFieldActive;
        _enterButton.gameObject.SetActive(isDisplayRoom);
    }

    private void Begin()
    {
        GameplaySpaceManager spaceManager = _spaceManager;
        if (_isActive ||
            spaceManager == null ||
            spaceManager.IsMainFieldActive ||
            spaceManager.IsTransitioning)
        {
            return;
        }

        // 확대한 채로는 놓을 자리가 화면 밖에 있을 수 있다. 방 전체가 보이게 되돌린 뒤
        // 들어간다. 되돌리는 도중에 또 눌러도 이전 연출이 끊기고 하나만 끝까지 가므로
        // 모드가 두 번 열리지 않는다.
        if (spaceManager.IsDisplayRoomZoomed)
        {
            spaceManager.RestoreDisplayRoomFocus(Begin);
            return;
        }

        _isActive = true;
        DisplayRoomCameraInputGate.Push(this);
        _modeSession.Enter(
            ClickerInputMode.Blocked,
            ClickerInputPriority.Selection,
            TryCancel);

        // 옮기는 동안 대포가 슬라임을 삼키거나 범퍼가 밀어내면, 플레이어가 잡고
        // 있는 것과 물리가 움직이는 것이 뒤섞인다.
        _field.SetInteractive(false);

        // 형제 순서는 씬이 정한다. 여기서 맨 위로 올리면 공간 전환 막까지 덮는다.
        ClearSelection();
        Refresh();
    }

    private bool TryCancel()
    {
        if (!_isActive) return false;

        End();
        return true;
    }

    private void End()
    {
        if (!_isActive) return;

        _isActive = false;
        DisplayRoomCameraInputGate.Release(this);
        _isDragging = false;
        _isPointerBlocked = false;
        _heldIndex = -1;
        ClearSelection();

        _modeSession.Exit();

        _field.SetInteractive(true);

        // 끌던 중이었다면 화면이 저장과 어긋나 있다. 저장 기준으로 다시 세운다.
        _field.Rebuild();
    }

    private void SelectType(EPlaygroundObjectType type)
    {
        if (!CanPlace(type))
        {
            _toast?.Show(UiMessages.PlacementUnavailable);
            return;
        }

        _hasSelectedType = true;
        _selectedType = type;
        _selectedIndex = -1;
        Refresh();
    }

    private void ClearSelection()
    {
        _hasSelectedType = false;
        _selectedIndex = -1;
    }

    private bool CanPlace(EPlaygroundObjectType type)
    {
        SlimeManager manager = _slimeManager;
        if (manager == null) return false;

        return manager.GetOwnedPlaygroundObjectCount(type) > 0 &&
               manager.GetPlacedPlaygroundObjectCount(type) == 0;
    }

    private void RemoveSelected()
    {
        int indexToRemove = _selectedIndex;
        if (indexToRemove < 0) return;

        // 선택을 먼저 해제한다. TryRemovePlacedObject가 성공하면 PlaygroundChanged로 Refresh가
        // 도는데, 그때 _selectedIndex가 이미 -1이라야 치우기 버튼과 안내가 정리된다.
        ClearSelection();
        if (!_slimeManager.TryRemovePlacedObject(indexToRemove))
        {
            // 실패 시엔 이벤트가 오지 않으므로 직접 화면을 정리한다.
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_bumperText != null)
        {
            _bumperText.text = "범퍼";
        }

        if (_cannonText != null)
        {
            _cannonText.text = "대포";
        }

        if (_bumperButton != null)
        {
            _bumperButton.interactable = CanPlace(EPlaygroundObjectType.Bumper);
        }

        if (_cannonButton != null)
        {
            _cannonButton.interactable = CanPlace(EPlaygroundObjectType.Cannon);
        }

        if (_removeButton != null)
        {
            _removeButton.gameObject.SetActive(_selectedIndex >= 0);
        }

        if (_guideText == null) return;

        if (_selectedIndex >= 0) _guideText.text = "옮기려면 끌고, 치우려면 버튼을 누르세요.";
        else if (_hasSelectedType) _guideText.text = "놓을 자리를 누르세요.";
        else _guideText.text = "놓을 것을 고르거나, 놓인 것을 눌러 보세요.";
    }

    private void Update()
    {
        if (!_isActive || _camera == null) return;

        if (PointerGestureUtility.HasMultipleActiveTouches())
        {
            if (_isDragging || _heldIndex >= 0) _field.Rebuild();
            _isPointerBlocked = false;
            _isDragging = false;
            _heldIndex = -1;
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPosition = pointer.position.ReadValue();
        Vector2 world = _camera.ScreenToWorldPoint(screenPosition);

        if (pointer.press.wasPressedThisFrame) OnPointerDown(world, screenPosition);
        else if (_isPointerBlocked)
        {
            if (pointer.press.wasReleasedThisFrame) _isPointerBlocked = false;
        }
        else if (pointer.press.isPressed) OnPointerDrag(world);
        else if (pointer.press.wasReleasedThisFrame) OnPointerUp(world);
    }

    private void OnPointerDown(Vector2 world, Vector2 screenPosition)
    {
        // 버튼을 누른 손가락으로 자리까지 고르면, 범퍼를 고를 때마다 버튼 아래
        // 언저리에 하나씩 놓인다. 화면 밖 좌표는 규칙이 안쪽으로 끌어당기므로
        // 자리가 없어서 거절되지도 않는다.
        _isPointerBlocked = IsOverBlocker(screenPosition);
        if (_isPointerBlocked) return;

        _pointerDownWorld = world;
        _isDragging = false;
        _heldIndex = FindPlacedIndexAt(world);
    }

    private bool IsOverBlocker(Vector2 screenPosition)
    {
        foreach (RectTransform blocker in _pointerBlockers)
        {
            if (blocker == null || !blocker.gameObject.activeInHierarchy) continue;

            // 오버레이 캔버스라 카메라가 없다.
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    blocker, screenPosition, null))
            {
                return true;
            }
        }

        return false;
    }

    private void OnPointerDrag(Vector2 world)
    {
        if (_heldIndex < 0) return;

        if (!_isDragging &&
            Vector2.Distance(_pointerDownWorld, world) > _dragThreshold)
        {
            _isDragging = true;
        }

        if (!_isDragging) return;

        GameObject held = GetSpawned(_heldIndex);
        if (held == null) return;

        held.transform.localPosition = new Vector3(
            PlaygroundRules.ClampX(world.x),
            PlaygroundRules.ClampY(world.y),
            0f);
    }

    private void OnPointerUp(Vector2 world)
    {
        if (_isDragging && _heldIndex >= 0)
        {
            CommitMove(_heldIndex, world);
        }
        else if (_heldIndex >= 0)
        {
            _selectedIndex = _heldIndex;
            _hasSelectedType = false;
            Refresh();
        }
        else if (_hasSelectedType)
        {
            Place(world);
        }
        else
        {
            ClearSelection();
            Refresh();
        }

        _isDragging = false;
        _heldIndex = -1;
    }

    private void CommitMove(int index, Vector2 world)
    {
        float x = PlaygroundRules.ClampX(world.x);
        float y = PlaygroundRules.ClampY(world.y);
        SlimeManager manager = _slimeManager;

        if (manager != null &&
            !manager.IsPlaygroundPositionAvailable(x, y, index))
        {
            _toast?.Show(UiMessages.TooCloseToOther);
            _field.Rebuild();
            return;
        }

        if (manager == null || !manager.TryMovePlacedObject(index, x, y))
        {
            _toast?.Show(UiMessages.CannotMoveHere);
            _field.Rebuild();
        }
    }

    private void Place(Vector2 world)
    {
        if (!PlaygroundRules.Contains(world.x, world.y))
        {
            _toast?.Show(UiMessages.PlaceInsideRoom);
            return;
        }

        float x = PlaygroundRules.ClampX(world.x);
        float y = PlaygroundRules.ClampY(world.y);
        SlimeManager manager = _slimeManager;

        if (manager != null &&
            !manager.IsPlaygroundPositionAvailable(x, y))
        {
            _toast?.Show(UiMessages.TooCloseToOther);
            return;
        }

        if (manager == null ||
            !manager.TryPlacePlaygroundObject(_selectedType, x, y))
        {
            _toast?.Show(UiMessages.CannotPlaceHere);
            return;
        }

        // 하나를 놓으면 해당 종류는 더 배치할 수 없으므로 선택도 함께 해제한다.
        if (!CanPlace(_selectedType)) ClearSelection();

        Refresh();
    }

    private GameObject GetSpawned(int index)
    {
        IReadOnlyList<GameObject> spawned = _field.Spawned;
        return index >= 0 && index < spawned.Count ? spawned[index] : null;
    }

    // 화면에 선 순서가 저장 목록의 순서와 같다. 가장 가까운 것을 집는다.
    private int FindPlacedIndexAt(Vector2 world)
    {
        IReadOnlyList<GameObject> spawned = _field.Spawned;
        int nearest = -1;
        float nearestDistance = PlaygroundRules.MinimumSpacing * 0.5f;

        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;

            float distance = Vector2.Distance(world, spawned[i].transform.position);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = i;
        }

        return nearest;
    }
}
