using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NanoGrowth
{
    /// <summary>
    /// Robot lính canh — phải dùng Sword Mode chém mới phá hủy được.
    /// Khi HP = 0, robot rã thành nhiều mảnh → mỗi mảnh bị Swarm hút vào (Absorbable).
    /// </summary>
    public class RobotEnemy : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float maxHP = 150f;
        [SerializeField] private int growthAmount = 150;     // Điểm cộng cho Swarm khi phá hủy

        [Header("Hit Feedback")]
        [SerializeField] private float hitFlashDuration = 0.1f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float knockbackForce = 3f;

        [Header("Road Movement")]
        [SerializeField] private Transform[] roadWaypoints;
        [SerializeField] private float roadMoveSpeed = 2.8f;
        [SerializeField] private float waypointReachDistance = 0.2f;
        [SerializeField] private bool loopWaypoints = true;

        [Header("Contact Damage")]
        [SerializeField] private int nanoDamageOnTouch = 2000;
        [SerializeField] private float touchDamageCooldown = 1.0f;

        [Header("Death / Shatter")]
        [SerializeField] private GameObject[] shatterPieces;     // Các mảnh vỡ (prefab hoặc child objects)
        [SerializeField] private float shatterForce = 8f;        // Lực bắn mảnh ra
        [SerializeField] private float shatterAbsorbDelay = 0.8f;// Thời gian chờ trước khi mảnh bị hút
        [SerializeField] private ParticleSystem deathExplosionFX;

        [Header("Visual (Auto)")]
        [SerializeField] private Renderer bodyRenderer;

        private float currentHP;
        private Material bodyMat;
        private Color originalColor;
        private bool isDead = false;
        public bool IsDefeated => isDead;
        private Coroutine flashRoutine;
        private int currentWaypointIndex = 0;
        private float lastTouchDamageTime = -999f;

        private void Start()
        {
            // Yêu cầu gameplay: mỗi lần robot chạm Swarm sẽ trừ đúng 2000 nano.
            nanoDamageOnTouch = 2000;

            currentHP = maxHP;

            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
            if (bodyRenderer != null)
            {
                bodyMat = bodyRenderer.material;
                originalColor = bodyMat.color;
            }

            // Ẩn sẵn các mảnh vỡ
            if (shatterPieces != null)
            {
                foreach (var piece in shatterPieces)
                {
                    if (piece != null) piece.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (isDead) return;
            MoveOnRoad();
        }

        // ─────────────────────────────── DAMAGE ───────────────────────────────

        /// <summary>Gọi từ SwarmSwordController khi bị chém</summary>
        public void TakeDamage(float damage)
        {
            if (isDead) return;

            currentHP -= damage;

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(HitFlash());

            // Knockback nhẹ — bị đẩy ra xa khỏi Swarm
            SwarmController swarm = FindObjectOfType<SwarmController>();
            if (swarm != null)
            {
                Vector3 knockDir = (transform.position - swarm.transform.position).normalized;
                StartCoroutine(KnockbackRoutine(knockDir));
            }

            // Camera shake nhẹ mỗi lần trúng
            if (CameraFollow.Instance != null)
                CameraFollow.Instance.Shake(0.1f, 0.15f);

            if (currentHP <= 0)
            {
                Die();
            }
        }

        public float GetHPPercent() => Mathf.Clamp01(currentHP / maxHP);

        // ─────────────────────────────── HIT FLASH ───────────────────────────────

        private IEnumerator HitFlash()
        {
            if (bodyMat == null) yield break;

            // Nháy trắng
            bodyMat.color = hitFlashColor;
            bodyMat.SetColor("_EmissionColor", hitFlashColor * 3f);

            yield return new WaitForSeconds(hitFlashDuration);

            // Trở về màu gốc với tỉ lệ HP (đỏ hơn khi sắp chết)
            float hpPercent = GetHPPercent();
            Color hurtColor = Color.Lerp(Color.red, originalColor, hpPercent);
            bodyMat.color = hurtColor;
            bodyMat.SetColor("_EmissionColor", Color.black);
        }

        // ─────────────────────────────── KNOCKBACK ───────────────────────────────

        private IEnumerator KnockbackRoutine(Vector3 direction)
        {
            float elapsed = 0f;
            float duration = 0.15f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + direction * knockbackForce * 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Ease-out: bắn ra nhanh rồi chậm dần
                float easeT = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(startPos, endPos, easeT);
                yield return null;
            }
        }

        private void MoveOnRoad()
        {
            if (roadWaypoints == null || roadWaypoints.Length == 0) return;
            Transform target = roadWaypoints[currentWaypointIndex];
            if (target == null) return;

            Vector3 currentPos = transform.position;
            Vector3 targetPos = target.position;
            targetPos.y = currentPos.y;

            Vector3 toTarget = targetPos - currentPos;
            if (toTarget.sqrMagnitude <= waypointReachDistance * waypointReachDistance)
            {
                AdvanceWaypoint();
                return;
            }

            Vector3 moveDir = toTarget.normalized;
            transform.position += moveDir * roadMoveSpeed * Time.deltaTime;
            transform.forward = Vector3.Lerp(transform.forward, moveDir, Time.deltaTime * 8f);
        }

        private void AdvanceWaypoint()
        {
            if (roadWaypoints == null || roadWaypoints.Length == 0) return;

            if (loopWaypoints)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % roadWaypoints.Length;
            }
            else
            {
                currentWaypointIndex = Mathf.Min(currentWaypointIndex + 1, roadWaypoints.Length - 1);
            }
        }

        // ─────────────────────────────── DEATH ───────────────────────────────

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            // Camera shake mạnh khi Robot nổ
            if (CameraFollow.Instance != null)
                CameraFollow.Instance.Shake(0.3f, 0.5f);

            // Hiệu ứng nổ
            if (deathExplosionFX != null)
            {
                deathExplosionFX.transform.SetParent(null);
                deathExplosionFX.Play();
                Destroy(deathExplosionFX.gameObject, 3f);
            }

            // Ẩn body chính
            if (bodyRenderer != null) bodyRenderer.enabled = false;

            // Collider tắt
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Bắn mảnh vỡ ra
            SpawnShatterPieces();

            // Delay rồi xóa
            Destroy(gameObject, 5f);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamageSwarm(other.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            TryDamageSwarm(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDamageSwarm(collision.gameObject);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryDamageSwarm(collision.gameObject);
        }

        private void TryDamageSwarm(GameObject other)
        {
            if (isDead) return;
            if (Time.time - lastTouchDamageTime < touchDamageCooldown) return;

            SwarmController swarm = other.GetComponentInParent<SwarmController>();
            if (swarm == null) swarm = other.GetComponent<SwarmController>();
            if (swarm == null) return;

            swarm.LoseNanoMass(nanoDamageOnTouch);
            lastTouchDamageTime = Time.time;
        }

        /// <summary>
        /// Bật các mảnh vỡ, bắn tung ra bằng lực ngẫu nhiên.
        /// Mỗi mảnh có Absorbable → bị Swarm hút vào sau một khoảng delay.
        /// </summary>
        private void SpawnShatterPieces()
        {
            if (shatterPieces == null || shatterPieces.Length == 0)
            {
                // Nếu không có mảnh vỡ sẵn, tạo cộng trực tiếp cho Swarm
                SwarmController swarm = FindObjectOfType<SwarmController>();
                if (swarm != null)
                {
                    swarm.Grow(growthAmount);
                    SwarmHUD.Instance?.RegisterAbsorb(transform.position, growthAmount);
                }
                return;
            }

            int pointsPerPiece = growthAmount / Mathf.Max(shatterPieces.Length, 1);

            foreach (var piece in shatterPieces)
            {
                if (piece == null) continue;

                // Tách mảnh ra khỏi parent để nó tự do
                piece.transform.SetParent(null);
                piece.SetActive(true);

                // Gắn Rigidbody nếu chưa có → cho mảnh bay tung
                Rigidbody rb = piece.GetComponent<Rigidbody>();
                if (rb == null) rb = piece.AddComponent<Rigidbody>();

                // Chỉ văng ngang theo X/Z (không bắn lên trục Y)
                Vector3 explosionDir = piece.transform.position - transform.position;
                explosionDir.y = 0f;
                Vector3 randomXZ = new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                explosionDir += randomXZ;
                if (explosionDir.sqrMagnitude < 0.0001f) explosionDir = transform.forward;
                explosionDir.Normalize();
                rb.AddForce(explosionDir * shatterForce, ForceMode.Impulse);
                rb.AddTorque(Vector3.up * Random.Range(-1f, 1f) * shatterForce * 2f, ForceMode.Impulse);

                // Gắn Absorbable cho mỗi mảnh → Swarm sẽ hút vào sau delay
                Absorbable absorbable = piece.GetComponent<Absorbable>();
                if (absorbable == null) absorbable = piece.AddComponent<Absorbable>();

                // Delay trước khi bắt đầu bị hút (để mảnh bay tung trước)
                StartCoroutine(DelayedAbsorb(piece, rb, shatterAbsorbDelay));
            }
        }

        /// <summary>Chờ mảnh bay tung ra, rồi tắt Rigidbody và cho Swarm hút từ từ</summary>
        private IEnumerator DelayedAbsorb(GameObject piece, Rigidbody rb, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (piece == null) yield break;

            // Giảm tốc dần rồi tắt gravity → mảnh "treo" trong không rồi bị hút
            if (rb != null)
            {
                rb.useGravity = false;
                rb.drag = 5f;
            }

            // Kích hoạt trigger để Swarm có thể hút
            Collider col = piece.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
