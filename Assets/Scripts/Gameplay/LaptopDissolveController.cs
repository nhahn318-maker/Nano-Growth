using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace NanoGrowth
{
    public class LaptopDissolveController : MonoBehaviour
    {
        [Header("References")]
        public MeshRenderer laptopRenderer;
        public ParticleSystem liftParticles;

        [Header("Swarm Absorption")]
        public float pullSpeed = 8f;
        public float shrinkSpeed = 3f;
        public int growthAmount = 250;
        public float dissolveDuration = 0.8f;

        [Header("Visual Settings")]
        public string shaderProperty = "_DissolveAmount";
        public string edgeColorProperty = "_EdgeColor";

        private List<Material> laptopMaterials = new List<Material>();
        private bool isDissolving = false;
        private Transform swarmTarget;

        void Start()
        {
            if (laptopRenderer == null) laptopRenderer = GetComponent<MeshRenderer>();
            
            if (laptopRenderer != null)
            {
                laptopMaterials.Clear();
                laptopMaterials.AddRange(laptopRenderer.materials);

                if (liftParticles != null && laptopMaterials.Count > 0)
                {
                    Color edgeColor = laptopMaterials[0].GetColor(edgeColorProperty);
                    var main = liftParticles.main;
                    main.startColor = edgeColor;
                }
            }

            if (liftParticles != null)
            {
                liftParticles.Stop();
                var main = liftParticles.main;
                main.duration = dissolveDuration;
            }
        }

        void Update()
        {
            if (isDissolving && swarmTarget != null)
            {
                // HÚT THẲNG VÀO TÂM (No Spiral)
                transform.position = Vector3.Lerp(transform.position, swarmTarget.position, Time.deltaTime * pullSpeed);
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * shrinkSpeed);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            bool isSwarm = other.GetComponentInParent<SwarmController>() != null || 
                          other.CompareTag("Swarm") || 
                          other.CompareTag("Player");

            if (!isDissolving && isSwarm)
            {
                swarmTarget = other.transform;
                StartDissolve();
                
                // RUNG MÀN HÌNH (Impact)
                if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.2f, 0.3f);
            }
        }

        public void StartDissolve()
        {
            if (isDissolving) return;
            if (laptopMaterials.Count > 0)
            {
                StartCoroutine(DissolveRoutine());
            }
        }

        IEnumerator DissolveRoutine()
        {
            isDissolving = true;
            float elapsedTime = 0;
            float randomRot = Random.Range(-300f, 300f);

            if (liftParticles != null) liftParticles.Play();

            while (elapsedTime < dissolveDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / dissolveDuration);
                
                // TĂNG CƯỜNG ĐỘ SÁNG (Glow Peak)
                // Khi laptop tan biến, viền sẽ sáng rực lên tạo cảm giác năng lượng bùng nổ
                float glowIntensity = Mathf.Sin(progress * Mathf.PI) * 10f + 1f;

                foreach (var mat in laptopMaterials)
                {
                    mat.SetFloat(shaderProperty, progress);
                    // Đẩy độ sáng Emission lên cao
                    if (mat.HasProperty(edgeColorProperty))
                    {
                        Color baseColor = mat.GetColor(edgeColorProperty);
                        mat.SetColor(edgeColorProperty, baseColor * glowIntensity);
                    }
                }

                transform.Rotate(Vector3.up * randomRot * Time.deltaTime);

                if (liftParticles != null)
                {
                    var emission = liftParticles.emission;
                    emission.rateOverTime = Mathf.Lerp(100, 1500, progress); 
                }
                
                yield return null;
            }

            SwarmController swarm = FindObjectOfType<SwarmController>();
            if (swarm != null) swarm.Grow(growthAmount);

            foreach (var mat in laptopMaterials) mat.SetFloat(shaderProperty, 1.0f);
            
            if (liftParticles != null)
            {
                var emission = liftParticles.emission;
                emission.rateOverTime = 0;
                liftParticles.Stop();
                liftParticles.transform.SetParent(null);
            }

            if (laptopRenderer != null) laptopRenderer.enabled = false;
            Destroy(gameObject, 5f);
        }
    }
}




