using UnityEngine;

namespace NanoGrowth
{
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance;

        [Header("Target Settings")]
        [SerializeField] private Transform target; // SwarmCenter
        [SerializeField] private Vector3 offset = new Vector3(0, 15, -10); // Vị trí tương đối của Camera so với Swarm
        
        [Header("Smooth Settings")]
        [SerializeField] private float smoothSpeed = 5f;

        [Header("Shake Effect")]
        private float shakeDuration = 0f;
        private float shakeAmount = 0.2f;
        private Vector3 shakeOffset = Vector3.zero;
        
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
        }

        public void Shake(float duration, float amount)
        {
            shakeDuration = duration;
            shakeAmount = amount;
        }

        private void LateUpdate()
        {
            if (target == null) return;

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

            // Tính toán vị trí đích
            Vector3 desiredPosition = target.position + offset + shakeOffset;
            
            // Di chuyển mượt mà tới vị trí đích
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            
            transform.position = smoothedPosition;
        }
    }
}
