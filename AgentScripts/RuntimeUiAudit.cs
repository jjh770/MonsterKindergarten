using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class RuntimeUiAudit
{
    public static object ClickLogin()
    {
        foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (button.name != "LoginButton" || !button.interactable) continue;
            button.onClick.Invoke();
            return "LoginButton invoked";
        }

        return "Active LoginButton not found";
    }

    public static object AuditActive()
    {
        Canvas.ForceUpdateCanvases();
        var issues = new List<string>();
        int inspected = 0;

        foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!text.enabled || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) continue;

            text.ForceMeshUpdate();
            inspected++;
            Rect rect = text.rectTransform.rect;
            float renderedWidth = text.renderedWidth;
            float renderedHeight = text.renderedHeight;
            if (!text.isTextOverflowing &&
                renderedWidth <= rect.width + 1f &&
                renderedHeight <= rect.height + 1f)
            {
                continue;
            }

            issues.Add($"{GetPath(text.transform)} | rect={rect.width:F1}x{rect.height:F1} " +
                       $"rendered={renderedWidth:F1}x{renderedHeight:F1} overflowing={text.isTextOverflowing} " +
                       $"font={text.fontSize:F1} text={Escape(text.text)}");
        }

        var result = new StringBuilder();
        result.Append($"Screen={Screen.width}x{Screen.height}, inspected={inspected}, issues={issues.Count}");
        foreach (string issue in issues) result.Append("\n").Append(issue);
        return result.ToString();
    }

    public static object SetPortrait1080x1920()
    {
        Screen.SetResolution(1080, 1920, false);
        return "Requested 1080x1920";
    }

    public static object SetPortrait1080x2340()
    {
        Screen.SetResolution(1080, 2340, false);
        return "Requested 1080x2340";
    }

    public static object SetLandscape1920x1080()
    {
        Screen.SetResolution(1920, 1080, false);
        return "Requested 1920x1080";
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }

    private static string Escape(string value) =>
        value.Replace("\n", "\\n").Replace("\r", string.Empty);
}
