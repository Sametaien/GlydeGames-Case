#region

using Fusion;
using PlayerRelated;
using UnityEngine;

#endregion

namespace UsableItems
{
    public class Weapon : NetworkBehaviour, IUsable
    {
        [Header("Weapon Settings")] [SerializeField]
        private float damage = 10f;

        [SerializeField] private float maxRange = 100f;
        [SerializeField] private float knockbackForce = 5f; // Geri itme kuvveti
        [SerializeField] private LayerMask hitLayerMask; // Sadece belirli katmanlara hasar uygula
        [SerializeField] private ParticleSystem hitEffectPrefab; // Vuruş efekti
        [SerializeField] private AudioClip hitSound; // Vuruş sesi

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && hitSound != null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        public void Use(NetworkObject user, ItemHolder holder)
        {
            if (!HasStateAuthority) return;

            var camera = Camera.main;
            var rayOrigin = camera.transform.position + camera.transform.forward * 0.5f;
            var ray = new Ray(rayOrigin, camera.transform.forward);

            Vector3 hitPoint;
            if (Physics.Raycast(ray, out var hit, maxRange, hitLayerMask))
            {
                hitPoint = hit.point;
                var player = hit.rigidbody.GetComponent<Player>();
                if (player != null && player.GetComponent<NetworkObject>().Id != user.Id)
                {
                    var hitDirection = ray.direction.normalized;
                    RpcApplyDamageToPlayer(player.GetComponent<NetworkObject>().Id, damage, hitDirection, hitPoint);
                }
                else
                {
                    RpcPlayHitEffect(hitPoint);
                }
            }
            else
            {
                hitPoint = ray.origin + ray.direction * maxRange;
                RpcPlayHitEffect(hitPoint);
            }
        }


        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcApplyDamageToPlayer(NetworkId targetId, float damage, Vector3 hitDirection, Vector3 hitPoint)
        {
            var target = Runner.FindObject(targetId);
            if (target == null) return;

            var player = target.GetComponent<Player>();
            if (player != null)
            {
                player.TakeDamage(damage, hitDirection, knockbackForce);
                Debug.Log($"Applied {damage} damage to {target.name} at {hitPoint}");
            }

            // Vuruş efektini ve sesini oynat
            PlayHitEffect(hitPoint);
            PlayHitSound();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlayHitEffect(Vector3 hitPoint)
        {
            PlayHitEffect(hitPoint);
            PlayHitSound(); // Her zaman ses çal
        }

        private void PlayHitEffect(Vector3 hitPoint)
        {
            if (hitEffectPrefab != null)
            {
                var effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
                Destroy(effect.gameObject, 2f); // Efekti 2 saniye sonra yok et
            }
        }

        private void PlayHitSound()
        {
            if (hitSound != null && audioSource != null) audioSource.PlayOneShot(hitSound);
        }
    }
}