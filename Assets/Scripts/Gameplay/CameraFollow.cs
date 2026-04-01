using UnityEngine;

namespace NanoGrowth
{
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance;

        [Header("Target Settings")]
        [SerializeField] private Transform target; // SwarmCenter
        [SerializeField] private Vector3 offset = new Vector3(0, 22, -10); // Vị trí tương đối của Camera so với Swarm
        
        [Header("Smooth Settings")]
        [SerializeField] private float smoothSpeed = 5f;

        [Header("Zoom Settings — Scale-based (Swarm size)")]
        [SerializeField] private float heightMultiplier = 15f;     // Kéo cao theo localScale
        [SerializeField] private float zoomOutMultiplier = 4f;     // Lùi xa theo localScale

        [Header("Zoom Settings — Mass-based (Score / điểm hấp thụ)")]
        [SerializeField] private float massHeightScale = 0.012f;   // Mỗi 1 điểm mass → cao lên bao nhiêu
        [SerializeField] private float massZoomOutScale = 0.006f;  // Mỗi 1 điểm mass → lùi xa thêm bao nhiêu
        [SerializeField] private float maxMassOffset = 40f;        // Giới hạn tối đa camera không bay mãi

        [Header("Shake Effect")]
        private float shakeDuration = 0f;
        private float shakeAmount = 0.2f;
        private Vector3 shakeOffset = Vector3.zero;

        private Vector3 initialOffset;
        private Camera cam;
        private float initialOrthographicSize;

        private SwarmController swarmController;
        
        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Nếu chưa gán target trong Inspector, tìm SwarmCenter
            if (target == null)
            {
                GameObject swarmObj = GameObject.Find("SwarmCenter");
                if (swarmObj != null) target = swarmObj.transform;
            }

            // Auto-tìm SwarmController để lấy CurrentNanoMass
            swarmController = FindObjectOfType<SwarmController>();

            initialOffset = offset;
            cam = GetComponent<Camera>();
            if (cam != null && cam.orthographic)
            {
                initialOrthographicSize = cam.orthographicSize;
            }
        }

        public void Shake(float duration, float amount)
        {
            shakeDuration = duration;
            shakeAmount = amount;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // --- SCALE-BASED (Swarm localScale) ---
            float scaleMultiplier = target.localScale.x;
            scaleMultiplier = Mathf.Max(1f, scaleMultiplier);
            float scaleHeightBonus   = (scaleMultiplier - 1f) * heightMultiplier;
            float scaleZoomOutBonus  = (scaleMultiplier - 1f) * zoomOutMultiplier;

            // --- MASS-BASED (CurrentNanoMass / điểm hấp thụ) ---
            float nanoMass = swarmController != null ? swarmController.CurrentNanoMass : 0f;
            
            float massHeightBonus = 0f;
            float massZoomOutBonus = 0f;

            // TỰ ĐỘNG KÉO CAMERA BAO QUÁT (Chỉ áp dụng SAU khi đã phá tường Zone 3)
            // Khắc phục lỗi: Không cho camera lùi ra xa khi còn ở Zone 1, 2 (dưới 3000 điểm)
            if (nanoMass >= 3000)
            {
                // Trừ đi 3000 mốc cơ bản để bonus bắt đầu mượt mà từ 0, chống giật khục camera
                float extraMass = nanoMass - 3000f;
                
                float currentHeightScale = massHeightScale * 2f;
                float currentZoomScale   = massZoomOutScale * 2f;
                float currentMaxOffset   = maxMassOffset * 3f;

                massHeightBonus  = Mathf.Min(extraMass * currentHeightScale,  currentMaxOffset);
                massZoomOutBonus = Mathf.Min(extraMass * currentZoomScale, currentMaxOffset * 0.5f);
            }

            // --- KẾT HỢP CẢ HAI ---
            Vector3 dynamicOffset = new Vector3(
                initialOffset.x,
                initialOffset.y + scaleHeightBonus + massHeightBonus,
                initialOffset.z - scaleZoomOutBonus - massZoomOutBonus
            );

            // Xử lý rung màn hình
            if (shakeDuration > 0)
            {
                shakeOffset = Random.insideUnitSphere * shakeAmount;
                shakeDuration -= Time.deltaTime;
            }
            else
            {
                shakeOffset = Vector3.zero;
            }

            // Di chuyển mượt mà tới vị trí đích
            Vector3 desiredPosition = target.position + dynamicOffset + shakeOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // Kéo zoom out tương ứng cho camera kiểu Orthographic
            if (cam != null && cam.orthographic)
            {
                float targetOrthoSize = initialOrthographicSize + scaleZoomOutBonus + massZoomOutBonus;
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, smoothSpeed * Time.deltaTime);
            }
        }
    }
}
