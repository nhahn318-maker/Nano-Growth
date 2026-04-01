using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NanoGrowth
{
    /// <summary>
    /// Break wall between zones. When broken, it spawns shatter pieces that can be absorbed.
    /// </summary>
    public class BreakableWall : MonoBehaviour
    {
        [Header("Requirements")]
        [SerializeField] private float requiredMassToBreak = 2500f;

        [Header("Shatter Settings")]
        [SerializeField] private GameObject solidWall;
        [SerializeField] private GameObject[] shatterPieces;
        [SerializeField] private float shatterForce = 10f;
        [SerializeField] private int growthPerPiece = 10;

        [Header("FX")]
        [SerializeField] private ParticleSystem breakExplosionFX;
        [SerializeField] private AudioSource breakSfxSource;
        [SerializeField] private AudioClip breakSfxClip;
        [SerializeField, Range(0f, 1f)] private float breakSfxVolume = 1f;

        [Header("Blocking")]
        [SerializeField] private bool enforceBlockingWhenLocked = true;
        [SerializeField, Range(0.5f, 2f)] private float blockPushMultiplier = 1f;

        private bool isBroken = false;
        private readonly List<Collider> wallColliders = new List<Collider>();
        private Collider fallbackBlockerCollider;

        private void Start()
        {
            if (shatterPieces != null)
            {
                for (int i = 0; i < shatterPieces.Length; i++)
                {
                    if (shatterPieces[i] != null) shatterPieces[i].SetActive(false);
                }
            }

            CacheWallColliders();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isBroken) return;
            TryHandleSwarmContact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (isBroken) return;
            TryHandleSwarmContact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isBroken) return;
            if (collision == null || collision.collider == null) return;
            TryHandleSwarmContact(collision.collider);
        }

        public void BreakWall(Vector3 hitPosition)
        {
            if (isBroken) return;
            isBroken = true;

            if (CameraFollow.Instance != null)
                CameraFollow.Instance.Shake(0.5f, 0.4f);

            PlayBreakSfx(hitPosition);

            if (breakExplosionFX != null)
            {
                breakExplosionFX.transform.SetParent(null);
                breakExplosionFX.Play();
                Destroy(breakExplosionFX.gameObject, 3f);
            }

            HideSolidWallVisual();
            SetWallCollidersEnabled(false);
            SpawnShatterPieces(hitPosition);
        }

        private void PlayBreakSfx(Vector3 hitPosition)
        {
            if (breakSfxClip == null) return;

            if (breakSfxSource != null)
            {
                breakSfxSource.pitch = Random.Range(0.95f, 1.05f);
                breakSfxSource.PlayOneShot(breakSfxClip, breakSfxVolume);
                return;
            }

            AudioSource.PlayClipAtPoint(breakSfxClip, hitPosition, breakSfxVolume);
        }

        private void TryHandleSwarmContact(Collider other)
        {
            if (other == null) return;

            SwarmController swarm = other.GetComponentInParent<SwarmController>();
            if (swarm == null) return;

            if (CanBreak(swarm))
            {
                BreakWall(swarm.transform.position);
                return;
            }

            if (enforceBlockingWhenLocked)
            {
                PushSwarmOut(other);
            }
        }

        private bool CanBreak(SwarmController swarm)
        {
            if (swarm == null) return false;

            if (swarm.CurrentNanoMass >= requiredMassToBreak) return true;

            SwarmMorphController morph = swarm.GetComponent<SwarmMorphController>();
            if (morph == null) morph = swarm.GetComponentInParent<SwarmMorphController>();
            return morph != null && morph.CurrentMode == SwarmMorphController.MorphMode.Sword;
        }

        private void PushSwarmOut(Collider swarmCollider)
        {
            if (swarmCollider == null) return;

            Transform swarmTransform = swarmCollider.attachedRigidbody != null
                ? swarmCollider.attachedRigidbody.transform
                : swarmCollider.transform;

            Vector3 totalOffset = Vector3.zero;
            int overlapCount = 0;

            for (int i = 0; i < wallColliders.Count; i++)
            {
                Collider wallCol = wallColliders[i];
                if (wallCol == null || !wallCol.enabled || !wallCol.gameObject.activeInHierarchy) continue;

                if (Physics.ComputePenetration(
                    swarmCollider, swarmCollider.transform.position, swarmCollider.transform.rotation,
                    wallCol, wallCol.transform.position, wallCol.transform.rotation,
                    out Vector3 direction, out float distance))
                {
                    totalOffset += direction * (distance + 0.02f);
                    overlapCount++;
                }
            }

            if (overlapCount <= 0) return;

            Vector3 push = totalOffset * blockPushMultiplier;
            if (push.sqrMagnitude > 1f) push = push.normalized;
            swarmTransform.position += push;
        }

        private void CacheWallColliders()
        {
            wallColliders.Clear();
            Collider[] all = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Collider c = all[i];
                if (c == null) continue;
                if (BelongsToShatterPiece(c.transform)) continue;
                wallColliders.Add(c);
            }

            if (wallColliders.Count == 0)
            {
                EnsureFallbackBlockerCollider();
            }
        }

        private void EnsureFallbackBlockerCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();

            if (TryGetSolidWallBounds(out Bounds bounds))
            {
                Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = new Vector3(
                    Mathf.Max(0.5f, bounds.size.x),
                    Mathf.Max(1f, bounds.size.y),
                    Mathf.Max(0.5f, bounds.size.z));

                box.center = localCenter;
                box.size = localSize;
            }

            box.isTrigger = true;
            box.enabled = true;
            fallbackBlockerCollider = box;
            wallColliders.Add(box);
        }

        private bool TryGetSolidWallBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.one);
            if (solidWall == null) return false;

            Renderer[] renderers = solidWall.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private bool BelongsToShatterPiece(Transform t)
        {
            if (t == null || shatterPieces == null) return false;
            for (int i = 0; i < shatterPieces.Length; i++)
            {
                GameObject piece = shatterPieces[i];
                if (piece == null) continue;
                if (t == piece.transform || t.IsChildOf(piece.transform)) return true;
            }

            return false;
        }

        private void SetWallCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < wallColliders.Count; i++)
            {
                if (wallColliders[i] != null) wallColliders[i].enabled = enabled;
            }

            if (fallbackBlockerCollider != null) fallbackBlockerCollider.enabled = enabled;
        }

        private void SpawnShatterPieces(Vector3 hitPos)
        {
            if (shatterPieces == null) return;

            for (int i = 0; i < shatterPieces.Length; i++)
            {
                GameObject piece = shatterPieces[i];
                if (piece == null) continue;

                piece.transform.SetParent(null, true);
                piece.SetActive(true);

                Rigidbody rb = piece.GetComponent<Rigidbody>();
                if (rb == null) rb = piece.AddComponent<Rigidbody>();

                Vector3 explosionDir = piece.transform.position - hitPos;
                explosionDir.y = 0f;
                if (explosionDir.sqrMagnitude < 0.0001f) explosionDir = transform.forward;
                explosionDir.Normalize();

                rb.AddForce(explosionDir * shatterForce, ForceMode.Impulse);
                rb.AddTorque(Vector3.up * Random.Range(-1f, 1f) * shatterForce, ForceMode.Impulse);

                Absorbable absorbable = piece.GetComponent<Absorbable>();
                if (absorbable == null) absorbable = piece.AddComponent<Absorbable>();
                absorbable.growthAmount = growthPerPiece;

                QueueDelayedAbsorb(piece, rb, 1f);
            }

            Destroy(gameObject, 3f);
        }

        private void HideSolidWallVisual()
        {
            if (solidWall == null) return;

            // If user assigns SolidWall = this root object, never deactivate root.
            if (solidWall == gameObject)
            {
                Renderer[] renderers = solidWall.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null) renderers[i].enabled = false;
                }

                return;
            }

            solidWall.SetActive(false);
        }

        private void QueueDelayedAbsorb(GameObject piece, Rigidbody rb, float delay)
        {
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedAbsorb(piece, rb, delay));
                return;
            }

            ApplyAbsorbState(piece, rb);
        }

        private IEnumerator DelayedAbsorb(GameObject piece, Rigidbody rb, float delay)
        {
            yield return new WaitForSeconds(delay);
            ApplyAbsorbState(piece, rb);
        }

        private static void ApplyAbsorbState(GameObject piece, Rigidbody rb)
        {
            if (piece == null) return;

            if (rb != null)
            {
                rb.useGravity = false;
                rb.drag = 5f;
            }

            Collider col = piece.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
