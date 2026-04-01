using System.IO;
using NanoGrowth;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RestoreUiCanvasTool
{
    [MenuItem("Tools/Nano Growth/Restore UI Canvas")]
    public static void RestoreCanvas()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Restore UI Canvas] Open a scene first.");
            return;
        }

        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        Canvas canvas = EnsureComponent<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        EnsureComponent<GraphicRaycaster>(canvasGo);
        EnsureEventSystem();

        RectTransform hudRoot = EnsureRect("HUDRoot", canvasGo.transform);
        Stretch(hudRoot);

        RectTransform progressBg = EnsureImage("ProgressBar_BG", hudRoot, new Color(0f, 0f, 0f, 0.45f));
        SetRect(progressBg, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(640f, 34f));

        RectTransform progressFillRt = EnsureImage("ProgressBar_Fill", progressBg, new Color(0.25f, 1f, 0.45f, 0.95f));
        Stretch(progressFillRt);
        progressFillRt.offsetMin = new Vector2(4f, 4f);
        progressFillRt.offsetMax = new Vector2(-4f, -4f);
        Image progressFill = progressFillRt.GetComponent<Image>();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;

        TextMeshProUGUI progressText = EnsureText("ProgressText", hudRoot, "NANO: 0/3000", 34, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetRect(progressText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(700f, 64f));

        TextMeshProUGUI warningText = EnsureText("WarningText", hudRoot, "NOT ENOUGH MASS", 32, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.2f, 1f));
        SetRect(warningText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(900f, 64f));
        warningText.gameObject.SetActive(false);

        TextMeshProUGUI unlockText = EnsureText("UnlockText", hudRoot, "SWORD MODE UNLOCKED!", 40, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.96f, 0.35f, 1f));
        SetRect(unlockText.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 90f));
        unlockText.gameObject.SetActive(false);

        TextMeshProUGUI comboText = EnsureText("ComboText", hudRoot, "2", 60, FontStyles.Bold, TextAlignmentOptions.TopRight, new Color(1f, 0.9f, 0.2f, 1f));
        SetRect(comboText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -44f), new Vector2(260f, 92f));
        comboText.gameObject.SetActive(false);

        TextMeshProUGUI popTemplate = EnsureText("PopTextTemplate", hudRoot, "+100", 36, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetRect(popTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 80f));
        popTemplate.gameObject.SetActive(false);

        RectTransform flashRt = EnsureImage("UnlockFlashOverlay", hudRoot, new Color(1f, 1f, 1f, 0f));
        Stretch(flashRt);
        Image flashImage = flashRt.GetComponent<Image>();
        flashImage.raycastTarget = false;
        flashRt.gameObject.SetActive(false);

        AudioSource absorbSfx = EnsureAudioSource("AbsorbSfxSource", canvasGo.transform);
        AudioSource comboSfx = EnsureAudioSource("ComboSfxSource", canvasGo.transform);
        AudioSource unlockSfx = EnsureAudioSource("UnlockSfxSource", canvasGo.transform);

        SwarmHUD hud = EnsureComponent<SwarmHUD>(hudRoot.gameObject);
        SwarmController swarm = Object.FindObjectOfType<SwarmController>();
        hud.swarm = swarm;
        hud.progressText = progressText;
        hud.warningText = warningText;
        hud.unlockText = unlockText;
        hud.popTextTemplate = popTemplate;
        hud.comboText = comboText;
        hud.absorbSfxSource = absorbSfx;
        hud.comboSfxSource = comboSfx;
        hud.absorbClip = FindAudioClip("coin", "pickup");
        hud.comboUpClip = FindAudioClip("levelup", "level", "up");

        SerializedObject hudSo = new SerializedObject(hud);
        hudSo.FindProperty("progressBarFill").objectReferenceValue = progressFill;
        hudSo.FindProperty("unlockFlashOverlay").objectReferenceValue = flashImage;
        hudSo.FindProperty("unlockSfxSource").objectReferenceValue = unlockSfx;
        hudSo.FindProperty("unlockSfxClip").objectReferenceValue = FindAudioClip("achievement", "unlock");
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        RectTransform uaRoot = EnsureRect("UAEffects", canvasGo.transform);
        Stretch(uaRoot);
        uaRoot.SetAsLastSibling();

        TextMeshProUGUI hookText = EnsureText("HookText", uaRoot, "ABSORB TO GROW!", 54, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetRect(hookText.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 120f));
        hookText.gameObject.SetActive(false);

        TextMeshProUGUI morphText = EnsureText("MorphText", uaRoot, "MORPH AS WEAPONS!", 54, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        SetRect(morphText.rectTransform, new Vector2(0.5f, 0.69f), new Vector2(0.5f, 0.69f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 120f));
        morphText.gameObject.SetActive(false);

        GameObject playNowButton = EnsureButton("PlayNowBtn", uaRoot);
        RectTransform playNowRt = playNowButton.GetComponent<RectTransform>();
        SetRect(playNowRt, new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 110f));
        playNowButton.SetActive(false);

        UAEffectController ua = EnsureComponent<UAEffectController>(uaRoot.gameObject);
        SerializedObject uaSo = new SerializedObject(ua);
        uaSo.FindProperty("hookText").objectReferenceValue = hookText;
        uaSo.FindProperty("morphText").objectReferenceValue = morphText;
        uaSo.FindProperty("playNowBtn").objectReferenceValue = playNowButton;
        uaSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(canvasGo);
        EditorUtility.SetDirty(hud);
        EditorUtility.SetDirty(ua);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvasGo;

        Debug.Log("[Restore UI Canvas] Canvas and HUD references restored. Save the scene to keep changes.");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static RectTransform EnsureRect(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            RectTransform existingRect = existing as RectTransform;
            if (existingRect != null) return existingRect;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static RectTransform EnsureImage(string name, Transform parent, Color color)
    {
        RectTransform rt = EnsureRect(name, parent);
        Image img = EnsureComponent<Image>(rt.gameObject);
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI EnsureText(
        string name,
        Transform parent,
        string text,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        RectTransform rt = EnsureRect(name, parent);
        TextMeshProUGUI tmp = EnsureComponent<TextMeshProUGUI>(rt.gameObject);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    private static AudioSource EnsureAudioSource(string name, Transform parent)
    {
        RectTransform rt = EnsureRect(name, parent);
        AudioSource src = EnsureComponent<AudioSource>(rt.gameObject);
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        return src;
    }

    private static GameObject EnsureButton(string name, Transform parent)
    {
        RectTransform rt = EnsureImage(name, parent, new Color(0.18f, 0.72f, 0.28f, 0.95f));
        Button btn = EnsureComponent<Button>(rt.gameObject);
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.18f, 0.72f, 0.28f, 0.95f);
        cb.highlightedColor = new Color(0.22f, 0.82f, 0.32f, 1f);
        cb.pressedColor = new Color(0.15f, 0.6f, 0.24f, 1f);
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;

        TextMeshProUGUI label = EnsureText("Label", rt, "PLAY NOW", 42, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(label.rectTransform);
        return rt.gameObject;
    }

    private static void SetRect(
        RectTransform rt,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private static AudioClip FindAudioClip(params string[] tokens)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            for (int t = 0; t < tokens.Length; t++)
            {
                if (lower.Contains(tokens[t].ToLowerInvariant()))
                {
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }
        }

        if (guids.Length > 0)
        {
            string fallbackPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(fallbackPath);
        }

        return null;
    }
}
