using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnSliderUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;
    [SerializeField] private Button _spawnPoolButton;
    [SerializeField] private SpawnPoolPopupUI _spawnPoolPopup;

    private int _displayedRemainingTenths = int.MinValue;
    private int _displayedActiveCount = int.MinValue;
    private int _displayedMaxCount = int.MinValue;
    public RectTransform SpawnPoolButtonTarget =>
        _spawnPoolButton != null
            ? _spawnPoolButton.transform as RectTransform
            : null;
    public event Action SpawnPoolPopupOpened;
    public event Action SpawnPoolPopupClosed;

    private void Awake()
    {
        _spawnPoolPopup?.Hide();
        if (_spawnPoolPopup != null)
        {
            _spawnPoolPopup.Closed += OnSpawnPoolPopupClosed;
        }

        _spawnPoolButton?.onClick.AddListener(OpenSpawnPoolPopup);
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
    }

    private void OnDisable()
    {
        _spawnPoolPopup?.Hide();
    }

    private void OnDestroy()
    {
        _spawnPoolButton?.onClick.RemoveListener(OpenSpawnPoolPopup);
        if (_spawnPoolPopup != null)
        {
            _spawnPoolPopup.Closed -= OnSpawnPoolPopupClosed;
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
                _spawnIntervalText.text = (remainingTenths * 0.1f).ToString("F1");
            }
        }

        if (_spawnMaxText != null)
        {
            int current = SpawnManager.Instance.GetActiveCount();
            int max = SpawnManager.Instance.MaxActiveCount;

            if (_displayedActiveCount != current || _displayedMaxCount != max)
            {
                _displayedActiveCount = current;
                _displayedMaxCount = max;
                _spawnMaxText.text = $"[{current}/{max}]";
            }
        }
    }

    private void OpenSpawnPoolPopup()
    {
        if (_spawnPoolPopup == null || SpawnManager.Instance == null) return;

        RefreshSpawnPoolPopup();
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

        _spawnPoolPopup.Show(
            SpawnManager.Instance.GetCurrentSpawnProbabilities());
    }
}
