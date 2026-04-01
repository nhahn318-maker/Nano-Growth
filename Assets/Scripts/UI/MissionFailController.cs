using System.Collections;
using TMPro;
using NanoGrowth;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MissionFailController : MonoBehaviour
{
    private static MissionFailController instance;

    [Header("Fail Trigger")]
    [SerializeField] private float pollInterval = 0.08f;
    [SerializeField] private bool requirePreviouslyAliveMass = true;
    [SerializeField] private bool debugLog = false;

    [Header("Fail Flow")]
    [SerializeField] private string failMessage = "MISSION FAILED";
    [SerializeField] private float controlLockDuration = 0.2f;
    [SerializeField] private bool pauseGameOnFail = true;
    [SerializeField] private bool clearGameplayVfxOnFail = false;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource failSfxSource;
    [SerializeField] private AudioClip failSfxClip;

    private SwarmController swarm;
    private bool hasEverHadNano;
    private bool failStarted;
    private float nextPollTime;

    private Canvas overlayCanvas;
    private GameObject overlayRoot;
    private TMP_Text titleText;
    private Button replayButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindObjectOfType<MissionFailController>() != null) return;

        GameObject go = new GameObject("MissionFailController");
        go.AddComponent<MissionFailController>();
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
        if (failStarted) return;
        if (Time.unscaledTime < nextPollTime) return;

        nextPollTime = Time.unscaledTime + Mathf.Max(0.02f, pollInterval);

        if (swarm == null)
        {
            swarm = FindObjectOfType<SwarmController>();
            if (swarm == null) return;
        }

        int currentMass = Mathf.Max(0, swarm.CurrentNanoMass);
        if (currentMass > 0)
        {
            hasEverHadNano = true;
            return;
        }

        if (requirePreviouslyAliveMass && !hasEverHadNano) return;

        StartCoroutine(PlayFailSequence());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BeginTrackingForCurrentScene();
    }

    private void BeginTrackingForCurrentScene()
    {
        StopAllCoroutines();
        EnableMissionCompleteControllers();

        Time.timeScale = 1f;
        failStarted = false;
        hasEverHadNano = false;
        swarm = null;

        BuildOverlayUiIfNeeded();
        HideOverlay();

        StartCoroutine(FindSwarmNextFrame());
    }

    private IEnumerator FindSwarmNextFrame()
    {
        yield return null;

        swarm = FindObjectOfType<SwarmController>();
        if (swarm != null && swarm.CurrentNanoMass > 0)
        {
            hasEverHadNano = true;
        }

        if (debugLog)
        {
            string swarmName = swarm != null ? swarm.name : "<null>";
            Debug.Log($"[MissionFail] Tracking started. Swarm: {swarmName}");
        }

        nextPollTime = Time.unscaledTime + Mathf.Max(0.02f, pollInterval);
    }

    private IEnumerator PlayFailSequence()
    {
        failStarted = true;

        DisableMissionCompleteControllers();
        SetPlayerControlEnabled(false);
        PlayFailSfx();

        float wait = Mathf.Clamp(controlLockDuration, 0f, 5f);
        if (wait > 0f)
        {
            yield return new WaitForSecondsRealtime(wait);
        }

        if (clearGameplayVfxOnFail)
        {
            StopAndClearGameplayVfx();
        }

        if (pauseGameOnFail)
        {
            Time.timeScale = 0f;
        }

        EnsureEventSystemExists();
        ShowOverlay();
    }

    private void DisableMissionCompleteControllers()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;
            if (behaviour.GetType().Name != "MissionCompleteController") continue;

            behaviour.enabled = false;
        }
    }

    private void EnableMissionCompleteControllers()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;
            if (behaviour.GetType().Name != "MissionCompleteController") continue;

            behaviour.enabled = true;
        }
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

        GameObject canvasGo = new GameObject("MissionFailCanvas");
        canvasGo.transform.SetParent(transform, false);

        overlayCanvas = canvasGo.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 5100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new GameObject("MissionFailOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        overlayRoot = panelGo;

        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.76f);
        panelImage.raycastTarget = true;

        titleText = CreateTMP(
            "MissionFailTitle",
            panelGo.transform,
            failMessage,
            92f,
            new Color(1f, 0.34f, 0.32f, 1f),
            FontStyles.Bold,
            TextAlignmentOptions.Center
        );

        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1500f, 180f);
        titleRect.anchoredPosition = new Vector2(0f, 100f);

        replayButton = CreateButton(panelGo.transform, "FailReplayButton", "Replay", new Vector2(0f, -60f), OnReplayPressed);
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
        GameObject buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(280f, 92f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.25f, 0.1f, 0.12f, 0.95f);

        Button button = buttonGo.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.25f, 0.1f, 0.12f, 0.95f);
        colors.highlightedColor = new Color(0.36f, 0.14f, 0.16f, 1f);
        colors.pressedColor = new Color(0.19f, 0.08f, 0.09f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.55f);
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TMP_Text labelText = CreateTMP(
            objectName + "_Label",
            buttonGo.transform,
            label,
            44f,
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
        if (titleText != null) titleText.text = failMessage;
        if (replayButton != null) replayButton.gameObject.SetActive(true);
        overlayRoot.SetActive(true);
    }

    private void HideOverlay()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void PlayFailSfx()
    {
        if (failSfxSource == null || failSfxClip == null) return;
        failSfxSource.PlayOneShot(failSfxClip);
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
}
