using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Play Mode에서 사람 대신 화면을 누르는 QA용 도구다. 에디터 평가 창이나 시나리오 테스트에서 부른다.
//
// UI는 실제 터치처럼 EventSystem 레이캐스트로 맨 위의 대상을 찾아 누른다. 그래서 막는 판이나 튜토리얼
// 구멍 밖을 누르면 실제 기기처럼 막힌다. 월드의 슬라임은 Clicker의 누름·끌기·뗌 경로를 그대로 탄다.
// Input System 장치를 흉내 내지 않는 이유는 에디터에서 게임 창에 포커스가 없으면 입력이 버려지기 때문이다.
public static class QaDriver
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    // 화면 좌표 한 점을 누른다. 실제 터치처럼 맨 위 UI와 월드 슬라임에 함께 전달한다.
    // Clicker는 UI가 덮여 있는지 묻지 않고 포인터를 직접 읽기 때문에 기기에서도 둘 다 받는다.
    public static string TapScreen(Vector2 screen)
    {
        string ui = TapUiAt(screen);
        string world = TapWorldScreen(screen);
        return ui + " | " + world;
    }

    // UI에만 전달한다. 슬라임 위의 버튼을 누를 때처럼 월드 클릭을 일부러 빼야 할 때 쓴다.
    public static string TapUiAt(Vector2 screen)
    {
        GameObject ui = RaycastUi(screen, out RaycastResult hit);
        if (ui != null)
        {
            var data = new PointerEventData(EventSystem.current)
            {
                position = screen,
                pressPosition = screen,
                button = PointerEventData.InputButton.Left,
                pointerPressRaycast = hit,
                pointerCurrentRaycast = hit,
            };
            GameObject down = ExecuteEvents.ExecuteHierarchy(ui, data, ExecuteEvents.pointerDownHandler);
            GameObject click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(ui);
            data.pointerPress = down != null ? down : click;
            data.rawPointerPress = ui;
            ExecuteEvents.ExecuteHierarchy(ui, data, ExecuteEvents.pointerUpHandler);
            if (click != null) ExecuteEvents.Execute(click, data, ExecuteEvents.pointerClickHandler);
            return "ui:" + PathOf(click != null ? click.transform : ui.transform);
        }

        return "ui:none";
    }

    // 이름이나 경로로 찾은 UI의 가운데를 실제 터치처럼 누른다. 위에 다른 것이 덮여 있으면 그것이 눌린다.
    public static string TapUi(string pathOrName)
    {
        GameObject go = Find(pathOrName);
        if (go == null) return "not found: " + pathOrName;
        if (!go.activeInHierarchy) return "inactive: " + PathOf(go.transform);

        return TapScreen(ScreenOf((RectTransform)go.transform));
    }

    public static string TapWorld(Transform target)
    {
        return TapWorldScreen(Camera.main.WorldToScreenPoint(target.position));
    }

    // 슬라임을 잡고 화면 좌표 to까지 끌어 놓는다. 놓는 자리에 같은 등급이 있으면 합성까지 간다.
    public static string DragWorld(Transform from, Vector3 toWorld)
    {
        Clicker clicker = Object.FindFirstObjectByType<Clicker>();
        Camera camera = Camera.main;
        Vector2 start = camera.WorldToScreenPoint(from.position);
        Vector2 end = camera.WorldToScreenPoint(toWorld);

        Invoke(clicker, "TrySelect", start);
        if (Get<SlimeController>(clicker, "_selectedTarget") == null) return "drag: nothing selected";

        // 시간 문턱을 넘긴 것으로 친다. 거리 문턱은 끝점이 넘긴다.
        Set(clicker, "_mouseDownTime", Time.time - 1f);
        Invoke(clicker, "CheckDragStart", end);
        if (Get<bool>(clicker, "_isDragging")) Invoke(clicker, "UpdateDrag", end);
        Invoke(clicker, "OnPointerUp", end);
        return "dragged " + from.name;
    }

    // 같은 등급 두 마리를 겹쳐 합성시킨다.
    public static string Merge(SlimeController a, SlimeController b)
    {
        return DragWorld(a.transform, b.transform.position);
    }

    // 지금 화면에 떠 있는 튜토리얼 대사, 안내 문구, 토스트를 모아 보여 준다.
    public static string Status()
    {
        var builder = new StringBuilder();
        builder.Append("tutorialRunning=").Append(TutorialManager.IsRunning);
        builder.Append(" gameplayActive=").Append(GameplayGate.IsActive).AppendLine();
        foreach (TutorialDialogueView view in Object.FindObjectsByType<TutorialDialogueView>(FindObjectsSortMode.None))
        {
            if (!view.isActiveAndEnabled) continue;
            builder.AppendLine("dialogue: " + Flatten(Get<TextMeshProUGUI>(view, "_dialogueText")?.text));
        }

        foreach (TutorialSpotlightView view in Object.FindObjectsByType<TutorialSpotlightView>(FindObjectsSortMode.None))
        {
            if (!view.isActiveAndEnabled) continue;
            foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(false))
            {
                builder.AppendLine("spotlight: " + Flatten(text.text));
            }
        }

        foreach (ToastMessageUI toast in Object.FindObjectsByType<ToastMessageUI>(FindObjectsSortMode.None))
        {
            foreach (TMP_Text text in toast.GetComponentsInChildren<TMP_Text>(false))
            {
                if (text.isActiveAndEnabled && text.alpha > 0.01f) builder.AppendLine("toast: " + Flatten(text.text));
            }
        }

        return builder.ToString();
    }

    // 튜토리얼 대화의 다음 버튼을 누른다. 실제 터치처럼 눌러서 막힌 경우도 드러난다.
    public static string NextDialogue()
    {
        foreach (TutorialDialogueView view in Object.FindObjectsByType<TutorialDialogueView>(FindObjectsSortMode.None))
        {
            if (!view.isActiveAndEnabled) continue;
            Button next = Get<Button>(view, "_nextButton");
            if (next == null || !next.isActiveAndEnabled) continue;

            return TapScreen(ScreenOf((RectTransform)next.transform));
        }

        return "no dialogue";
    }

    // 튜토리얼 안내 구멍(스포트라이트)의 가운데를 누른다.
    public static string TapSpotlightHole()
    {
        foreach (TutorialSpotlightView view in Object.FindObjectsByType<TutorialSpotlightView>(FindObjectsSortMode.None))
        {
            if (!view.isActiveAndEnabled || !view.TryGetHoleWorldRect(out Rect rect)) continue;

            Canvas canvas = view.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return TapScreen(RectTransformUtility.WorldToScreenPoint(camera, rect.center));
        }

        return "no spotlight";
    }

    public static List<SlimeController> MainFieldSlimes()
    {
        var result = new List<SlimeController>();
        foreach (SlimeController slime in Object.FindObjectsByType<SlimeController>(FindObjectsSortMode.None))
        {
            if (slime.isActiveAndEnabled && slime.Location == ESlimeLocation.MainField) result.Add(slime);
        }

        return result;
    }

    // 같은 등급 쌍을 찾아 하나 합성한다. 특별 슬라임은 뺀다.
    public static string MergeAnyPair()
    {
        List<SlimeController> slimes = MainFieldSlimes();
        for (int i = 0; i < slimes.Count; i++)
        {
            for (int j = i + 1; j < slimes.Count; j++)
            {
                if (slimes[i].CanMergeWith(slimes[j])) return Merge(slimes[i], slimes[j]);
            }
        }

        return "no pair";
    }

    public static GameObject Find(string pathOrName)
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!transform.gameObject.scene.IsValid()) continue;

            string path = PathOf(transform);
            if (path == pathOrName || path.EndsWith("/" + pathOrName) || transform.name == pathOrName)
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    public static string PathOf(Transform transform)
    {
        string path = transform.name;
        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }

    public static Vector2 ScreenOf(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }

    private static string TapWorldScreen(Vector2 screen)
    {
        Clicker clicker = Object.FindFirstObjectByType<Clicker>();
        if (clicker == null) return "no clicker";

        Invoke(clicker, "TrySelect", screen);
        SlimeController selected = Get<SlimeController>(clicker, "_selectedTarget");
        if (selected == null) return "world: nothing";

        Invoke(clicker, "OnPointerUp", screen);
        return "world:" + selected.name;
    }

    private static GameObject RaycastUi(Vector2 screen, out RaycastResult first)
    {
        first = default;
        if (EventSystem.current == null) return null;

        var data = new PointerEventData(EventSystem.current) { position = screen };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        if (results.Count == 0) return null;

        first = results[0];
        return first.gameObject;
    }

    private static string Flatten(string text)
    {
        return text == null ? "" : text.Replace("\r", "").Replace("\n", "\\n");
    }

    private static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, Instance).Invoke(target, args);
    }

    private static T Get<T>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, Instance);
        return info == null ? default : (T)info.GetValue(target);
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, Instance).SetValue(target, value);
    }
}
