using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NanoGrowth
{
    /// <summary>
    /// Step 3: Trigger for growth. When the swarm touches this object, it dissolves and grows the swarm.
    /// </summary>
    public class Absorbable : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float dissolveDuration = 1.5f;
        [SerializeField] public int growthAmount = 100;
        [SerializeField] private int requiredNanoMass = 0; // Lượng hạt nhỏ nhất cần để nuốt vật này
        
        [SerializeField] private float pullSpeed = 5f;
        [SerializeField] private float shrinkSpeed = 2f;
        
        [Header("Visuals (Auto-assigned if empty)")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private ParticleSystem absorbParticles; // Các hạt bay về phía swarm
        private Material mat;
        
        private bool isBeingAbsorbed = false;
        private float dissolveProgress = 0f;
        private Transform swarmTarget;

        private void Start()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                mat = targetRenderer.material;
                // Ensure shader property is initialized correctly
                mat.SetFloat("_DissolveAmount", 0f);
            }

            // Nếu không có hạt, thử tìm trong con
            if (absorbParticles == null) absorbParticles = GetComponentInChildren<ParticleSystem>();
        }

        private void Update()
        {
            if (isBeingAbsorbed && swarmTarget != null)
            {
                // Kéo vật thể về phía tâm swarm
                transform.position = Vector3.Lerp(transform.position, swarmTarget.position, Time.deltaTime * pullSpeed);
                // Co nhỏ vật thể lại
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * shrinkSpeed);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Trigger logic for either the Swarm Center or individual particles (if they have triggers)
            if (!isBeingAbsorbed && (other.CompareTag("Swarm") || other.GetComponent<SwarmController>() != null || other.CompareTag("Player")))
            {
                SwarmController swarm = other.GetComponentInParent<SwarmController>();
                if (swarm == null) swarm = FindObjectOfType<SwarmController>();

                if (swarm != null)
                {
                    // Kiểm tra tiến trình (Progression)
                    if (swarm.CurrentNanoMass >= requiredNanoMass)
                    {
                        swarmTarget = other.transform;
                        StartAbsorb();
                    }
                    else
                    {
                        // Demo mode: Báo log nếu chưa đủ điểm nuốt vật thể lớn
                        Debug.Log($"[-] Swarm quá nhỏ để nuốt '{gameObject.name}'. Cần: {requiredNanoMass}, Hiện có: {swarm.CurrentNanoMass}");
                    }
                }
            }
        }

        public void StartAbsorb()
        {
            if (isBeingAbsorbed) return;
            isBeingAbsorbed = true;

            if (absorbParticles != null) absorbParticles.Play();
            
            StartCoroutine(DissolveSequence());
        }

        private IEnumerator DissolveSequence()
        {
            // Tăng cường cảm giác "hút" bằng cách xoay vật thể nhẹ
            float randomRotation = Random.Range(-100f, 100f);

            // Step-by-step dissolve visual
            while (dissolveProgress < 1f)
            {
                dissolveProgress += Time.deltaTime / dissolveDuration;
                if (mat != null) mat.SetFloat("_DissolveAmount", dissolveProgress);
                
                // Xoay vật thể khi đang bị hút
                transform.Rotate(Vector3.up * randomRotation * Time.deltaTime);
                
                yield return null;
            }

            // Tell the swarm to grow
            SwarmController swarm = FindObjectOfType<SwarmController>();
            if (swarm != null)
            {
                swarm.Grow(growthAmount);
                SwarmHUD.Instance?.RegisterAbsorb(transform.position, growthAmount);
            }

            // Cleanup
            Destroy(gameObject, 0.1f);
        }
    }
}
