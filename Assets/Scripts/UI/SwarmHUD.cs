using TMPro;
using UnityEngine;
using System.Collections;
using NanoGrowth;
using UnityEngine.UI;

public class SwarmHUD : MonoBehaviour {
    public static SwarmHUD Instance { get; private set; }

    [Header("Core HUD")]
    public SwarmController swarm;
    public TMP_Text progressText;
    public TMP_Text warningText;
    public TMP_Text unlockText;
    [SerializeField] private Image progressBarFill;
    [SerializeField] private int unlockMassTarget = 3000;

    [Header("Feedback - Combo & Pop")]
    public TMP_Text popTextTemplate;
    public TMP_Text comboText;
    [SerializeField] private float comboWindow = 2.4f;
    [SerializeField] private float comboIdleResetDelay = 5f;
    [SerializeField] private Color popColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color popCombo5Color = new Color(1f, 0.58f, 0.2f, 1f);
    [SerializeField] private Color popCombo10Color = new Color(1f, 0.3f, 0.42f, 1f);
    [SerializeField] private Color comboColor = new Color(1f, 0.92f, 0.22f, 1f);
    [SerializeField] private Color combo5Color = new Color(1f, 0.62f, 0.2f, 1f);
    [SerializeField] private Color combo10Color = new Color(1f, 0.35f, 0.88f, 1f);

    [Header("Feedback - Audio")]
    public AudioSource absorbSfxSource;
    public AudioSource comboSfxSource;
    public AudioClip absorbClip;
    public AudioClip comboUpClip;

    [Header("Unlock Moment")]
    [SerializeField] private float unlockFreezeDuration = 0.12f;
    [SerializeField] private Image unlockFlashOverlay;
    [SerializeField] private float unlockFlashPeakAlpha = 0.8f;
    [SerializeField] private float unlockFlashDuration = 0.2f;
    [SerializeField] private AudioSource unlockSfxSource;
    [SerializeField] private AudioClip unlockSfxClip;

    private bool unlockedShown = false;
    private int comboCount = 0;
    private float lastAbsorbTime = -999f;
    private Coroutine comboPulseRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (popTextTemplate != null) popTextTemplate.gameObject.SetActive(false);
        if (comboText != null) comboText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (swarm == null) return;

        int cur = swarm.CurrentNanoMass;
        int target = Mathf.Max(1, unlockMassTarget);
        progressText.text = $"NANO: {cur}/{target}";

        if (progressBarFill != null)
        {
            float t = Mathf.Clamp01(cur / (float)target);
            if (progressBarFill.type == Image.Type.Filled)
            {
                progressBarFill.fillAmount = t;
            }
            else
            {
                Vector3 s = progressBarFill.rectTransform.localScale;
                s.x = Mathf.Max(0.0001f, t);
                progressBarFill.rectTransform.localScale = s;
            }
        }

        if (comboCount > 0 && Time.time - lastAbsorbTime > comboIdleResetDelay)
        {
            comboCount = 0;
            if (comboText != null)
            {
                if (comboPulseRoutine != null)
                {
                    StopCoroutine(comboPulseRoutine);
                    comboPulseRoutine = null;
                }

                comboText.gameObject.SetActive(false);
                comboText.transform.localScale = Vector3.one;
            }
        }

        if (!unlockedShown && cur >= target)
        {
            unlockedShown = true;
            StartCoroutine(PlayUnlockMoment());
        }
    }

    public void ShowNotEnough() => StartCoroutine(ShowTemp(warningText, 0.9f));

    public void RegisterAbsorb(Vector3 worldPos, int growthAmount)
    {
        UpdateCombo();
        SpawnPopText(worldPos, $"+{growthAmount}");
        UpdateComboText();
        PlayAbsorbSfx();
        PlayComboSfxIfNeeded();
    }

    IEnumerator ShowTemp(TMP_Text txt, float t)
    {
        if (txt == null) yield break;
        txt.gameObject.SetActive(true);
        yield return new WaitForSeconds(t);
        txt.gameObject.SetActive(false);
    }

    private void UpdateCombo()
    {
        float effectiveWindow = Mathf.Max(0.15f, comboWindow);
        bool keepCombo = Time.time - lastAbsorbTime <= effectiveWindow;
        comboCount = keepCombo ? comboCount + 1 : 1;
        lastAbsorbTime = Time.time;
    }

    private void UpdateComboText()
    {
        if (comboText == null) return;

        if (comboCount >= 2)
        {
            comboText.gameObject.SetActive(true);
            bool isCombo10 = comboCount >= 10;
            bool isCombo5 = comboCount >= 5;

            string comboLabel = isCombo10 ? " OVERDRIVE" : isCombo5 ? " HOT" : string.Empty;
            Color baseColor = isCombo10 ? combo10Color : isCombo5 ? combo5Color : comboColor;
            float baseScale = Mathf.Lerp(1.05f, 1.4f, Mathf.Clamp01((comboCount - 1) / 8f));
            float pulseScale = isCombo10 ? 1.45f : isCombo5 ? 1.3f : 1.18f;

            comboText.text = $"x{comboCount}{comboLabel}";
            comboText.color = baseColor;
            comboText.transform.localScale = Vector3.one * baseScale;

            if (comboPulseRoutine != null) StopCoroutine(comboPulseRoutine);
            comboPulseRoutine = StartCoroutine(AnimateComboPulse(baseScale, pulseScale, baseColor));
        }
        else
        {
            if (comboPulseRoutine != null)
            {
                StopCoroutine(comboPulseRoutine);
                comboPulseRoutine = null;
            }

            comboText.gameObject.SetActive(false);
        }
    }

    private void SpawnPopText(Vector3 worldPos, string content)
    {
        if (popTextTemplate == null) return;

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos + Vector3.up * 1f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPos))
        {
            return;
        }

        TMP_Text pop = Instantiate(popTextTemplate, popTextTemplate.transform.parent != null ? popTextTemplate.transform.parent : transform);
        RectTransform popRect = pop.GetComponent<RectTransform>();
        if (popRect != null) popRect.anchoredPosition = localPos;
        bool isCombo10 = comboCount >= 10;
        bool isCombo5 = comboCount >= 5;
        float scaleBoost = isCombo10 ? 1.35f : isCombo5 ? 1.18f : 1f;
        string suffix = isCombo10 ? "!!" : isCombo5 ? "!" : string.Empty;

        pop.text = content + suffix;
        pop.color = GetPopColorForCombo();
        pop.gameObject.SetActive(true);

        StartCoroutine(AnimatePopText(pop, scaleBoost));
    }

    private IEnumerator AnimatePopText(TMP_Text pop, float scaleBoost)
    {
        if (pop == null) yield break;

        RectTransform rect = pop.GetComponent<RectTransform>();
        float elapsed = 0f;
        float duration = 0.78f;
        float startScale = 0.9f * scaleBoost;
        float endScale = 1.25f * scaleBoost;
        Vector2 startPos = rect != null ? rect.anchoredPosition : Vector2.zero;
        Vector2 endPos = startPos + Vector2.up * (60f + 18f * (scaleBoost - 1f));

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (rect != null) rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            pop.transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

            Color c = pop.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            pop.color = c;

            yield return null;
        }

        if (pop != null) Destroy(pop.gameObject);
    }

    private Color GetPopColorForCombo()
    {
        if (comboCount >= 10) return popCombo10Color;
        if (comboCount >= 5) return popCombo5Color;
        return popColor;
    }

    private IEnumerator AnimateComboPulse(float baseScale, float pulseScale, Color baseColor)
    {
        if (comboText == null) yield break;

        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float punch = Mathf.Sin(t * Mathf.PI);

            float currentScale = Mathf.Lerp(baseScale, baseScale * pulseScale, punch);
            comboText.transform.localScale = Vector3.one * currentScale;
            comboText.color = Color.Lerp(baseColor, Color.white, punch * 0.35f);

            yield return null;
        }

        comboText.transform.localScale = Vector3.one * baseScale;
        comboText.color = baseColor;
        comboPulseRoutine = null;
    }

    private void PlayAbsorbSfx()
    {
        if (absorbSfxSource == null || absorbClip == null) return;
        absorbSfxSource.pitch = Mathf.Clamp(1f + comboCount * 0.05f, 1f, 1.35f);
        absorbSfxSource.PlayOneShot(absorbClip);
    }

    private void PlayComboSfxIfNeeded()
    {
        if (comboCount < 2) return;
        if (comboSfxSource == null || comboUpClip == null) return;

        if (comboCount == 2 || comboCount == 3 || comboCount == 4 || comboCount % 5 == 0)
        {
            comboSfxSource.pitch = Mathf.Clamp(1f + comboCount * 0.03f, 1f, 1.3f);
            comboSfxSource.PlayOneShot(comboUpClip);
        }
    }

    private IEnumerator PlayUnlockMoment()
    {
        if (unlockSfxSource != null && unlockSfxClip != null)
        {
            unlockSfxSource.pitch = 1f;
            unlockSfxSource.PlayOneShot(unlockSfxClip);
        }

        if (unlockText != null) unlockText.gameObject.SetActive(true);

        // Hit-stop ngắn để cảm giác "unlock đã mắt" khi chạm mốc.
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(Mathf.Clamp(unlockFreezeDuration, 0.05f, 0.25f));
        Time.timeScale = previousTimeScale;

        // Flash trắng toàn màn hình (nếu có gán overlay).
        if (unlockFlashOverlay != null)
        {
            unlockFlashOverlay.gameObject.SetActive(true);
            Color c = unlockFlashOverlay.color;
            c.a = unlockFlashPeakAlpha;
            unlockFlashOverlay.color = c;

            float elapsed = 0f;
            while (elapsed < unlockFlashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, unlockFlashDuration));
                c.a = Mathf.Lerp(unlockFlashPeakAlpha, 0f, t);
                unlockFlashOverlay.color = c;
                yield return null;
            }

            c.a = 0f;
            unlockFlashOverlay.color = c;
            unlockFlashOverlay.gameObject.SetActive(false);
        }

        // Giữ text unlock hiện sau hit-stop + flash.
        yield return StartCoroutine(ShowTemp(unlockText, 0.9f));
    }
}
