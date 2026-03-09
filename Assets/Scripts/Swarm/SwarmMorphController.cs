using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NanoGrowth
{
    /// <summary>
    /// Step 4: Logic for transformation between states for the UA video segments.
    /// Handles visual switching between Swarm, Sword, and Vortex.
    /// </summary>
    public class SwarmMorphController : MonoBehaviour
    {
        [Header("Visual Modes")]
        [SerializeField] private GameObject swarmObject;
        [SerializeField] private GameObject swordObject;
        [SerializeField] private GameObject vortexObject;
        
        [Header("Controllers")]
        [SerializeField] private SwarmSwordController swordController;
        // [SerializeField] private SwarmVortexController vortexController; // TODO: Ngày sau

        [Header("FX & Audio")]
        [SerializeField] private ParticleSystem transformationEffect;
        [SerializeField] private AudioSource transformationSnd;

        [Header("Progression / Requirements")]
        [SerializeField] private int requiredMassForSword = 500; // Cần 500 điểm ăn để biến thành kiếm

        public enum MorphMode { Swarm, Sword, Vortex }
        private MorphMode currentMode = MorphMode.Swarm;

        public MorphMode CurrentMode => currentMode;

        private void Start()
        {
            // Auto-find SwordController nếu chưa gán
            if (swordController == null) swordController = GetComponentInChildren<SwarmSwordController>();

            // Start as swarm
            UpdateVisuals(MorphMode.Swarm);
        }

        private void Update()
        {
            // Hotkeys for easy UA video recording/testing
            if (Input.GetKeyDown(KeyCode.Alpha1)) UpdateVisuals(MorphMode.Swarm);
            if (Input.GetKeyDown(KeyCode.Alpha2)) UpdateVisuals(MorphMode.Sword);
            if (Input.GetKeyDown(KeyCode.Alpha3)) UpdateVisuals(MorphMode.Vortex);
        }

        public void UpdateVisuals(MorphMode newMode)
        {
            if (currentMode == newMode && Time.time > 0.1f) return;
            
            // --- KIỂM TRA ĐIỂM SỐ NANO ĐỂ ĐƯỢC PHÉP BIẾN HÌNH ---
            if (newMode == MorphMode.Sword)
            {
                SwarmController swarm = GetComponent<SwarmController>();
                if (swarm == null) swarm = GetComponentInParent<SwarmController>();
                if (swarm == null) swarm = FindObjectOfType<SwarmController>();

                if (swarm != null && swarm.CurrentNanoMass < requiredMassForSword)
                {
                    Debug.Log($"[-] Không Đủ Hạt Nano! Bạn cần ăn đạt {requiredMassForSword} kích cỡ mới có thể biến thành Kiếm! Hiện có: {swarm.CurrentNanoMass}");
                    return; // Block the transformation
                }
            }

            // ── Tắt mode cũ ──
            DeactivateMode(currentMode);

            // Visual Flash (Trick for fast morphing)
            if (transformationEffect != null) transformationEffect.Play();
            if (transformationSnd != null) transformationSnd.Play();
            
            // Toggle GameObjects
            if (swarmObject != null) swarmObject.SetActive(newMode == MorphMode.Swarm);
            if (swordObject != null) swordObject.SetActive(newMode == MorphMode.Sword);
            if (vortexObject != null) vortexObject.SetActive(newMode == MorphMode.Vortex);

            // ── Bật mode mới ──
            ActivateMode(newMode);

            currentMode = newMode;
            Debug.Log($"Nano Swarm morphed into {newMode}!");
        }

        private void ActivateMode(MorphMode mode)
        {
            switch (mode)
            {
                case MorphMode.Sword:
                    if (swordController != null) swordController.ActivateSword();
                    break;
                // case MorphMode.Vortex:
                //     if (vortexController != null) vortexController.ActivateVortex();
                //     break;
            }
        }

        private void DeactivateMode(MorphMode mode)
        {
            switch (mode)
            {
                case MorphMode.Sword:
                    if (swordController != null) swordController.DeactivateSword();
                    break;
                // case MorphMode.Vortex:
                //     if (vortexController != null) vortexController.DeactivateVortex();
                //     break;
            }
        }
    }
}
