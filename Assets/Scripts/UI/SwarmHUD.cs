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
    [SerializeField] private float comboWindow = 1.2f;

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
        bool keepCombo = Time.time - lastAbsorbTime <= comboWindow;
        comboCount = keepCombo ? comboCount + 1 : 1;
        lastAbsorbTime = Time.time;
    }

    private void UpdateComboText()
    {
        if (comboText == null) return;

        if (comboCount >= 2)
        {
            comboText.gameObject.SetActive(true);
            comboText.text = $"x{comboCount}";
            float scale = Mathf.Lerp(1f, 1.35f, Mathf.Clamp01((comboCount - 1) / 6f));
            comboText.transform.localScale = Vector3.one * scale;
        }
        else
        {
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
        pop.text = content;
        pop.color = Color.white;
        pop.gameObject.SetActive(true);

        StartCoroutine(AnimatePopText(pop));
    }

    private IEnumerator AnimatePopText(TMP_Text pop)
    {
        if (pop == null) yield break;

        RectTransform rect = pop.GetComponent<RectTransform>();
        float elapsed = 0f;
        float duration = 0.75f;
        float startScale = 0.9f;
        float endScale = 1.25f;
        Vector2 startPos = rect != null ? rect.anchoredPosition : Vector2.zero;
        Vector2 endPos = startPos + Vector2.up * 60f;

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
