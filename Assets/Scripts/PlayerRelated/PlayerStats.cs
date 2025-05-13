#region

using Fusion;
using Network;
using UnityEngine;

#endregion

namespace PlayerRelated
{
    public class PlayerStats : NetworkBehaviour
    {
        public const int MaxHealth = 100;

        [Networked]
        [OnChangedRender(nameof(OnHealthChanged))]
        public int Health { get; set; } = 100;

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcTakeDamage(int amount)
        {
            Health = Mathf.Max(0, Health - amount);
        }

        private void OnHealthChanged()
        {
            if (Object.HasInputAuthority) 
                FusionHUD.Instance?.UpdateHealth(Health);
        }
    }
}