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
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private Joystick joystick;
        
        [Header("Speed Scaling")]
        [SerializeField, Range(0.5f, 3f)] private float minSpeedMultiplier = 1f;
        [SerializeField] private float zone12SpeedGrowthMultiplier = 1.6f;
        [SerializeField] private float zone3SpeedGrowthMultiplier = 3.25f;
        [SerializeField] private float zone12SpeedCapMultiplier = 4.5f;
        [SerializeField] private float zone3SpeedCapMultiplier = 10f;
        
        [Header("Swarm References")]
        [SerializeField] private ParticleSystem swarmParticles;
        [SerializeField] private Transform swarmCenter;
        [SerializeField] private float swarmRadius = 0.6f; 
        [SerializeField] private float particlePullForce = 25f; // Tăng thêm để hạt bám theo cực nhanh
        [Tooltip("Collider nhỏ hơn vòng hạt visual bao nhiêu lần. 0.4 = Collider chỉ bằng 40% bán kính visual, cảm giác cần chạm sát hơn mới kích hoạt.")]
        [SerializeField] private float colliderRadiusScale = 0.4f;
        
        [Header("Organic Sway (Lấy lư tự nhiên)")]
        [SerializeField] private float swaySpeed = 0.8f;
        [SerializeField] private float swayAmount = 0.3f;

        [Header("Damage Feedback")]
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.22f, 0.22f, 1f);
        [SerializeField, Range(0.05f, 0.4f)] private float damageFlashDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float damageFlashBlend = 1f;
        [SerializeField, Range(0.5f, 4f)] private float damageFlashEmissionBoost = 2.2f;
        
        private Camera mainCam;
        private Vector3 targetPosition;

        private float currentSwaySpeed;
        private float currentSwayAmount;

        private ParticleSystem.Particle[] particlesBuffer; // Bộ đệm tái sử dụng để tránh giật lag nhấp nháy (GC spike)

        private SwarmMorphController morphController;
        
        [field: Header("Debug Info")]
        [field: SerializeField] public int CurrentNanoMass { get; private set; } = 0;
        public float CurrentSwarmRadius => swarmRadius;
        public float InitialSwarmRadius => _initialRadius;

        // Lưu giá trị gốc để tính tỉ lệ tăng tốc về sau
        private float _baseMoveSpeed;
        private float _initialRadius;
        private int _initialMaxParticles;
        private float _initialEmissionRate;
        private float _initialParticleSize;
        private ParticleSystem.MinMaxGradient _baseStartColor;
        private Color _baseParticleColor = Color.white;
        private bool _hasBaseParticleColor;
        private Coroutine _damageFlashRoutine;
        private ParticleSystemRenderer _swarmRenderer;
        private Material _swarmMaterial;
        private int _swarmMaterialColorPropId = -1;
        private int _swarmMaterialEmissionPropId = -1;
        private Color _baseMaterialColor = Color.white;
        private Color _baseMaterialEmissionColor = Color.black;

        private void Start()
        {
            if (swarmCenter == null) swarmCenter = this.transform;
            targetPosition = swarmCenter.position;
            
            currentSwaySpeed = swaySpeed;
            currentSwayAmount = swayAmount;

            _baseMoveSpeed = moveSpeed;
            _initialRadius = Mathf.Max(swarmRadius, 0.01f); // Tránh chia cho 0 nếu swarmRadius chưa set
            if (swarmParticles != null)
            {
                var main = swarmParticles.main;
                _initialMaxParticles = Mathf.Max(1, main.maxParticles);
                _initialParticleSize = Mathf.Max(0.01f, main.startSize.constant);

                var emission = swarmParticles.emission;
                _initialEmissionRate = emission.rateOverTime.constant;

                _baseStartColor = main.startColor;
                _baseParticleColor = GetRepresentativeColor(_baseStartColor);
                _hasBaseParticleColor = true;
                CacheSwarmMaterial();
            }

            morphController = GetComponent<SwarmMorphController>();
            if (morphController == null) morphController = GetComponentInParent<SwarmMorphController>();

            SetupPhysics();
        }

        private void SetupPhysics()
        {
            SphereCollider col = swarmCenter.GetComponent<SphereCollider>();
            if (col == null) col = swarmCenter.gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = swarmRadius * colliderRadiusScale;
            col.center = Vector3.up * 1.5f;

            Rigidbody rb = swarmCenter.GetComponent<Rigidbody>();
            if (rb == null) rb = swarmCenter.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (swarmCenter.tag == "Untagged") swarmCenter.tag = "Player";
        }

        private void OnDrawGizmosSelected()
        {
            Transform center = swarmCenter != null ? swarmCenter : this.transform;
            Vector3 pos = center.position + Vector3.up * 1.5f;

            // 🟡 Vàng = Vùng bao phủ thực tế của các hạt Particle (swarmRadius)
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
            Gizmos.DrawWireSphere(pos, swarmRadius);

            // 🔴 Đỏ = Vùng Collider Physics thực sự kích hoạt Trigger ăn vật thể
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(pos, swarmRadius * colliderRadiusScale);

#if UNITY_EDITOR
            // Vẽ label chú thích trong Scene View cho dễ đọc
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(pos + Vector3.right * swarmRadius, $" Particles r={swarmRadius:F2}");
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(pos + Vector3.right * (swarmRadius * colliderRadiusScale), $" Collider r={swarmRadius * colliderRadiusScale:F2}");
#endif
        }

        private void Update()
        {
            // Safety: reset nếu targetPosition hoặc moveSpeed bị NaN (do bug chia cho 0 trước đây)
            if (float.IsNaN(targetPosition.x) || float.IsNaN(targetPosition.z) || float.IsInfinity(targetPosition.x))
            {
                targetPosition = swarmCenter.position;
                Debug.LogWarning("[SwarmController] targetPosition bị NaN → đã reset!");
            }
            if (float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed))
            {
                moveSpeed = _baseMoveSpeed;
                Debug.LogWarning("[SwarmController] moveSpeed bị NaN → đã reset về base!");
            }

            HandleInput();
            LerpMovement();
            SyncColliderRadius();

            // Chỉ attract hình cầu khi ở Swarm Mode (không đánh nhau với Sword/Vortex)
            bool isSwarmMode = morphController == null || morphController.CurrentMode == SwarmMorphController.MorphMode.Swarm;
            if (isSwarmMode)
            {
                AttractParticles();
            }
        }

        /// <summary>
        /// Giữ SphereCollider luôn đúng kích thước world-space bất kể swarmCenter.localScale thay đổi.
        /// GrowPulse scale swarmCenter → Unity tự nhân col.radius theo scale → collider to hơn dự kiến.
        /// Fix: col.radius = target / localScale để world-space radius luôn = swarmRadius * colliderRadiusScale.
        /// </summary>
        private void SyncColliderRadius()
        {
            if (swarmCenter == null) return;
            // Fix: Phải dùng lossyScale thay vì localScale để tính radius chính xác tuyệt đối trong World Space 
            float globalScale = Mathf.Max(swarmCenter.lossyScale.x, Mathf.Max(swarmCenter.lossyScale.y, swarmCenter.lossyScale.z));
            if (globalScale < 0.001f) return; 

            SphereCollider col = swarmCenter.GetComponent<SphereCollider>();
            if (col != null)
            {
                float targetWorldRadius = swarmRadius * colliderRadiusScale;
                col.radius = targetWorldRadius / globalScale;
            }

            // Đồng bộ kích thước vòng Shape (vòng xanh lá cây mặc định của Unity) với swarmRadius
            if (swarmParticles != null)
            {
                var shape = swarmParticles.shape;
                float psScale = Mathf.Max(swarmParticles.transform.lossyScale.x, Mathf.Max(swarmParticles.transform.lossyScale.y, swarmParticles.transform.lossyScale.z));
                if (psScale > 0.001f) shape.radius = swarmRadius / psScale;
            }
        }

        private void OnValidate()
        {
            damageFlashDuration = Mathf.Clamp(damageFlashDuration, 0.05f, 0.4f);
            damageFlashBlend = Mathf.Clamp01(damageFlashBlend);
            damageFlashEmissionBoost = Mathf.Clamp(damageFlashEmissionBoost, 0.5f, 4f);
            minSpeedMultiplier = Mathf.Max(0.5f, minSpeedMultiplier);
            zone12SpeedCapMultiplier = Mathf.Max(minSpeedMultiplier, zone12SpeedCapMultiplier);
            zone3SpeedCapMultiplier = Mathf.Max(zone12SpeedCapMultiplier, zone3SpeedCapMultiplier);

            // Cho phép xem vòng Particle Shape (xanh lá cây) thay đổi theo swarmRadius ngay từ lúc chưa bấm Play
            if (swarmParticles != null)
            {
                var shape = swarmParticles.shape;
                float psScale = Mathf.Max(swarmParticles.transform.lossyScale.x, Mathf.Max(swarmParticles.transform.lossyScale.y, swarmParticles.transform.lossyScale.z));
                if (psScale > 0.001f) shape.radius = swarmRadius / psScale;
            }
        }


        private void AttractParticles()
        {
            if (swarmParticles == null) return;

            // Đảm bảo buffer luôn đủ sức chứa lượng hạt Particle tối đa (maxParticles)
            int currentMax = swarmParticles.main.maxParticles;
            if (particlesBuffer == null || particlesBuffer.Length < currentMax)
            {
                particlesBuffer = new ParticleSystem.Particle[currentMax];
            }

            // GetParticles sẽ đổ hạt vào mảng buffer (không tạo mảng mới để tránh Nhấp nháy/Giật lag khung hình)
            int count = swarmParticles.GetParticles(particlesBuffer);

            Vector3 centerPos = swarmCenter.position + Vector3.up * 1.5f;

            for (int i = 0; i < count; i++)
            {
                Random.InitState((int)particlesBuffer[i].randomSeed);
                Vector3 randomOffset = Random.insideUnitSphere * swarmRadius; 

                float seed = particlesBuffer[i].randomSeed;
                Vector3 sway = new Vector3(
                    Mathf.PerlinNoise(seed, Time.time * currentSwaySpeed) - 0.5f,
                    Mathf.PerlinNoise(seed + 100, Time.time * currentSwaySpeed) - 0.5f,
                    Mathf.PerlinNoise(seed + 200, Time.time * currentSwaySpeed) - 0.5f
                ) * currentSwayAmount;

                Vector3 individualTarget = centerPos + randomOffset + sway;
                Vector3 desiredVelocity = (individualTarget - particlesBuffer[i].position) * particlePullForce;
                particlesBuffer[i].velocity = Vector3.Lerp(particlesBuffer[i].velocity, desiredVelocity, Time.deltaTime * 15f);
            }

            swarmParticles.SetParticles(particlesBuffer, count);
        }

        private void HandleInput()
        {
            Vector3 inputDir = Vector3.zero;
            if (joystick != null)
            {
                inputDir = new Vector3(joystick.Direction.x, 0, joystick.Direction.y);

#if UNITY_EDITOR
                // Trong Editor: nếu joystick không được kéo thì dùng WASD để test
                if (inputDir.sqrMagnitude < 0.01f)
                    inputDir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
#endif
            }
            else
            {
                inputDir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            }

            if (inputDir.sqrMagnitude > 0.01f) targetPosition += inputDir * moveSpeed * Time.deltaTime;
        }


        private void LerpMovement()
        {
            swarmCenter.position = Vector3.Lerp(swarmCenter.position, targetPosition, Time.deltaTime * smoothSpeed);
        }

        /// <summary>
        /// Swarm dần rơi xuống đất theo fallSpeed mỗi frame,
        /// dừng khi Raycast phát hiện collider bên dưới.
        /// </summary>
        public void SnapToGround()
        {
            StartCoroutine(SnapToGroundRoutine());
        }

        // Test nhanh trong Editor: chuột phải vào component → "Test Snap To Ground"
        [ContextMenu("Test Snap To Ground")]
        private void TestSnapToGround() => SnapToGround();

        private IEnumerator SnapToGroundRoutine(float fallSpeed = 6f, float maxFallDistance = 30f)
        {
            float startY = targetPosition.y;
            float minY   = startY - maxFallDistance;

            Debug.Log($"[SnapToGround] ▶ Bắt đầu | swarmCenter={swarmCenter.position} | targetPos={targetPosition} | fallSpeed={fallSpeed}");

            while (targetPosition.y > minY)
            {
                Vector3 rayOrigin = new Vector3(
                    swarmCenter.position.x,
                    targetPosition.y + 0.3f,
                    swarmCenter.position.z);

                // Hiển thị tia trong Scene View (bật Gizmos khi Play)
                Debug.DrawRay(rayOrigin, Vector3.down * 1.0f, Color.cyan, 0.05f);

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1.0f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    // Tia màu trắng = hit sàn
                    Debug.DrawRay(rayOrigin, Vector3.down * hit.distance, Color.white, 2f);
                    
                    // Thêm +1.04f để nâng độ cao nhỉnh lên theo yêu cầu (từ -5.03 lên -4)
                    targetPosition = new Vector3(targetPosition.x, hit.point.y + 1.04f, targetPosition.z);
                    
                    Debug.Log($"[SnapToGround] ✅ Hit '{hit.collider.name}' (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) tại y={hit.point.y:F2} | Đã bù +1.04f lên {targetPosition.y}");
                    yield break;
                }
                else
                {
                    // Tia màu đỏ = miss (không thấy gì 1 unit phía dưới)
                    Debug.DrawRay(rayOrigin, Vector3.down * 1.0f, Color.red, 0.05f);
                }

                targetPosition.y -= fallSpeed * Time.deltaTime;
                yield return null;
            }

            Debug.LogWarning($"[SnapToGround] ❌ Rơi hết {maxFallDistance} đơn vị vẫn không tìm thấy sàn!\n" +
                             $"→ Kiểm tra: Floor có Collider không? isTrigger bị bật không?\n" +
                             $"→ swarmCenter cuối: {swarmCenter.position}");
        }



        public void Grow(int amount)
        {
            CurrentNanoMass += amount; // Ghi nhận độ lớn để mở khóa biến hình / ăn vật to hơn

            var main = swarmParticles.main;
            // Tăng sức chứa tối đa lên thật DƯ DẢ để không bị tràn gây nhấp nháy giới hạn (cũ bị giết, mới đẻ ra chèn lấp)
            main.maxParticles += amount * 4;
            
            // Nhả tức thì một lượng hạt lớn để bùng nổ tức thì
            swarmParticles.Emit(amount * 2);

            // Nhích tốc độ sinh hạt cực ít thôi (0.05f so với 0.5f) vì nếu sinh nhanh quá max limit sẽ gây giật chớp.
            var emission = swarmParticles.emission;
            emission.rateOverTime = emission.rateOverTime.constant + (amount * 0.05f);
            
            StopCoroutine("GrowPulse");
            StartCoroutine(GrowPulse(amount));

            // TRẠNG THÁI HÀO HỨNG: Swarm sôi sục khi ăn
            StopCoroutine("ExcitementRoutine");
            StartCoroutine(ExcitementRoutine());

            float growthFactor = amount * 0.0015f; // Scale pulse lên khi ăn
            
            // TỰ ĐỘNG CHUYỂN SỐ SỐ THEO ZONE:
            // Dưới 3000 điểm (Zone 1, 2) thì giữ nguyên gốc để không bị loãng.
            // Trên 3000 điểm (Zone 3 - sau phá tường) thì tăng cực mạnh để bù lại kích thước các tòa nhà.
            bool isZone3 = CurrentNanoMass >= 3000;
            
            float radiusGrowth = amount * (isZone3 ? 0.012f : 0.005f);  

            swarmRadius += radiusGrowth;
            // KHÔNG gán col.radius ở đây vì SyncColliderRadius() trong Update() đã xử lý mỗi frame

            // Tăng kích thước hạt mới (startSize cho emitter)
            float currentSize = main.startSize.constant;
            float newSize = currentSize + growthFactor * (isZone3 ? 0.25f : 0.12f); 
            main.startSize = new ParticleSystem.MinMaxCurve(newSize);

            // Tăng kích thước các hạt CŨ đang tồn tại
            if (particlesBuffer != null)
            {
                int count = swarmParticles.GetParticles(particlesBuffer);
                for (int i = 0; i < count; i++)
                {
                    particlesBuffer[i].startSize = newSize;
                }
                swarmParticles.SetParticles(particlesBuffer, count);
            }

            // Tốc độ di chuyển tỉ lệ trực tiếp với bán kính swarm
            RecalculateMoveSpeed();
        }

        public void LoseNanoMass(int amount)
        {
            if (amount <= 0 || swarmParticles == null) return;

            int oldMass = CurrentNanoMass;
            CurrentNanoMass = Mathf.Max(0, CurrentNanoMass - amount);
            int actualLoss = oldMass - CurrentNanoMass;
            if (actualLoss <= 0) return;

            var main = swarmParticles.main;
            var emission = swarmParticles.emission;

            // Giảm sức chứa hạt theo đúng tỉ lệ đã tăng trước đó.
            int reducedMax = main.maxParticles - (actualLoss * 4);
            main.maxParticles = Mathf.Max(_initialMaxParticles, reducedMax);

            // Giảm nhịp sinh hạt nhưng không thấp hơn mốc gốc.
            emission.rateOverTime = Mathf.Max(_initialEmissionRate, emission.rateOverTime.constant - (actualLoss * 0.05f));

            bool wasZone3 = oldMass >= 3000;
            float radiusLoss = actualLoss * (wasZone3 ? 0.012f : 0.005f);
            swarmRadius = Mathf.Max(_initialRadius, swarmRadius - radiusLoss);

            float sizeLoss = actualLoss * 0.0015f * (wasZone3 ? 0.25f : 0.12f);
            float newSize = Mathf.Max(_initialParticleSize, main.startSize.constant - sizeLoss);
            main.startSize = new ParticleSystem.MinMaxCurve(newSize);

            // Co kích thước các hạt đang tồn tại cho đồng bộ visual.
            int currentMax = main.maxParticles;
            if (particlesBuffer == null || particlesBuffer.Length < currentMax)
            {
                particlesBuffer = new ParticleSystem.Particle[currentMax];
            }

            int count = swarmParticles.GetParticles(particlesBuffer);
            int targetCount = Mathf.Min(count, main.maxParticles);
            for (int i = 0; i < targetCount; i++)
            {
                particlesBuffer[i].startSize = newSize;
            }
            swarmParticles.SetParticles(particlesBuffer, targetCount);

            RecalculateMoveSpeed();
            TriggerDamageFlash();
        }

        private void RecalculateMoveSpeed()
        {
            if (_initialRadius <= 0.001f)
            {
                moveSpeed = _baseMoveSpeed;
                return;
            }

            bool isZone3 = CurrentNanoMass >= 3000;
            float radiusRatio = Mathf.Max(0f, (swarmRadius - _initialRadius) / _initialRadius);
            float growthMultiplier = isZone3 ? zone3SpeedGrowthMultiplier : zone12SpeedGrowthMultiplier;
            float capMultiplier = isZone3 ? zone3SpeedCapMultiplier : zone12SpeedCapMultiplier;

            float targetMultiplier = 1f + radiusRatio * growthMultiplier;
            float minMultiplier = Mathf.Max(0.5f, minSpeedMultiplier);
            float maxMultiplier = Mathf.Max(minMultiplier, capMultiplier);
            moveSpeed = _baseMoveSpeed * Mathf.Clamp(targetMultiplier, minMultiplier, maxMultiplier);
        }

        private void TriggerDamageFlash()
        {
            if (swarmParticles == null) return;

            if (_damageFlashRoutine != null)
            {
                StopCoroutine(_damageFlashRoutine);
                _damageFlashRoutine = null;
            }

            _damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
        }

        private IEnumerator DamageFlashRoutine()
        {
            if (swarmParticles == null) yield break;

            var main = swarmParticles.main;
            if (!_hasBaseParticleColor)
            {
                _baseStartColor = main.startColor;
                _baseParticleColor = GetRepresentativeColor(_baseStartColor);
                _hasBaseParticleColor = true;
            }

            CacheSwarmMaterial();

            // Dùng màu damage trực tiếp để flash luôn nổi bật, không bị chìm vào màu swarm gốc.
            Color flashColor = Color.Lerp(_baseParticleColor, damageFlashColor, Mathf.Clamp01(damageFlashBlend));
            if (damageFlashBlend < 0.95f) flashColor = damageFlashColor;
            main.startColor = new ParticleSystem.MinMaxGradient(flashColor);
            ApplyColorToAliveParticles(flashColor);
            ApplyMaterialTint(flashColor);

            yield return new WaitForSeconds(Mathf.Max(0.14f, damageFlashDuration));

            main.startColor = _baseStartColor;
            ApplyColorToAliveParticles(_baseParticleColor);
            RestoreMaterialTint();
            _damageFlashRoutine = null;
        }

        private void ApplyColorToAliveParticles(Color targetColor)
        {
            if (swarmParticles == null) return;

            int currentMax = Mathf.Max(1, swarmParticles.main.maxParticles);
            if (particlesBuffer == null || particlesBuffer.Length < currentMax)
            {
                particlesBuffer = new ParticleSystem.Particle[currentMax];
            }

            int count = swarmParticles.GetParticles(particlesBuffer);
            if (count <= 0) return;

            Color32 baseColor32 = targetColor;
            for (int i = 0; i < count; i++)
            {
                Color32 c = baseColor32;
                c.a = particlesBuffer[i].startColor.a;
                particlesBuffer[i].startColor = c;
            }

            swarmParticles.SetParticles(particlesBuffer, count);
        }

        private void CacheSwarmMaterial()
        {
            if (_swarmMaterial != null && (_swarmMaterialColorPropId != -1 || _swarmMaterialEmissionPropId != -1)) return;
            if (swarmParticles == null) return;

            _swarmRenderer = swarmParticles.GetComponent<ParticleSystemRenderer>();
            if (_swarmRenderer == null) return;

            _swarmMaterial = _swarmRenderer.material;
            if (_swarmMaterial == null) return;

            int tintId = Shader.PropertyToID("_TintColor");
            int baseColorId = Shader.PropertyToID("_BaseColor");
            int colorId = Shader.PropertyToID("_Color");
            int emissionId = Shader.PropertyToID("_EmissionColor");

            if (_swarmMaterial.HasProperty(tintId))
            {
                _swarmMaterialColorPropId = tintId;
                _baseMaterialColor = _swarmMaterial.GetColor(tintId);
            }
            else if (_swarmMaterial.HasProperty(baseColorId))
            {
                _swarmMaterialColorPropId = baseColorId;
                _baseMaterialColor = _swarmMaterial.GetColor(baseColorId);
            }
            else if (_swarmMaterial.HasProperty(colorId))
            {
                _swarmMaterialColorPropId = colorId;
                _baseMaterialColor = _swarmMaterial.GetColor(colorId);
            }

            if (_swarmMaterial.HasProperty(emissionId))
            {
                _swarmMaterialEmissionPropId = emissionId;
                _baseMaterialEmissionColor = _swarmMaterial.GetColor(emissionId);
            }
        }

        private void ApplyMaterialTint(Color targetColor)
        {
            if (_swarmMaterial == null) return;

            if (_swarmMaterialColorPropId != -1)
            {
                _swarmMaterial.SetColor(_swarmMaterialColorPropId, targetColor);
            }

            if (_swarmMaterialEmissionPropId != -1)
            {
                _swarmMaterial.SetColor(_swarmMaterialEmissionPropId, targetColor * damageFlashEmissionBoost);
            }
        }

        private void RestoreMaterialTint()
        {
            if (_swarmMaterial == null) return;

            if (_swarmMaterialColorPropId != -1)
            {
                _swarmMaterial.SetColor(_swarmMaterialColorPropId, _baseMaterialColor);
            }

            if (_swarmMaterialEmissionPropId != -1)
            {
                _swarmMaterial.SetColor(_swarmMaterialEmissionPropId, _baseMaterialEmissionColor);
            }
        }

        private static Color GetRepresentativeColor(ParticleSystem.MinMaxGradient gradient)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return gradient.color;
                case ParticleSystemGradientMode.TwoColors:
                    return Color.Lerp(gradient.colorMin, gradient.colorMax, 0.5f);
                case ParticleSystemGradientMode.Gradient:
                    return gradient.gradient != null ? gradient.gradient.Evaluate(0.5f) : Color.white;
                case ParticleSystemGradientMode.TwoGradients:
                    Color a = gradient.gradientMin != null ? gradient.gradientMin.Evaluate(0.5f) : Color.white;
                    Color b = gradient.gradientMax != null ? gradient.gradientMax.Evaluate(0.5f) : Color.white;
                    return Color.Lerp(a, b, 0.5f);
                case ParticleSystemGradientMode.RandomColor:
                    return gradient.gradient != null ? gradient.gradient.Evaluate(0.5f) : Color.white;
                default:
                    return Color.white;
            }
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

        private IEnumerator GrowPulse(int amount)
        {
            float growthFactor = amount * 0.0015f; // Lượng to lên thực tế
            Vector3 baseScale = swarmCenter.localScale;
            Vector3 targetScale = baseScale + Vector3.one * (growthFactor * 2.5f); // Phình ra to hơn lúc mới ăn
            
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                swarmCenter.localScale = Vector3.Lerp(baseScale, targetScale, elapsed / duration);
                yield return null;
            }

            Vector3 finalScale = baseScale + Vector3.one * growthFactor; // Co về bằng mức tăng thêm
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

