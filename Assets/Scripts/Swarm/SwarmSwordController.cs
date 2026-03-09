using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NanoGrowth
{
    /// <summary>
    /// Sword Mode: Các hạt nano nén lại thành hình lưỡi kiếm năng lượng.
    /// Khi chém (Slash), kiếm quét ngang gây sát thương lên Robot.
    /// </summary>
    public class SwarmSwordController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParticleSystem swarmParticles;
        [SerializeField] private Transform swarmCenter;
        [SerializeField] private TrailRenderer slashTrail;         // Trail khi chém
        [SerializeField] private ParticleSystem slashImpactFX;     // Tia lửa khi chạm Robot
        [SerializeField] private ParticleSystem bladeGlowFX;       // Tia điện chạy dọc lưỡi kiếm

        [Header("Blade Shape")]
        [SerializeField] private float bladeLength = 2.5f;         // Chiều dài lưỡi kiếm (ngắn hơn → đặc hơn)
        [SerializeField] private float bladeWidth = 0.15f;         // Chiều rộng (hơi rộng hơn → đặc hơn)
        [SerializeField] private float bladeThickness = 0.04f;     // Chiều dày
        [SerializeField] private float formationSpeed = 25f;       // Tốc độ hạt nén
        [SerializeField] private float bladeHoverHeight = 1.5f;    // Độ cao so với mặt đất
        [SerializeField] private float tipTaper = 0.7f;            // Từ đâu kiếm bắt đầu nhọn (0-1)

        [Header("Slash Settings")]
        [SerializeField] private float slashDuration = 0.25f;      // Thời gian 1 nhát chém
        [SerializeField] private float slashArc = 150f;            // Góc quét (độ)
        [SerializeField] private float slashCooldown = 0.4f;       // Thời gian hồi giữa 2 nhát
        [SerializeField] private float slashDamage = 50f;          // Sát thương mỗi nhát
        [SerializeField] private float slashRadius = 3.5f;         // Tầm chém

        [Header("Visual Feedback")]
        [SerializeField] private float slashShakeAmount = 0.4f;
        [SerializeField] private float slashShakeDuration = 0.15f;
        [SerializeField] private Color bladeColor = new Color(0.2f, 0.8f, 1f, 1f); // Cyan neon

        // Internal state
        private bool isActive = false;
        private bool isSlashing = false;
        private float lastSlashTime = -999f;
        private float currentBladeAngle = 0f;       // Góc hiện tại của kiếm (Y-axis)
        private int slashDirection = 1;              // 1 = phải, -1 = trái (đổi chiều mỗi nhát)
        private Quaternion bladeBaseRotation;
        private float originalEmissionRate = 0f;
        private int originalMaxParticles = 0;
        private int swordParticleCount = 0;
#pragma warning disable 0414
        private bool firstFrame = false; // Teleport ngay frame đầu
#pragma warning restore 0414

        private void Start()
        {
            // Auto-find nếu chưa gán trong Inspector
            if (swarmCenter == null) swarmCenter = transform;
            if (swarmParticles == null)
            {
                SwarmController sc = GetComponent<SwarmController>();
                if (sc == null) sc = GetComponentInParent<SwarmController>();
                if (sc != null) swarmParticles = GetComponentInChildren<ParticleSystem>();
            }
            if (slashTrail == null) slashTrail = GetComponentInChildren<TrailRenderer>();

            Debug.Log($"[SwordController] Start - Particles: {swarmParticles != null}, Center: {swarmCenter != null}, Trail: {slashTrail != null}");
        }

        // ─────────────────────────────── PUBLIC API ───────────────────────────────

        /// <summary>Bật Sword Mode, gọi từ SwarmMorphController</summary>
        public void ActivateSword()
        {
            isActive = true;
            firstFrame = true;
            Debug.Log($"[SwordController] ACTIVATED! Particles: {swarmParticles != null}, count: {(swarmParticles != null ? swarmParticles.particleCount : 0)}");
            currentBladeAngle = swarmCenter.eulerAngles.y;
            bladeBaseRotation = swarmCenter.rotation;

            if (slashTrail != null) { slashTrail.Clear(); slashTrail.emitting = false; }
            if (bladeGlowFX != null) bladeGlowFX.Play();

            // Đổi màu hạt sang màu kiếm + KHÓA CỨNG SỐ HẠT
            if (swarmParticles != null)
            {
                var main = swarmParticles.main;
                main.startColor = bladeColor;

                // Lưu và khóa cứng maxParticles = số hạt hiện tại
                swordParticleCount = swarmParticles.particleCount;
                originalMaxParticles = main.maxParticles;
                main.maxParticles = swordParticleCount; // KHÔNG THỂ sinh thêm

                // Tắt mọi nguồn emission
                var emission = swarmParticles.emission;
                originalEmissionRate = emission.rateOverTime.constant;
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 0f;
                emission.enabled = false;

                // Tắt cả sub-emitters nếu có
                var subEmitters = swarmParticles.subEmitters;
                subEmitters.enabled = false;

                Debug.Log($"[Sword] Locked particles at {swordParticleCount}");
            }
        }

        /// <summary>Tắt Sword Mode</summary>
        public void DeactivateSword()
        {
            isActive = false;
            isSlashing = false;

            if (slashTrail != null) slashTrail.emitting = false;
            if (bladeGlowFX != null) bladeGlowFX.Stop();

            // KHÔI PHỤC khi về Swarm Mode
            if (swarmParticles != null)
            {
                var main = swarmParticles.main;
                main.maxParticles = originalMaxParticles;

                var emission = swarmParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = originalEmissionRate;

                var subEmitters = swarmParticles.subEmitters;
                subEmitters.enabled = true;
            }
        }

        // ─────────────────────────────── UPDATE ───────────────────────────────

        private void Update()
        {
            if (!isActive) return;

            ShapeParticlesIntoBlade();
            HandleSlashInput();
        }

        // ─────────────────────────────── BLADE SHAPE ───────────────────────────────

        /// <summary>
        /// Tạo hình kiếm hoàn chỉnh với 3 phần: CÁN + THANH CHẮN + LƯỠI KIẾM NHỌN
        /// 
        ///    ╔══╗
        ///    ║  ║  ← Cán kiếm (Handle) - 10% hạt
        ///  ══╬══╬══  ← Thanh chắn (Guard) - 5% hạt
        ///    ┃  ┃
        ///    ┃  ┃  ← Lưỡi kiếm (Blade) - 85% hạt
        ///    ┃  ┃
        ///     ╲╱   ← Mũi nhọn
        /// </summary>
        private void ShapeParticlesIntoBlade()
        {
            if (swarmParticles == null) return;

            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[swarmParticles.particleCount];
            int count = swarmParticles.GetParticles(particles);
            if (count == 0) return;

            Vector3 centerPos = swarmCenter.position + Vector3.up * bladeHoverHeight;

            Vector3 bladeForward = swarmCenter.forward;
            Vector3 bladeRight = swarmCenter.right;
            Vector3 bladeUp = swarmCenter.up;

            // Kích thước các phần
            float handleLength = bladeLength * 0.12f;  // Cán ngắn hơn
            float guardWidth = bladeWidth * 4f;         // Thanh chắn vừa phải
            float guardThickness = 0.06f;               // Thanh chắn mỏng hơn

            for (int i = 0; i < count; i++)
            {
                Random.InitState((int)particles[i].randomSeed);

                // Phân loại hạt: 15% cán, 10% thanh chắn, 75% lưỡi
                float role = Random.Range(0f, 1f);
                Vector3 targetPos;
                float seed = particles[i].randomSeed;

                if (role < 0.15f)
                {
                    // ═══ CÁN KIẾM (Handle) ═══
                    float posAlong = Random.Range(0f, 1f) * handleLength;
                    float handleRadius = 0.06f; // Nhỏ hơn → đặc hơn

                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float radius = Mathf.Pow(Random.Range(0f, 1f), 0.5f) * handleRadius; // Sqrt → phân bố đều trong vòng tròn
                    float px = Mathf.Cos(angle) * radius;
                    float py = Mathf.Sin(angle) * radius;

                    targetPos = centerPos
                        - bladeForward * posAlong   // Sát ngay sau center
                        + bladeRight * px
                        + bladeUp * py;
                }
                else if (role < 0.25f)
                {
                    // ═══ THANH CHẮN (Crossguard) ═══
                    float posAcross = Random.Range(-0.5f, 0.5f) * guardWidth;
                    float posThick = Random.Range(-0.5f, 0.5f) * guardThickness;

                    targetPos = centerPos
                        + bladeRight * posAcross
                        + bladeUp * posThick;
                }
                else
                {
                    // ═══ LƯỠI KIẾM (Blade) ═══
                    // Dài, mỏng, nhọn mũi
                    float t = Random.Range(0f, 1f);
                    float posAlongBlade = t * bladeLength;

                    // Nhọn mũi: thu hẹp dần từ tipTaper
                    float taperFactor = 1f;
                    if (t > tipTaper)
                    {
                        taperFactor = 1f - ((t - tipTaper) / (1f - tipTaper));
                        taperFactor = taperFactor * taperFactor;
                    }

                    // Ép hạt sát trục giữa (power curve)
                    float rawW = Random.Range(-1f, 1f);
                    float concentrated = Mathf.Sign(rawW) * Mathf.Pow(Mathf.Abs(rawW), 2.5f);
                    float posAcross = concentrated * bladeWidth * taperFactor;

                    float rawH = Random.Range(-1f, 1f);
                    float concentratedH = Mathf.Sign(rawH) * Mathf.Pow(Mathf.Abs(rawH), 3f);
                    float posHeight = concentratedH * bladeThickness * taperFactor;

                    targetPos = centerPos
                        + bladeForward * posAlongBlade
                        + bladeRight * posAcross
                        + bladeUp * posHeight;
                }

                // Rung năng lượng nhẹ chạy dọc kiếm
                float wave = Mathf.Sin(Time.time * 8f + seed * 0.01f) * 0.015f;
                targetPos += bladeRight * wave;

                // Dùng Lerp trực tiếp vào Position thay vì Velocity
                // Tránh việc ParticleSystem tự đẩy hạt ra xa do startSpeed
                if (firstFrame)
                {
                    particles[i].position = targetPos;
                    particles[i].velocity = Vector3.zero;
                }
                else
                {
                    particles[i].position = Vector3.Lerp(particles[i].position, targetPos, Time.deltaTime * formationSpeed);
                    particles[i].velocity = Vector3.zero; // Khóa cứng velocity của hệ thống tự nhiên
                }
            }

            swarmParticles.SetParticles(particles, count);
            if (firstFrame) firstFrame = false;
        }

        // ─────────────────────────────── SLASH ───────────────────────────────

        private void HandleSlashInput()
        {
            if (isSlashing) return;

            // Input: Click chuột hoặc chạm màn hình
            bool slashPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);

            if (slashPressed && Time.time - lastSlashTime >= slashCooldown)
            {
                StartCoroutine(SlashRoutine());
            }
        }

        private IEnumerator SlashRoutine()
        {
            isSlashing = true;
            lastSlashTime = Time.time;

            // Bật Trail
            if (slashTrail != null) { slashTrail.Clear(); slashTrail.emitting = true; }

            float elapsed = 0f;
            float startAngle = currentBladeAngle - (slashArc * 0.5f * slashDirection);
            float endAngle = currentBladeAngle + (slashArc * 0.5f * slashDirection);

            // Ease-in-out curve cho cảm giác kiếm "nặng"
            // Bắt đầu chậm → tăng tốc ở giữa → chậm lại ở cuối
            while (elapsed < slashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slashDuration);

                // Smooth-step cho chuyển động tự nhiên
                float smoothT = t * t * (3f - 2f * t);
                float angle = Mathf.Lerp(startAngle, endAngle, smoothT);

                // Xoay swarmCenter (kiếm xoay theo)
                swarmCenter.rotation = Quaternion.Euler(0, angle, 0);

                yield return null;
            }

            // Tắt Trail
            if (slashTrail != null) slashTrail.emitting = false;

            // Detect va chạm trong vùng chém
            DetectSlashHits();

            // Camera Shake
            if (CameraFollow.Instance != null)
                CameraFollow.Instance.Shake(slashShakeDuration, slashShakeAmount);

            // Đổi chiều chém cho nhát tiếp theo (trái ↔ phải)
            slashDirection *= -1;

            // Cooldown ngắn
            yield return new WaitForSeconds(0.05f);
            isSlashing = false;
        }

        /// <summary>
        /// Tìm tất cả Robot trong tầm chém và gây sát thương.
        /// Dùng OverlapSphere + kiểm tra góc để tạo vùng hình quạt.
        /// </summary>
        private void DetectSlashHits()
        {
            Collider[] hits = Physics.OverlapSphere(swarmCenter.position, slashRadius);

            foreach (Collider hit in hits)
            {
                RobotEnemy robot = hit.GetComponent<RobotEnemy>();
                if (robot == null) robot = hit.GetComponentInParent<RobotEnemy>();

                if (robot != null)
                {
                    // Kiểm tra robot có nằm trong "cung chém" không.
                    // QUAN TRỌNG: Dùng hướng cơ bản (trước khi xoay) thay vì hướng lúc vung xong
                    Vector3 forwardDir = bladeBaseRotation * Vector3.forward;
                    Vector3 dirToRobot = (robot.transform.position - swarmCenter.position).normalized;
                    float angle = Vector3.Angle(forwardDir, dirToRobot);

                    if (angle <= slashArc * 0.5f)
                    {
                        robot.TakeDamage(slashDamage);

                        // Spawn hiệu ứng va chạm tại vị trí Robot
                        if (slashImpactFX != null)
                        {
                            slashImpactFX.transform.position = robot.transform.position;
                            slashImpactFX.Play();
                        }
                    }
                }
            }
        }

        // ─────────────────────────────── GIZMOS ───────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (swarmCenter == null) return;

            // Vẽ vùng chém (hình quạt)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(swarmCenter.position, slashRadius);

            // Vẽ hình kiếm
            Gizmos.color = Color.cyan;
            Vector3 center = swarmCenter.position + Vector3.up * bladeHoverHeight;
            Vector3 tip = center + swarmCenter.forward * bladeLength * 0.5f;
            Vector3 hilt = center - swarmCenter.forward * bladeLength * 0.5f;
            Gizmos.DrawLine(hilt, tip);
            Gizmos.DrawWireCube(center, new Vector3(bladeWidth, bladeWidth * 0.5f, bladeLength));
        }
    }
}
