using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace NanoGrowth
{
    /// <summary>
    /// Step 7: UI for the UA Video. Handles the 'Catchy' texts and 'Play Now' button.
    /// </summary>
    public class UAEffectController : MonoBehaviour
    {
        [Header("Text Overlays")]
        [SerializeField] private TMP_Text hookText; // "ABSORB TO GROW!"
        [SerializeField] private TMP_Text morphText; // "MORPH AS WEAPONS!"
        [SerializeField] private GameObject playNowBtn;
        
        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private void Start()
        {
            // Reset state
            if (hookText != null) hookText.gameObject.SetActive(false);
            if (morphText != null) morphText.gameObject.SetActive(false);
            if (playNowBtn != null) playNowBtn.SetActive(false);
        }

        private void Update()
        {
            // Video shortcuts (For the UA recording)
            if (Input.GetKeyDown(KeyCode.H)) ShowHook();
            if (Input.GetKeyDown(KeyCode.M)) ShowMorph();
            if (Input.GetKeyDown(KeyCode.P)) ShowPlayNow();
        }

        public void ShowHook()
        {
            StartCoroutine(FadeUi(hookText, 3f));
        }

        public void ShowMorph()
        {
            StartCoroutine(FadeUi(morphText, 3f));
        }

        public void ShowPlayNow()
        {
            if (playNowBtn != null) playNowBtn.SetActive(true);
        }

        private IEnumerator FadeUi(TMP_Text target, float duration)
        {
            // Simple visual pop for video
            target.gameObject.SetActive(true);
            target.transform.localScale = Vector3.zero;
            
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                target.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.2f, elapsed / fadeDuration);
                yield return null;
            }
            
            yield return new WaitForSeconds(duration);
            target.gameObject.SetActive(false);
        }
    }
}
