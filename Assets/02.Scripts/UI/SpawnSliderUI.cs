using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnSliderUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;

    [Tooltip("타이머가 멈춘 이유를 게이지 위에 보여 줍니다.")]
    [SerializeField] private TextMeshProUGUI _spawnStateText;
    [SerializeField] private Button _spawnPoolButton;
    [SerializeField] private SpawnPoolPopupUI _spawnPoolPopup;
    [SerializeField] private ScholarGuideUI _scholarGuide;

    private const string AutoSpawnOffMessage = "자동 스폰 꺼짐";
    private const string FieldFullMessage = "공간 가득 참";

    // 게이지가 멈춰 있어도 숫자만 남으면 고장처럼 보인다. 멈춘 이유를 따로 알린다.
    private enum SpawnGaugeState
    {
        Unknown,
        Running,
        AutoSpawnOff,
        FieldFull,
    }

    private int _displayedRemainingTenths = int.MinValue;
    private int _displayedActiveCount = int.MinValue;
    private int _displayedMaxCount = int.MinValue;
    private SpawnGaugeState _displayedState = SpawnGaugeState.Unknown;
    public RectTransform SpawnPoolButtonTarget =>
        _spawnPoolButton != null
            ? _spawnPoolButton.transform as RectTransform
            : null;
    public RectTransform SpawnPoolPopupTarget =>
        _scholarGuide != null
            ? _scholarGuide.DetailTarget
            : _spawnPoolPopup != null ? _spawnPoolPopup.TutorialTarget : null;
    public RectTransform SpawnPoolChoiceTarget => _scholarGuide?.ProbabilityButtonTarget;
    public event Action ScholarGuideMenuOpened;
    public event Action SpawnPoolPopupOpened;
    public event Action SpawnPoolPopupClosed;

    private void Awake()
    {
        _spawnPoolPopup?.Hide();
        if (_spawnPoolPopup != null)
        {
            _spawnPoolPopup.Closed += OnSpawnPoolPopupClosed;
        }

        if (_scholarGuide != null)
        {
            _scholarGuide.MenuOpened += OnScholarGuideMenuOpened;
            _scholarGuide.ProbabilityOpened += OnScholarProbabilityOpened;
            _scholarGuide.Closed += OnSpawnPoolPopupClosed;
        }

        _spawnPoolButton?.onClick.AddListener(OpenScholarGuide);
    }

    private void Start()
    {
        SlimeManager.OnHighestGradeChanged += OnHighestGradeChanged;
        UpgradeManager.OnUpgraded += OnUpgraded;
    }

    private void OnEnable()
    {
        _displayedRemainingTenths = int.MinValue;
        _displayedActiveCount = int.MinValue;
        _displayedMaxCount = int.MinValue;
        _displayedState = SpawnGaugeState.Unknown;
    }

    private void OnDisable()
    {
        _spawnPoolPopup?.Hide();
    }

    private void OnDestroy()
    {
        _spawnPoolButton?.onClick.RemoveListener(OpenScholarGuide);
        if (_spawnPoolPopup != null)
        {
            _spawnPoolPopup.Closed -= OnSpawnPoolPopupClosed;
        }

        if (_scholarGuide != null)
        {
            _scholarGuide.MenuOpened -= OnScholarGuideMenuOpened;
            _scholarGuide.ProbabilityOpened -= OnScholarProbabilityOpened;
            _scholarGuide.Closed -= OnSpawnPoolPopupClosed;
        }

        SlimeManager.OnHighestGradeChanged -= OnHighestGradeChanged;
        UpgradeManager.OnUpgraded -= OnUpgraded;
    }

    private void Update()
    {
        if (SpawnManager.Instance == null) return;

        if (_slider != null)
        {
            _slider.value = SpawnManager.Instance.SpawnProgress;
        }

        if (_spawnIntervalText != null)
        {
            int remainingTenths = Mathf.RoundToInt(
                SpawnManager.Instance.RemainingTime * 10f);

            if (_displayedRemainingTenths != remainingTenths)
            {
                _displayedRemainingTenths = remainingTenths;
                // 간격이 아니라 다음 생성까지 남은 시간이다. 숫자만으로는 둘을 구분할 수 없다.
                _spawnIntervalText.text =
                    $"다음 생성 {remainingTenths * 0.1f:F1}초";
            }
        }

        RefreshSpawnState();

        if (_spawnMaxText != null)
        {
            int current = SpawnManager.Instance.GetMainStageSlimeCount();
            int max = SpawnManager.Instance.MaxActiveCount;

            if (_displayedActiveCount != current || _displayedMaxCount != max)
            {
                _displayedActiveCount = current;
                _displayedMaxCount = max;
                _spawnMaxText.text = $"[{current}/{max}]";
            }
        }
    }

    // SpawnManager.Update가 타이머를 멈추는 순서와 같게 판정한다. 자동 스폰이 꺼져
    // 있으면 자리를 보기 전에 멈추므로 그쪽이 먼저다.
    //
    // 튜토리얼의 일시정지는 알리지 않는다. 안내가 화면을 잡고 있어 게이지를 볼 일이 없고,
    // 끝나면 곧바로 풀린다.
    private void RefreshSpawnState()
    {
        if (_spawnStateText == null) return;

        SpawnGaugeState state = GetSpawnState();
        if (state == _displayedState) return;

        _displayedState = state;
        // 남은 시간과 멈춤 사유는 게이지 안의 같은 자리를 쓴다. 멈춰 있으면 줄지 않는
        // 시간은 의미가 없으므로 사유만 보인다.
        bool isRunning = state == SpawnGaugeState.Running;
        _spawnStateText.gameObject.SetActive(!isRunning);
        if (_spawnIntervalText != null)
        {
            _spawnIntervalText.gameObject.SetActive(isRunning);
        }

        _spawnStateText.text = state switch
        {
            SpawnGaugeState.AutoSpawnOff => AutoSpawnOffMessage,
            SpawnGaugeState.FieldFull => FieldFullMessage,
            _ => string.Empty,
        };
    }

    private static SpawnGaugeState GetSpawnState()
    {
        if (SlimeManager.Instance != null && !SlimeManager.Instance.IsAutoSpawnEnabled)
        {
            return SpawnGaugeState.AutoSpawnOff;
        }

        return SpawnManager.Instance.HasMainStageRoom()
            ? SpawnGaugeState.Running
            : SpawnGaugeState.FieldFull;
    }

    private void OpenScholarGuide()
    {
        if (_scholarGuide != null)
        {
            _scholarGuide.Open();
            return;
        }

        // 이전 씬 배선도 계속 동작하게 두되 새 씬에서는 학자 안내가 이 경로를 대신한다.
        if (_spawnPoolPopup == null || SpawnManager.Instance == null) return;
        RefreshSpawnPoolPopup();
        SpawnPoolPopupOpened?.Invoke();
    }

    public void CloseSpawnPoolPopup()
    {
        if (_scholarGuide != null && _scholarGuide.IsOpen)
        {
            _scholarGuide.Close();
            return;
        }

        _spawnPoolPopup?.Close();
    }

    private void OnScholarGuideMenuOpened()
    {
        ScholarGuideMenuOpened?.Invoke();
    }

    private void OnScholarProbabilityOpened()
    {
        SpawnPoolPopupOpened?.Invoke();
    }

    private void OnSpawnPoolPopupClosed()
    {
        SpawnPoolPopupClosed?.Invoke();
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        if (_spawnPoolPopup != null && _spawnPoolPopup.IsOpen)
        {
            RefreshSpawnPoolPopup();
        }
    }

    private void OnUpgraded(EUpgradeType type, ESlimeGrade grade)
    {
        if (type == EUpgradeType.HigherGradeSpawnWeightAdd &&
            _spawnPoolPopup != null &&
            _spawnPoolPopup.IsOpen)
        {
            RefreshSpawnPoolPopup();
        }
    }

    private void RefreshSpawnPoolPopup()
    {
        if (_spawnPoolPopup == null || SpawnManager.Instance == null) return;

        // 해금 전에는 레벨을 보여 줘도 뜻이 통하지 않는다. 음수로 넘겨 줄을 뺀다.
        bool isUnlocked = SlimeManager.Instance != null &&
                          SlimeManager.Instance.IsHigherGradeSpawnUnlocked;

        _spawnPoolPopup.Show(
            SpawnManager.Instance.GetCurrentSpawnProbabilities(),
            isUnlocked ? SpawnManager.GetSpawnWeightUpgradeLevel() : -1);
    }
}
