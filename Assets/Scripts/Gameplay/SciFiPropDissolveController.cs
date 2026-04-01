using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace NanoGrowth
{
    public class SciFiPropDissolveController : MonoBehaviour
    {
        [Header("References")]
        public MeshRenderer propRenderer; // Đổi tên biến cho đúng ngữ cảnh
        public ParticleSystem liftParticles;

        [Header("Swarm Absorption")]
        public float pullSpeed = 8f;
        public float shrinkSpeed = 3f;
        public int growthAmount = 250;
        public int requiredNanoMass = 0; // Thêm check tiến trình
        public float dissolveDuration = 0.8f;
        [Tooltip("Bật nếu swarm đang đứng trên vật này — sau khi hấp thụ xong swarm sẽ tự hạ xuống nền")]
        public bool snapToGroundOnAbsorb = false;

        [Header("Visual Settings")]
        public string shaderProperty = "_DissolveAmount";
        public string edgeColorProperty = "_EdgeColor";

        private List<Material> propMaterials = new List<Material>();
        private bool isDissolving = false;
        private Transform swarmTarget;

        void Start()
        {
            if (propRenderer == null) propRenderer = GetComponent<MeshRenderer>();
            
            if (propRenderer != null)
            {
                propMaterials.Clear();
                propMaterials.AddRange(propRenderer.materials);

                if (liftParticles != null && propMaterials.Count > 0)
                {
                    if (propMaterials[0].HasProperty(edgeColorProperty))
                    {
                        Color edgeColor = propMaterials[0].GetColor(edgeColorProperty);
                        var main = liftParticles.main;
                        main.startColor = edgeColor;
                    }
                }
            }

            if (liftParticles != null)
            {
                liftParticles.Stop();
                var main = liftParticles.main;
                main.duration = dissolveDuration;

                // Fix "Particle System is trying to spawn on a mesh with zero surface area"
                // Shape module trong Inspector có thể để trống (None) nên cần gán lại tại runtime
                if (propRenderer != null)
                {
                    var shape = liftParticles.shape;
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                    shape.meshRenderer = propRenderer;
                }
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
            TryAbsorb(other);
        }

        // Fix: Swarm đứng sẵn trên vật thể, sau khi đạt đủ mass thì cần OnTriggerStay
        // để kiểm tra lại điều kiện mà không cần phải ra vào lại trigger
        private void OnTriggerStay(Collider other)
        {
            TryAbsorb(other);
        }

        private void TryAbsorb(Collider other)
        {
            if (isDissolving) return;

            bool isSwarm = other.GetComponentInParent<SwarmController>() != null || 
                          other.CompareTag("Swarm") || 
                          other.CompareTag("Player");

            if (!isSwarm) return;

            SwarmController swarm = other.GetComponentInParent<SwarmController>();
            if (swarm == null) swarm = FindObjectOfType<SwarmController>();

            if (swarm != null)
            {
                if (swarm.CurrentNanoMass >= requiredNanoMass)
                {
                    swarmTarget = other.transform;
                    StartDissolve();
                    
                    // RUNG MÀN HÌNH (Impact)
                    if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.2f, 0.3f);
                }
                else
                {
                    // Chỉ log trong OnTriggerEnter để tránh spam console mỗi frame
                    // Debug.Log($"[-] Swarm quá nhỏ để nuốt Prop này. Cần: {requiredNanoMass}, Hiện có: {swarm.CurrentNanoMass}");
                }
            }
        }

        public void StartDissolve()
        {
            if (isDissolving) return;
            if (propMaterials.Count > 0)
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
                // Khi vật thể tan biến, viền sẽ sáng rực lên tạo cảm giác năng lượng bùng nổ
                float glowIntensity = Mathf.Sin(progress * Mathf.PI) * 10f + 1f;

                foreach (var mat in propMaterials)
                {
                    if (mat.HasProperty(shaderProperty))
                    {
                        mat.SetFloat(shaderProperty, progress);
                    }
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
            Debug.Log($"[Dissolve DONE] '{gameObject.name}' | swarm={(swarm != null ? "found" : "NULL")} | snapToGroundOnAbsorb={snapToGroundOnAbsorb}");

            if (swarm != null)
            {
                swarm.Grow(growthAmount);
                SwarmHUD.Instance?.RegisterAbsorb(transform.position, growthAmount);
                if (snapToGroundOnAbsorb)
                {
                    Debug.Log("[Dissolve] → Gọi SnapToGround()");
                    swarm.SnapToGround();
                }
                else
                {
                    Debug.Log("[Dissolve] → snapToGroundOnAbsorb=FALSE, không gọi SnapToGround. Tick checkbox trong Inspector nếu muốn swarm hạ xuống.");
                }
            }



            foreach (var mat in propMaterials)
            {
                if (mat.HasProperty(shaderProperty))
                {
                    mat.SetFloat(shaderProperty, 1.0f);
                }
            }
            
            if (liftParticles != null)
            {
                var emission = liftParticles.emission;
                emission.rateOverTime = 0;
                liftParticles.Stop();
                liftParticles.transform.SetParent(null);
            }

            if (propRenderer != null) propRenderer.enabled = false;
            Destroy(gameObject, 5f);
        }
    }
}




