using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NanoGrowth
{
    /// <summary>
    /// Step 1: Handles the movement of the Nano Swarm following the Mouse/Touch.
    /// </summary>
    public class SwarmController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float smoothSpeed = 1000f; 
        [SerializeField] private float moveSpeed = 8f; // Tốc độ di chuyển khi đẩy Joystick
        [SerializeField] private Joystick joystick; // Dùng class Joystick từ Joystick Pack
        
        [Header("Swarm References")]
        [SerializeField] private ParticleSystem swarmParticles;
        [SerializeField] private Transform swarmCenter;
        [SerializeField] private float swarmRadius = 0.6f; 
        [SerializeField] private float particlePullForce = 25f; // Tăng thêm để hạt bám theo cực nhanh
        
        [Header("Organic Sway (Lấy lư tự nhiên)")]
        [SerializeField] private float swaySpeed = 0.8f; // Nhịp điệu chậm rãi
        [SerializeField] private float swayAmount = 0.3f; // Độ lắc nhẹ nhàng

        private Camera mainCam;
        private Vector3 targetPosition;

        private float currentSwaySpeed;
        private float currentSwayAmount;

        private SwarmMorphController morphController;

        private void Start()
        {
            if (swarmCenter == null) swarmCenter = this.transform;
            targetPosition = swarmCenter.position;
            
            currentSwaySpeed = swaySpeed;
            currentSwayAmount = swayAmount;

            morphController = GetComponent<SwarmMorphController>();
            if (morphController == null) morphController = GetComponentInParent<SwarmMorphController>();

            SetupPhysics();
        }

        private void SetupPhysics()
        {
            SphereCollider col = swarmCenter.GetComponent<SphereCollider>();
            if (col == null) col = swarmCenter.gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = swarmRadius;
            col.center = Vector3.up * 1.5f;

            Rigidbody rb = swarmCenter.GetComponent<Rigidbody>();
            if (rb == null) rb = swarmCenter.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (swarmCenter.tag == "Untagged") swarmCenter.tag = "Player";
        }

        private void OnDrawGizmosSelected()
        {
            if (swarmCenter != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(swarmCenter.position + Vector3.up * 1.5f, swarmRadius);
            }
        }

        private void Update()
        {
            HandleInput();
            LerpMovement();

            // Chỉ attract hình cầu khi ở Swarm Mode (không đánh nhau với Sword/Vortex)
            bool isSwarmMode = morphController == null || morphController.CurrentMode == SwarmMorphController.MorphMode.Swarm;
            if (isSwarmMode)
            {
                AttractParticles();
            }
        }

        private void AttractParticles()
        {
            if (swarmParticles == null) return;

            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[swarmParticles.particleCount];
            int count = swarmParticles.GetParticles(particles);

            Vector3 centerPos = swarmCenter.position + Vector3.up * 1.5f;

            for (int i = 0; i < count; i++)
            {
                Random.InitState((int)particles[i].randomSeed);
                Vector3 randomOffset = Random.insideUnitSphere * swarmRadius; 

                float seed = particles[i].randomSeed;
                Vector3 sway = new Vector3(
                    Mathf.PerlinNoise(seed, Time.time * currentSwaySpeed) - 0.5f,
                    Mathf.PerlinNoise(seed + 100, Time.time * currentSwaySpeed) - 0.5f,
                    Mathf.PerlinNoise(seed + 200, Time.time * currentSwaySpeed) - 0.5f
                ) * currentSwayAmount;

                Vector3 individualTarget = centerPos + randomOffset + sway;
                Vector3 desiredVelocity = (individualTarget - particles[i].position) * particlePullForce;
                particles[i].velocity = Vector3.Lerp(particles[i].velocity, desiredVelocity, Time.deltaTime * 15f);
            }

            swarmParticles.SetParticles(particles, count);
        }

        private void HandleInput()
        {
            Vector3 inputDir = Vector3.zero;
            if (joystick != null) inputDir = new Vector3(joystick.Direction.x, 0, joystick.Direction.y);
            else inputDir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

            if (inputDir.sqrMagnitude > 0.01f) targetPosition += inputDir * moveSpeed * Time.deltaTime;
        }

        private void LerpMovement()
        {
            swarmCenter.position = Vector3.Lerp(swarmCenter.position, targetPosition, Time.deltaTime * smoothSpeed);
        }

        public void Grow(int amount)
        {
            var main = swarmParticles.main;
            main.maxParticles += amount;
            swarmParticles.Emit(amount);
            
            StopCoroutine("GrowPulse");
            StartCoroutine(GrowPulse());

            // TRẠNG THÁI HÀO HỨNG: Swarm sôi sục khi ăn
            StopCoroutine("ExcitementRoutine");
            StartCoroutine(ExcitementRoutine());

            swarmRadius += 0.05f;
            SphereCollider col = swarmCenter.GetComponent<SphereCollider>();
            if (col != null) col.radius = swarmRadius;
            
            moveSpeed += 0.1f;
        }

        private IEnumerator ExcitementRoutine()
        {
            // Tăng tốc độ rung lắc của các hạt
            currentSwaySpeed = swaySpeed * 3f;
            currentSwayAmount = swayAmount * 2f;
            
            yield return new WaitForSeconds(1.0f); // Sôi sục trong 1 giây

            // Trở về bình thường
            float elapsed = 0;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                currentSwaySpeed = Mathf.Lerp(swaySpeed * 3f, swaySpeed, elapsed);
                currentSwayAmount = Mathf.Lerp(swayAmount * 2f, swayAmount, elapsed);
                yield return null;
            }
        }

        private IEnumerator GrowPulse()
        {
            Vector3 baseScale = swarmCenter.localScale;
            Vector3 targetScale = baseScale + Vector3.one * 0.15f;
            
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                swarmCenter.localScale = Vector3.Lerp(baseScale, targetScale, elapsed / duration);
                yield return null;
            }

            Vector3 finalScale = baseScale + Vector3.one * 0.05f;
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                swarmCenter.localScale = Vector3.Lerp(targetScale, finalScale, elapsed / duration);
                yield return null;
            }
            swarmCenter.localScale = finalScale;
        }
    }
}
