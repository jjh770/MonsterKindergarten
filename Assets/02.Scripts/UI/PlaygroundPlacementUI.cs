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

    [Header("UI")]
    [SerializeField] private Button _enterButton;
    [SerializeField] private GameObject _modeRoot;
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

    private Camera _camera;
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
            _gameExitManager == null || _modeRoot == null)
        {
            Debug.LogError("배치 모드의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _camera = Camera.main;
        _modeRoot.SetActive(false);
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

        SlimeManager.OnPlaygroundChanged += Refresh;

        if (GameplaySpaceManager.Instance != null)
        {
            GameplaySpaceManager.Instance.SpaceChanged += OnSpaceChanged;
        }

        RefreshEnterButton();
    }

    private void OnDestroy()
    {
        SlimeManager.OnPlaygroundChanged -= Refresh;

        // 종료 순서는 보장되지 않아 매니저가 먼저 사라질 수 있다.
        if (GameplaySpaceManager.Instance == null) return;

        GameplaySpaceManager.Instance.SpaceChanged -= OnSpaceChanged;
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

        bool isDisplayRoom = GameplaySpaceManager.Instance != null &&
                             !GameplaySpaceManager.Instance.IsMainFieldActive;
        _enterButton.gameObject.SetActive(isDisplayRoom);
    }

    private void Begin()
    {
        GameplaySpaceManager spaceManager = GameplaySpaceManager.Instance;
        if (_isActive ||
            spaceManager == null ||
            spaceManager.IsMainFieldActive ||
            spaceManager.IsTransitioning)
        {
            return;
        }

        _isActive = true;
        _upgradeUI.TryClose();
        _upgradeUI.SetToggleInputEnabled(false);
        _upgradeUI.SetToggleVisible(false);

        // 슬라임 관찰이 열리면 배치 탭과 겹친다. 이 모드에서는 월드 입력을 막고
        // 이 컴포넌트가 직접 읽는다.
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Selection);
        _gameExitManager.RegisterBackHandler(this, TryCancel);

        // 옮기는 동안 대포가 슬라임을 삼키거나 범퍼가 밀어내면, 플레이어가 잡고
        // 있는 것과 물리가 움직이는 것이 뒤섞인다.
        _field.SetInteractive(false);

        _modeRoot.SetActive(true);
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
        _isDragging = false;
        _heldIndex = -1;
        ClearSelection();

        _clicker.ReleaseMode(this);
        _gameExitManager.UnregisterBackHandler(this);
        _upgradeUI.SetToggleInputEnabled(true);
        _upgradeUI.SetToggleVisible(true);
        _modeRoot.SetActive(false);

        _field.SetInteractive(true);

        // 끌던 중이었다면 화면이 저장과 어긋나 있다. 저장 기준으로 다시 세운다.
        _field.Rebuild();
    }

    private void SelectType(EPlaygroundObjectType type)
    {
        if (RemainingCount(type) <= 0)
        {
            _toast?.Show("남은 것이 없어요. 상점에서 살 수 있어요.");
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

    private static int RemainingCount(EPlaygroundObjectType type)
    {
        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return 0;

        return manager.GetOwnedPlaygroundObjectCount(type) -
               manager.GetPlacedPlaygroundObjectCount(type);
    }

    private void RemoveSelected()
    {
        if (_selectedIndex < 0) return;

        SlimeManager.Instance?.TryRemovePlacedObject(_selectedIndex);
        ClearSelection();
    }

    private void Refresh()
    {
        if (_bumperText != null)
        {
            _bumperText.text = $"범퍼 {RemainingCount(EPlaygroundObjectType.Bumper)}";
        }

        if (_cannonText != null)
        {
            _cannonText.text = $"대포 {RemainingCount(EPlaygroundObjectType.Cannon)}";
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

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPosition = pointer.position.ReadValue();
        Vector2 world = _camera.ScreenToWorldPoint(screenPosition);

        if (pointer.press.wasPressedThisFrame) OnPointerDown(world);
        else if (pointer.press.isPressed) OnPointerDrag(world);
        else if (pointer.press.wasReleasedThisFrame) OnPointerUp(world);
    }

    private void OnPointerDown(Vector2 world)
    {
        _pointerDownWorld = world;
        _isDragging = false;
        _heldIndex = FindPlacedIndexAt(world);
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

        if (IsTooClose(x, y, index))
        {
            _toast?.Show("다른 것과 너무 가까워요.");
            _field.Rebuild();
            return;
        }

        SlimeManager.Instance?.TryMovePlacedObject(index, x, y);
    }

    private void Place(Vector2 world)
    {
        float x = PlaygroundRules.ClampX(world.x);
        float y = PlaygroundRules.ClampY(world.y);

        if (IsTooClose(x, y, -1))
        {
            _toast?.Show("다른 것과 너무 가까워요.");
            return;
        }

        if (SlimeManager.Instance == null ||
            !SlimeManager.Instance.TryPlacePlaygroundObject(_selectedType, x, y))
        {
            _toast?.Show("여기에는 놓을 수 없어요.");
            return;
        }

        // 남은 것이 없으면 고르기를 풀어 준다. 그대로 두면 눌러도 계속 거절된다.
        if (RemainingCount(_selectedType) <= 0) ClearSelection();

        Refresh();
    }

    // 겹쳐 놓으면 어느 쪽이 슬라임을 잡았는지 알 수 없다. ignoreIndex는 자기 자신이다.
    private static bool IsTooClose(float x, float y, int ignoreIndex)
    {
        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return false;

        IReadOnlyList<PlacedPlaygroundObject> placed = manager.PlacedPlaygroundObjects;
        for (int i = 0; i < placed.Count; i++)
        {
            if (i == ignoreIndex) continue;

            float dx = placed[i].X - x;
            float dy = placed[i].Y - y;
            if (dx * dx + dy * dy <
                PlaygroundRules.MinimumSpacing * PlaygroundRules.MinimumSpacing)
            {
                return true;
            }
        }

        return false;
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
