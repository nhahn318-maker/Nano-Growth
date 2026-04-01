using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NanoGrowth;

public class MissionCompleteController : MonoBehaviour
{
    private static MissionCompleteController instance;

    [Header("Target Tracking")]
    [SerializeField] private bool autoCollectTargets = true;
    [SerializeField] private bool includeInactiveTargetsAtStart = false;
    [SerializeField] private float pollInterval = 0.08f;
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool trackAbsorbables = false;
    [SerializeField] private bool trackSciFiProps = false;
    [SerializeField] private bool trackRobots = true;
    [SerializeField] private bool trackBreakableWalls = false;

    [Header("Completion Flow")]
    [SerializeField] private string completeMessage = "MISSION COMPLETE";
    [SerializeField] private float controlLockDuration = 0.2f;
    [SerializeField] private bool pauseGameOnComplete = true;
    [SerializeField] private bool clearGameplayVfxOnComplete = true;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource completeSfxSource;
    [SerializeField] private AudioClip completeSfxClip;

    private readonly List<GameObject> trackedTargets = new List<GameObject>();

    private bool trackingReady;
    private bool trackingArmed;
    private bool completionStarted;
    private float nextPollTime;

    private Canvas overlayCanvas;
    private GameObject overlayRoot;
    private TMP_Text titleText;
    private Button replayButton;
    private Button nextButton;
    private TMP_Text nextButtonLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindObjectOfType<MissionCompleteController>() != null) return;

        GameObject go = new GameObject("MissionCompleteController");
        go.AddComponent<MissionCompleteController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        BuildOverlayUiIfNeeded();
        HideOverlay();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        BeginTrackingForCurrentScene();
    }

    private void Update()
    {
        if (!trackingReady || completionStarted) return;
        if (Time.unscaledTime < nextPollTime) return;

        nextPollTime = Time.unscaledTime + Mathf.Max(0.02f, pollInterval);

        if (HasAnyActiveTrackableTargets())
        {
            trackingArmed = true;
        }

        // Tránh complete sớm khi target chưa được bật (ví dụ robot ở zone khóa).
        if (!trackingArmed) return;

        int remaining = CountRemainingTargets();
        if (remaining > 0) return;

        StartCoroutine(PlayCompletionSequence());
    }

    [ContextMenu("Force Mission Complete")]
    private void ForceMissionComplete()
    {
        if (!completionStarted)
        {
            StartCoroutine(PlayCompletionSequence());
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BeginTrackingForCurrentScene();
    }

    private void BeginTrackingForCurrentScene()
    {
        StopAllCoroutines();

        Time.timeScale = 1f;
        completionStarted = false;
        trackingReady = false;
        trackingArmed = false;
        trackedTargets.Clear();

        BuildOverlayUiIfNeeded();
        HideOverlay();

        if (autoCollectTargets)
        {
            StartCoroutine(CollectTargetsNextFrame());
        }
        else
        {
            trackingReady = true;
            nextPollTime = Time.unscaledTime + Mathf.Max(0.02f, pollInterval);
        }
    }

    private IEnumerator CollectTargetsNextFrame()
    {
        yield return null;

        CollectTargets();

        trackingReady = true;
        nextPollTime = Time.unscaledTime + Mathf.Max(0.02f, pollInterval);

        if (debugLog)
        {
            Debug.Log($"[MissionComplete] Tracking started. Total targets: {trackedTargets.Count}");
        }

        if (debugLog && trackedTargets.Count == 0)
        {
            Debug.LogWarning("[MissionComplete] No targets found. Overlay will not trigger.");
        }
    }

    private void CollectTargets()
    {
        HashSet<GameObject> uniqueTargets = new HashSet<GameObject>();

        if (trackAbsorbables)
        {
            AddTargets(FindObjectsOfType<Absorbable>(includeInactiveTargetsAtStart), uniqueTargets);
        }

        if (trackSciFiProps)
        {
            AddTargets(FindObjectsOfType<SciFiPropDissolveController>(includeInactiveTargetsAtStart), uniqueTargets);
        }

        if (trackRobots)
        {
            AddTargets(FindObjectsOfType<RobotEnemy>(includeInactiveTargetsAtStart), uniqueTargets);
        }

        if (trackBreakableWalls)
        {
            AddTargets(FindObjectsOfType<BreakableWall>(includeInactiveTargetsAtStart), uniqueTargets);
        }

        trackedTargets.Clear();
        foreach (GameObject target in uniqueTargets)
        {
            if (target == null) continue;
            if (!target.scene.IsValid()) continue;
            if (!includeInactiveTargetsAtStart && !target.activeInHierarchy) continue;

            trackedTargets.Add(target);
        }
    }

    private void AddTargets<T>(T[] components, HashSet<GameObject> targetSet) where T : Component
    {
        if (components == null) return;

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null) continue;
            targetSet.Add(components[i].gameObject);
        }
    }

    private int CountRemainingTargets()
    {
        int remaining = 0;
        HashSet<GameObject> visited = new HashSet<GameObject>();

        for (int i = 0; i < trackedTargets.Count; i++)
        {
            GameObject target = trackedTargets[i];
            if (target == null) continue;
            visited.Add(target);
            if (IsTargetResolved(target)) continue;

            remaining++;
        }

        if (trackRobots)
        {
            remaining += CountDynamicRemaining(FindObjectsOfType<RobotEnemy>(true), visited);
        }

        if (trackBreakableWalls)
        {
            remaining += CountDynamicRemaining(FindObjectsOfType<BreakableWall>(true), visited);
        }

        if (trackAbsorbables)
        {
            remaining += CountDynamicRemaining(FindObjectsOfType<Absorbable>(true), visited);
        }

        if (trackSciFiProps)
        {
            remaining += CountDynamicRemaining(FindObjectsOfType<SciFiPropDissolveController>(true), visited);
        }

        return remaining;
    }

    private bool HasAnyActiveTrackableTargets()
    {
        for (int i = 0; i < trackedTargets.Count; i++)
        {
            GameObject target = trackedTargets[i];
            if (target == null) continue;
            if (!target.activeInHierarchy) continue;
            if (IsTargetResolved(target)) continue;
            return true;
        }

        if (trackRobots && HasAnyActiveUnresolved(FindObjectsOfType<RobotEnemy>(true))) return true;
        if (trackBreakableWalls && HasAnyActiveUnresolved(FindObjectsOfType<BreakableWall>(true))) return true;
        if (trackAbsorbables && HasAnyActiveUnresolved(FindObjectsOfType<Absorbable>(true))) return true;
        if (trackSciFiProps && HasAnyActiveUnresolved(FindObjectsOfType<SciFiPropDissolveController>(true))) return true;

        return false;
    }

    private static bool HasAnyActiveUnresolved<T>(T[] components) where T : Component
    {
        if (components == null) return false;

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null) continue;

            GameObject go = components[i].gameObject;
            if (go == null) continue;
            if (!go.scene.IsValid()) continue;
            if (!go.activeInHierarchy) continue;
            if (IsTargetResolved(go)) continue;

            return true;
        }

        return false;
    }

    private static int CountDynamicRemaining<T>(T[] components, HashSet<GameObject> visited) where T : Component
    {
        if (components == null) return 0;

        int remaining = 0;
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null) continue;

            GameObject go = components[i].gameObject;
            if (go == null) continue;
            if (!go.scene.IsValid()) continue;
            if (!go.activeInHierarchy) continue;
            if (!visited.Add(go)) continue;
            if (IsTargetResolved(go)) continue;

            remaining++;
        }

        return remaining;
    }

    private static bool IsTargetResolved(GameObject target)
    {
        if (target == null) return true;
        if (!target.activeInHierarchy) return true;

        RobotEnemy robot = target.GetComponent<RobotEnemy>();
        if (robot != null && robot.IsDefeated) return true;

        BreakableWall wall = target.GetComponent<BreakableWall>();
        if (wall != null && wall.IsBroken) return true;

        return false;
    }

    private IEnumerator PlayCompletionSequence()
    {
        completionStarted = true;

        SetPlayerControlEnabled(false);
        PlayCompleteSfx();

        float wait = Mathf.Clamp(controlLockDuration, 0f, 5f);
        if (wait > 0f)
        {
            yield return new WaitForSecondsRealtime(wait);
        }

        if (clearGameplayVfxOnComplete)
        {
            StopAndClearGameplayVfx();
        }

        if (pauseGameOnComplete)
        {
            Time.timeScale = 0f;
        }

        EnsureEventSystemExists();
        ShowOverlay();
    }

    private void SetPlayerControlEnabled(bool enabled)
    {
        SwarmController[] swarmControllers = FindObjectsOfType<SwarmController>(true);
        for (int i = 0; i < swarmControllers.Length; i++)
        {
            if (swarmControllers[i] != null) swarmControllers[i].enabled = enabled;
        }

        SwarmMorphController[] morphControllers = FindObjectsOfType<SwarmMorphController>(true);
        for (int i = 0; i < morphControllers.Length; i++)
        {
            if (morphControllers[i] != null) morphControllers[i].enabled = enabled;
        }

        SwarmSwordController[] swordControllers = FindObjectsOfType<SwarmSwordController>(true);
        for (int i = 0; i < swordControllers.Length; i++)
        {
            if (swordControllers[i] != null) swordControllers[i].enabled = enabled;
        }
    }

    private void StopAndClearGameplayVfx()
    {
        ParticleSystem[] particleSystems = FindObjectsOfType<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null) continue;
            if (overlayCanvas != null && ps.transform.IsChildOf(overlayCanvas.transform)) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        TrailRenderer[] trails = FindObjectsOfType<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            TrailRenderer trail = trails[i];
            if (trail == null) continue;
            if (overlayCanvas != null && trail.transform.IsChildOf(overlayCanvas.transform)) continue;
            trail.Clear();
            trail.enabled = false;
        }
    }

    private void BuildOverlayUiIfNeeded()
    {
        if (overlayCanvas != null) return;

        GameObject canvasGo = new GameObject("MissionOverlayCanvas");
        canvasGo.transform.SetParent(transform, false);

        overlayCanvas = canvasGo.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new GameObject("MissionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        overlayRoot = panelGo;

        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = true;

        titleText = CreateTMP(
            "MissionTitle",
            panelGo.transform,
            completeMessage,
            96,
            new Color(1f, 0.93f, 0.28f, 1f),
            FontStyles.Bold,
            TextAlignmentOptions.Center
        );

        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1500f, 180f);
        titleRect.anchoredPosition = new Vector2(0f, 120f);

        replayButton = CreateButton(panelGo.transform, "ReplayButton", "Replay", new Vector2(-180f, -70f), OnReplayPressed);
        nextButton = CreateButton(panelGo.transform, "NextButton", "Next", new Vector2(180f, -70f), OnNextPressed, out nextButtonLabel);
    }

    private TMP_Text CreateTMP(
        string objectName,
        Transform parent,
        string content,
        float fontSize,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject textGo = new GameObject(objectName, typeof(RectTransform));
        textGo.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        TMP_Text sample = FindObjectOfType<TMP_Text>(true);
        if (sample != null && sample.font != null)
        {
            tmp.font = sample.font;
            tmp.fontSharedMaterial = sample.fontSharedMaterial;
        }

        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;

        return tmp;
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction onClick)
    {
        return CreateButton(parent, objectName, label, anchoredPosition, onClick, out _);
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction onClick,
        out TMP_Text labelText)
    {
        GameObject buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(260f, 88f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.08f, 0.13f, 0.2f, 0.95f);

        Button button = buttonGo.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.13f, 0.2f, 0.95f);
        colors.highlightedColor = new Color(0.15f, 0.22f, 0.34f, 1f);
        colors.pressedColor = new Color(0.05f, 0.09f, 0.14f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.55f);
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        labelText = CreateTMP(
            objectName + "_Label",
            buttonGo.transform,
            label,
            44,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center
        );

        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private void ShowOverlay()
    {
        if (overlayRoot == null) return;

        if (titleText != null)
        {
            titleText.text = completeMessage;
        }

        bool hasNextScene = SceneManager.GetActiveScene().buildIndex >= 0
                            && SceneManager.GetActiveScene().buildIndex + 1 < SceneManager.sceneCountInBuildSettings;

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(hasNextScene);
        }

        if (nextButtonLabel != null && hasNextScene)
        {
            nextButtonLabel.text = "Next";
        }

        overlayRoot.SetActive(true);
    }

    private void HideOverlay()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void PlayCompleteSfx()
    {
        if (completeSfxSource == null || completeSfxClip == null) return;
        completeSfxSource.PlayOneShot(completeSfxClip);
    }

    private void EnsureEventSystemExists()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }

    private void OnReplayPressed()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void OnNextPressed()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        int nextSceneIndex = activeScene.buildIndex + 1;
        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneIndex);
    }
}
