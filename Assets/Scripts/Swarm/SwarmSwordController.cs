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
        [SerializeField] private SwarmController swarmController;
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

        [Header("Dynamic Sword Scale")]
        [SerializeField] private float minSwordScale = 1f;
        [SerializeField] private float maxSwordScale = 4f;
        [SerializeField] private float swordScaleExponent = 0.9f;
        [SerializeField] private int swordScaleMinMass = 2500;
        [SerializeField] private int swordScaleMaxMass = 6000;
        [SerializeField] private float autoMinSwordScale = 2.5f;
        [SerializeField] private float autoMaxSwordScale = 8.5f;
        [SerializeField] private float autoHardCapSwordScale = 14f;

        [Header("Visual Feedback")]
        [SerializeField] private float slashShakeAmount = 0.4f;
        [SerializeField] private float slashShakeDuration = 0.15f;
        [SerializeField] private Color bladeColor = new Color(0.2f, 0.8f, 1f, 1f); // Cyan neon

        [Header("Audio")]
        [SerializeField] private AudioSource slashSfxSource;
        [SerializeField] private AudioClip slashSfxClip;
        [SerializeField] private AudioClip slashHitSfxClip;
        [SerializeField, Range(0f, 1f)] private float slashSfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float slashHitSfxVolume = 1f;

        // Internal state
        private bool isActive = false;
        private bool isSlashing = false;
        private float lastSlashTime = -999f;
        private float currentBladeAngle = 0f;       // Góc hiện tại của kiếm (Y-axis)
        private int slashDirection = 1;              // 1 = phải, -1 = trái (đổi chiều mỗi nhát)
        private Quaternion bladeBaseRotation;
        private float originalEmissionRate = 0f;
        private float originalEmissionRateOverDistance = 0f;
        private bool originalEmissionEnabled = true;
        private int originalMaxParticles = 0;
        private int swordParticleCount = 0;
        private float baseBladeLength;
        private float baseBladeWidth;
        private float baseBladeThickness;
        private float baseSlashRadius;
        private float baseSlashTrailWidthMultiplier;
        private float baseSlashTrailTime;
#pragma warning disable 0414
        private bool firstFrame = false; // Teleport ngay frame đầu
#pragma warning restore 0414

        private void Start()
        {
            // Auto-find nếu chưa gán trong Inspector
            if (swarmCenter == null) swarmCenter = transform;
            if (swarmController == null)
            {
                swarmController = GetComponent<SwarmController>();
                if (swarmController == null) swarmController = GetComponentInParent<SwarmController>();
            }
            if (swarmParticles == null)
            {
                SwarmController sc = GetComponent<SwarmController>();
                if (sc == null) sc = GetComponentInParent<SwarmController>();
                if (sc != null) swarmParticles = GetComponentInChildren<ParticleSystem>();
            }
            if (slashTrail == null) slashTrail = GetComponentInChildren<TrailRenderer>();

            baseBladeLength = bladeLength;
            baseBladeWidth = bladeWidth;
            baseBladeThickness = bladeThickness;
            baseSlashRadius = slashRadius;
            if (slashTrail != null)
            {
                baseSlashTrailWidthMultiplier = slashTrail.widthMultiplier;
                baseSlashTrailTime = slashTrail.time;
            }

            Debug.Log($"[SwordController] Start - Particles: {swarmParticles != null}, Center: {swarmCenter != null}, Trail: {slashTrail != null}");
        }

        // ─────────────────────────────── PUBLIC API ───────────────────────────────

        /// <summary>Bật Sword Mode, gọi từ SwarmMorphController</summary>
        public void ActivateSword()
        {
            ApplySwordScaleFromSwarm();

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

                // Dùng số hạt đã lưu trong hệ (maxParticles) để tránh bị khóa nhầm về 1
                originalMaxParticles = Mathf.Max(1, main.maxParticles);

                // Bù hạt còn thiếu trước khi khóa emission để thanh kiếm không "rụng" về ít hạt
                int currentLiveCount = swarmParticles.particleCount;
                if (currentLiveCount < originalMaxParticles)
                {
                    swarmParticles.Emit(originalMaxParticles - currentLiveCount);
                }

                // XÓA BỎ GIỚI HẠN TUỔI THỌ: Các hạt sẽ CHẾT nếu hết lifetime, làm thanh kiếm biến mất
                // Phải cho tất cả hạt sống bất tử (9999s) trong lúc làm hình dạng kiếm
                ParticleSystem.Particle[] particles = new ParticleSystem.Particle[swarmParticles.particleCount];
                int count = swarmParticles.GetParticles(particles);
                for (int i = 0; i < count; i++)
                {
                    particles[i].startLifetime = 9999f;
                    particles[i].remainingLifetime = 9999f;
                }
                swarmParticles.SetParticles(particles, count);

                // Lưu và khóa cứng maxParticles theo số đã tích lũy, không theo count tức thời
                swordParticleCount = originalMaxParticles;
                main.maxParticles = swordParticleCount; // KHÔNG THỂ sinh thêm

                // XÓA TÍNH NĂNG COLLISION VÀ EXTERNAL FORCES: 
                // Khi bật Sword, hạt phải quét qua tường/vật thể mà không bị Engine Unity "giết chết" (Action: Kill)
                var collision = swarmParticles.collision;
                if (collision.enabled)
                {
                    // Lợi dụng bộ nhớ tạm của class để khôi phục sau
                    PlayerPrefs.SetInt("SwarmColEnabled", 1);
                    collision.enabled = false;
                }
                else
                {
                    PlayerPrefs.SetInt("SwarmColEnabled", 0);
                }

                var force = swarmParticles.externalForces;
                if (force.enabled)
                {
                    PlayerPrefs.SetInt("SwarmForceEnabled", 1);
                    force.enabled = false;
                }
                else
                {
                    PlayerPrefs.SetInt("SwarmForceEnabled", 0);
                }

                // Tắt mọi nguồn emission
                var emission = swarmParticles.emission;
                originalEmissionEnabled = emission.enabled;
                originalEmissionRate = emission.rateOverTime.constant;
                originalEmissionRateOverDistance = emission.rateOverDistance.constant;
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 0f;
                emission.enabled = false;

                // Tắt cả sub-emitters nếu có
                var subEmitters = swarmParticles.subEmitters;
                subEmitters.enabled = false;

                Debug.Log($"[Sword] Locked particles at {swordParticleCount}");
            }
        }

        private void ApplySwordScaleFromSwarm()
        {
            if (swarmController == null) return;

            float initialRadius = Mathf.Max(swarmController.InitialSwarmRadius, 0.01f);
            float radiusRatio = swarmController.CurrentSwarmRadius / initialRadius;
            float radiusScale = Mathf.Pow(radiusRatio, swordScaleExponent);

            float minMass = Mathf.Max(1f, swordScaleMinMass);
            float maxMass = Mathf.Max(minMass + 1f, swordScaleMaxMass);
            float currentMass = Mathf.Max(0f, swarmController.CurrentNanoMass);
            float massLerp = Mathf.InverseLerp(minMass, maxMass, currentMass);
            float massScale = Mathf.Lerp(autoMinSwordScale, autoMaxSwordScale, massLerp);

            // Sau mốc maxMass, tiếp tục nở thêm để 6000+ nhìn đã hơn.
            if (currentMass > maxMass)
            {
                float extraRatio = (currentMass - maxMass) / maxMass;
                massScale *= (1f + extraRatio * 0.9f);
            }

            float desiredScale = Mathf.Max(radiusScale, massScale);
            float effectiveMin = Mathf.Max(minSwordScale, autoMinSwordScale);

            // Khi Swarm quá lớn (6000+), không được chặn kiếm ở hard-cap thấp.
            // Scale trần sẽ nới theo radiusRatio để kiếm giữ tỷ lệ với Swarm.
            float dynamicCapFromRadius = Mathf.Max(autoHardCapSwordScale, radiusRatio * 1.1f);
            float effectiveMax = Mathf.Max(effectiveMin, Mathf.Max(maxSwordScale, dynamicCapFromRadius));
            float scale = Mathf.Clamp(desiredScale, effectiveMin, effectiveMax);

            bladeLength = baseBladeLength * scale;
            bladeWidth = baseBladeWidth * scale;
            bladeThickness = baseBladeThickness * scale;
            slashRadius = baseSlashRadius * scale;
            if (slashTrail != null)
            {
                float trailBase = baseSlashTrailWidthMultiplier > 0.0001f ? baseSlashTrailWidthMultiplier : 1f;
                slashTrail.widthMultiplier = trailBase * scale;

                float trailTimeBase = baseSlashTrailTime > 0.0001f ? baseSlashTrailTime : 0.15f;
                float trailTimeScale = Mathf.Clamp(scale * 0.55f, 1f, 3.5f);
                slashTrail.time = trailTimeBase * trailTimeScale;
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
                main.startColor = Color.cyan; // Hoặc màu gốc của Swarm (đang mặc định là cyan)

                var emission = swarmParticles.emission;
                emission.enabled = originalEmissionEnabled;
                emission.rateOverTime = originalEmissionRate;
                emission.rateOverDistance = originalEmissionRateOverDistance;

                // KHÔI PHỤC COLLISION
                if (PlayerPrefs.GetInt("SwarmColEnabled", 0) == 1)
                {
                    var collision = swarmParticles.collision;
                    collision.enabled = true;
                }
                
                if (PlayerPrefs.GetInt("SwarmForceEnabled", 0) == 1)
                {
                    var force = swarmParticles.externalForces;
                    force.enabled = true;
                }

                var subEmitters = swarmParticles.subEmitters;
                subEmitters.enabled = true;
            }
        }

        // ─────────────────────────────── UPDATE ───────────────────────────────

        private void Update()
        {
            if (!isActive) return;

            // Re-scale theo NanoMass/Radius liên tục để kiếm to lên ngay khi đang hấp thụ.
            ApplySwordScaleFromSwarm();
            SyncSlashTrailToBladeTip();
            ShapeParticlesIntoBlade();
            HandleSlashInput();
        }

        private void SyncSlashTrailToBladeTip()
        {
            if (slashTrail == null || swarmCenter == null) return;

            Vector3 bladeCenter = swarmCenter.position + Vector3.up * bladeHoverHeight;
            Vector3 bladeTip = bladeCenter + swarmCenter.forward * bladeLength;

            slashTrail.transform.position = bladeTip;
            slashTrail.transform.rotation = swarmCenter.rotation;
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
                // Nhắm hướng chém theo vị trí click/touch hiện tại.
                if (TryGetAimPoint(out Vector3 aimPoint))
                {
                    Vector3 toAim = aimPoint - swarmCenter.position;
                    toAim.y = 0f;
                    if (toAim.sqrMagnitude > 0.0001f)
                    {
                        currentBladeAngle = Quaternion.LookRotation(toAim.normalized, Vector3.up).eulerAngles.y;
                    }
                }
                StartCoroutine(SlashRoutine());
            }
        }

        private IEnumerator SlashRoutine()
        {
            isSlashing = true;
            lastSlashTime = Time.time;
            bladeBaseRotation = Quaternion.Euler(0f, currentBladeAngle, 0f);
            PlaySlashSfx();

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
            int hitCount = DetectSlashHits();
            PlaySlashHitSfx(hitCount);

            // Camera shake nhẹ khi chém trúng nhiều mục tiêu.
            if (CameraFollow.Instance != null)
            {
                if (hitCount >= 3)
                {
                    CameraFollow.Instance.Shake(Mathf.Max(0.1f, slashShakeDuration), slashShakeAmount * 0.8f);
                }
                else if (hitCount >= 2)
                {
                    CameraFollow.Instance.Shake(Mathf.Max(0.08f, slashShakeDuration * 0.6f), slashShakeAmount * 0.55f);
                }
            }

            // Đổi chiều chém cho nhát tiếp theo (trái ↔ phải)
            slashDirection *= -1;

            // Cooldown ngắn
            yield return new WaitForSeconds(0.05f);
            isSlashing = false;
        }

        private void PlaySlashSfx()
        {
            if (slashSfxClip == null) return;

            if (slashSfxSource != null)
            {
                slashSfxSource.pitch = Random.Range(0.96f, 1.08f);
                slashSfxSource.PlayOneShot(slashSfxClip, slashSfxVolume);
                return;
            }

            AudioSource.PlayClipAtPoint(slashSfxClip, swarmCenter.position, slashSfxVolume);
        }

        private void PlaySlashHitSfx(int hitCount)
        {
            if (hitCount <= 0 || slashHitSfxClip == null) return;

            float pitch = Mathf.Clamp(1f + hitCount * 0.04f, 1f, 1.2f);

            if (slashSfxSource != null)
            {
                slashSfxSource.pitch = pitch;
                slashSfxSource.PlayOneShot(slashHitSfxClip, slashHitSfxVolume);
                return;
            }

            AudioSource.PlayClipAtPoint(slashHitSfxClip, swarmCenter.position, slashHitSfxVolume);
        }

        private bool TryGetAimPoint(out Vector3 aimPoint)
        {
            aimPoint = swarmCenter.position + swarmCenter.forward * 3f;

            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 screenPos = Input.mousePosition;
            if (Input.touchCount > 0) screenPos = Input.GetTouch(0).position;

            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, swarmCenter.position);
            if (groundPlane.Raycast(ray, out float enter))
            {
                aimPoint = ray.GetPoint(enter);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tìm tất cả Robot trong tầm chém và gây sát thương.
        /// Dùng OverlapSphere + kiểm tra góc để tạo vùng hình quạt.
        /// </summary>
        private int DetectSlashHits()
        {
            Collider[] hits = Physics.OverlapSphere(swarmCenter.position, slashRadius);
            int hitCount = 0;
            HashSet<RobotEnemy> hitRobots = new HashSet<RobotEnemy>();

            foreach (Collider hit in hits)
            {
                RobotEnemy robot = hit.GetComponent<RobotEnemy>();
                if (robot == null) robot = hit.GetComponentInParent<RobotEnemy>();

                if (robot != null)
                {
                    if (hitRobots.Contains(robot)) continue;

                    // Kiểm tra robot có nằm trong "cung chém" không.
                    // QUAN TRỌNG: Dùng hướng cơ bản (trước khi xoay) thay vì hướng lúc vung xong
                    Vector3 forwardDir = bladeBaseRotation * Vector3.forward;
                    Vector3 dirToRobot = (robot.transform.position - swarmCenter.position).normalized;
                    float angle = Vector3.Angle(forwardDir, dirToRobot);

                    if (angle <= slashArc * 0.5f)
                    {
                        hitRobots.Add(robot);
                        robot.TakeDamage(slashDamage);
                        hitCount++;

                        // Spawn hiệu ứng va chạm tại vị trí Robot
                        if (slashImpactFX != null)
                        {
                            slashImpactFX.transform.position = robot.transform.position;
                            slashImpactFX.Play();
                        }
                    }
                }
            }

            return hitCount;
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
