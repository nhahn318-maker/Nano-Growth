using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NanoGrowth
{
    public class SciFiPropDissolveController : MonoBehaviour
    {
        [Header("References")]
        public MeshRenderer propRenderer;
        public ParticleSystem liftParticles;

        [Header("Swarm Absorption")]
        public float pullSpeed = 8f;
        public float shrinkSpeed = 3f;
        public int growthAmount = 250;
        public int requiredNanoMass = 0;
        public float dissolveDuration = 0.8f;
        [Tooltip("Enable if the swarm stands on this prop and should snap to ground after absorb.")]
        public bool snapToGroundOnAbsorb = false;

        [Header("Visual Settings")]
        public string shaderProperty = "_DissolveAmount";
        public string edgeColorProperty = "_EdgeColor";

        [Header("Dissolve Particles")]
        [SerializeField] private float baseEmissionStartRate = 100f;
        [SerializeField] private float baseEmissionPeakRate = 1500f;
        [SerializeField] private float referenceObjectSize = 1f;
        [SerializeField] private float minSizeRateMultiplier = 0.75f;
        [SerializeField] private float maxSizeRateMultiplier = 12f;
        [SerializeField] private float sizeResponseExponent = 1.6f;
        [SerializeField] private bool scaleByGrowthAmount = true;
        [SerializeField] private float growthReference = 100f;
        [SerializeField] private float growthResponseExponent = 0.65f;
        [SerializeField] private float minGrowthMultiplier = 0.7f;
        [SerializeField] private float maxGrowthMultiplier = 3f;
        [SerializeField] private int baseMaxParticles = 3000;
        [SerializeField] private int maxParticlesCap = 20000;
        [SerializeField] private bool debugScaleLog = false;

        private readonly List<Material> propMaterials = new List<Material>();
        private bool isDissolving = false;
        private Transform swarmTarget;

        private float emissionScale = 1f;
        private float scaledStartRate;
        private float scaledPeakRate;

        private void Start()
        {
            if (propRenderer == null) propRenderer = GetComponent<MeshRenderer>();

            if (propRenderer != null)
            {
                propMaterials.Clear();
                propMaterials.AddRange(propRenderer.materials);

                if (liftParticles != null && propMaterials.Count > 0 && propMaterials[0].HasProperty(edgeColorProperty))
                {
                    Color edgeColor = propMaterials[0].GetColor(edgeColorProperty);
                    var main = liftParticles.main;
                    main.startColor = edgeColor;
                }
            }

            if (liftParticles != null)
            {
                liftParticles.Stop();
                var main = liftParticles.main;
                main.duration = dissolveDuration;

                if (propRenderer != null)
                {
                    var shape = liftParticles.shape;
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                    shape.meshRenderer = propRenderer;
                }
            }

            RecalculateParticleScaling();
        }

        private void Update()
        {
            if (isDissolving && swarmTarget != null)
            {
                transform.position = Vector3.Lerp(transform.position, swarmTarget.position, Time.deltaTime * pullSpeed);
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * shrinkSpeed);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryAbsorb(other);
        }

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

                    if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.2f, 0.3f);
                }
            }
        }

        public void StartDissolve()
        {
            if (isDissolving) return;
            if (propMaterials.Count > 0)
            {
                RecalculateParticleScaling();
                StartCoroutine(DissolveRoutine());
            }
        }

        private IEnumerator DissolveRoutine()
        {
            isDissolving = true;
            float elapsedTime = 0f;
            float randomRot = Random.Range(-300f, 300f);

            if (liftParticles != null) liftParticles.Play();

            while (elapsedTime < dissolveDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / dissolveDuration);
                float glowIntensity = Mathf.Sin(progress * Mathf.PI) * 10f + 1f;

                for (int i = 0; i < propMaterials.Count; i++)
                {
                    Material mat = propMaterials[i];
                    if (mat.HasProperty(shaderProperty))
                    {
                        mat.SetFloat(shaderProperty, progress);
                    }

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
                    emission.rateOverTime = Mathf.Lerp(scaledStartRate, scaledPeakRate, progress);
                }

                yield return null;
            }

            SwarmController swarm = FindObjectOfType<SwarmController>();
            if (swarm != null)
            {
                swarm.Grow(growthAmount);
                SwarmHUD.Instance?.RegisterAbsorb(transform.position, growthAmount);

                if (snapToGroundOnAbsorb)
                {
                    swarm.SnapToGround();
                }
            }

            for (int i = 0; i < propMaterials.Count; i++)
            {
                Material mat = propMaterials[i];
                if (mat.HasProperty(shaderProperty))
                {
                    mat.SetFloat(shaderProperty, 1f);
                }
            }

            if (liftParticles != null)
            {
                var emission = liftParticles.emission;
                emission.rateOverTime = 0f;
                liftParticles.Stop();
                liftParticles.transform.SetParent(null);
            }

            if (propRenderer != null) propRenderer.enabled = false;
            Destroy(gameObject, 5f);
        }

        private void RecalculateParticleScaling()
        {
            float sizeMetric = GetObjectSizeMetric();
            float safeReference = Mathf.Max(0.01f, referenceObjectSize);
            float rawSizeRatio = Mathf.Max(0.01f, sizeMetric / safeReference);

            float sizeMultiplier = Mathf.Pow(rawSizeRatio, Mathf.Max(0.01f, sizeResponseExponent));
            sizeMultiplier = Mathf.Clamp(
                sizeMultiplier,
                Mathf.Max(0.01f, minSizeRateMultiplier),
                Mathf.Max(minSizeRateMultiplier, maxSizeRateMultiplier));

            float growthMultiplier = 1f;
            if (scaleByGrowthAmount)
            {
                float safeGrowthRef = Mathf.Max(1f, growthReference);
                float rawGrowthRatio = Mathf.Max(0.01f, growthAmount / safeGrowthRef);
                growthMultiplier = Mathf.Pow(rawGrowthRatio, Mathf.Max(0.01f, growthResponseExponent));
                growthMultiplier = Mathf.Clamp(
                    growthMultiplier,
                    Mathf.Max(0.01f, minGrowthMultiplier),
                    Mathf.Max(minGrowthMultiplier, maxGrowthMultiplier));
            }

            emissionScale = sizeMultiplier * growthMultiplier;

            scaledStartRate = Mathf.Max(0f, baseEmissionStartRate * emissionScale);
            scaledPeakRate = Mathf.Max(scaledStartRate, baseEmissionPeakRate * emissionScale);

            if (liftParticles != null)
            {
                var main = liftParticles.main;
                int scaledMax = Mathf.CeilToInt(baseMaxParticles * emissionScale);
                scaledMax = Mathf.Clamp(scaledMax, 1, Mathf.Max(1, maxParticlesCap));
                main.maxParticles = scaledMax;
            }

            if (debugScaleLog)
            {
                Debug.Log(
                    $"[DissolveParticles] {name} sizeMetric={sizeMetric:F2} scale={emissionScale:F2} " +
                    $"start={scaledStartRate:F0} peak={scaledPeakRate:F0} growthAmount={growthAmount}");
            }
        }

        private float GetObjectSizeMetric()
        {
            if (propRenderer != null)
            {
                Bounds b = propRenderer.bounds;
                float volume = Mathf.Max(0.0001f, b.size.x * b.size.y * b.size.z);
                return Mathf.Pow(volume, 1f / 3f);
            }

            Vector3 s = transform.lossyScale;
            float fallbackVolume = Mathf.Max(0.0001f, s.x * s.y * s.z);
            return Mathf.Pow(fallbackVolume, 1f / 3f);
        }
    }
}
