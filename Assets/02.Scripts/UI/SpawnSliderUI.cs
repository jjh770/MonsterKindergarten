using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpawnSliderUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _spawnIntervalText;
    [SerializeField] private TextMeshProUGUI _spawnMaxText;
    [SerializeField] private Button _spawnPoolButton;

    private int _displayedRemainingTenths = int.MinValue;
    private int _displayedActiveCount = int.MinValue;
    private int _displayedMaxCount = int.MinValue;
    private RectTransform _spawnPoolPopup;
    private TextMeshProUGUI _spawnPoolText;

    public RectTransform SpawnPoolButtonTarget =>
        _spawnPoolButton != null
            ? _spawnPoolButton.transform as RectTransform
            : null;
    public event Action SpawnPoolPopupOpened;
    public event Action SpawnPoolPopupClosed;

    private void Awake()
    {
        CreateSpawnPoolPopup();
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
        if (_spawnPoolPopup != null)
        {
            _spawnPoolPopup.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        _spawnPoolButton?.onClick.RemoveListener(OpenSpawnPoolPopup);
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

    private void CreateSpawnPoolPopup()
    {
        if (_spawnIntervalText == null) return;

        GameObject popupObject = new GameObject(
            "SpawnPoolPopup",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline));
        popupObject.layer = gameObject.layer;

        _spawnPoolPopup = (RectTransform)popupObject.transform;
        _spawnPoolPopup.SetParent(transform, false);
        _spawnPoolPopup.anchorMin = new Vector2(0.5f, 0f);
        _spawnPoolPopup.anchorMax = new Vector2(0.5f, 0f);
        _spawnPoolPopup.pivot = new Vector2(0.5f, 1f);
        _spawnPoolPopup.anchoredPosition = new Vector2(0f, -20f);
        _spawnPoolPopup.sizeDelta = new Vector2(560f, 240f);

        Image popupImage = popupObject.GetComponent<Image>();
        popupImage.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        Outline outline = popupObject.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.5f);
        outline.effectDistance = new Vector2(3f, -3f);

        Button closeButton = popupObject.GetComponent<Button>();
        closeButton.targetGraphic = popupImage;
        closeButton.onClick.AddListener(CloseSpawnPoolPopup);

        GameObject textObject = Instantiate(_spawnIntervalText.gameObject, _spawnPoolPopup);
        textObject.name = "SpawnPoolText";
        _spawnPoolText = textObject.GetComponent<TextMeshProUGUI>();

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 25f);
        textRect.offsetMax = new Vector2(-30f, -25f);

        _spawnPoolText.alignment = TextAlignmentOptions.Center;
        _spawnPoolText.enableAutoSizing = true;
        _spawnPoolText.fontSizeMin = 22f;
        _spawnPoolText.fontSizeMax = 36f;
        _spawnPoolText.raycastTarget = false;

        popupObject.SetActive(false);
    }

    private void OpenSpawnPoolPopup()
    {
        if (_spawnPoolPopup == null || SpawnManager.Instance == null) return;

        RefreshSpawnPoolPopup();
        _spawnPoolPopup.gameObject.SetActive(true);
        _spawnPoolPopup.SetAsLastSibling();
        SpawnPoolPopupOpened?.Invoke();
    }

    private void CloseSpawnPoolPopup()
    {
        if (_spawnPoolPopup != null)
        {
            _spawnPoolPopup.gameObject.SetActive(false);
            SpawnPoolPopupClosed?.Invoke();
        }
    }

    private void OnHighestGradeChanged(ESlimeGrade grade)
    {
        if (_spawnPoolPopup != null && _spawnPoolPopup.gameObject.activeSelf)
        {
            RefreshSpawnPoolPopup();
        }
    }

    private void OnUpgraded(EUpgradeType type, ESlimeGrade grade)
    {
        if (type == EUpgradeType.HigherGradeSpawnWeightAdd &&
            _spawnPoolPopup != null &&
            _spawnPoolPopup.gameObject.activeSelf)
        {
            RefreshSpawnPoolPopup();
        }
    }

    private void RefreshSpawnPoolPopup()
    {
        if (_spawnPoolText == null || SpawnManager.Instance == null) return;

        List<SpawnProbability> probabilities =
            SpawnManager.Instance.GetCurrentSpawnProbabilities();
        var builder = new StringBuilder("현재 자연 등장 확률\n");

        foreach (SpawnProbability probability in probabilities)
        {
            builder.Append("<sprite name=\"")
                .Append(((int)probability.Grade).ToString("00"))
                .Append("\"> Lv.")
                .Append((int)probability.Grade)
                .Append("   ")
                .Append((probability.Probability * 100f).ToString("F1"))
                .Append("%\n");
        }

        builder.Append("\n눌러서 닫기");
        _spawnPoolText.text = builder.ToString();
        _spawnPoolPopup.sizeDelta = new Vector2(
            _spawnPoolPopup.sizeDelta.x,
            150f + probabilities.Count * 58f);
    }
}
