#region

using Fusion;
using UnityEngine;

#endregion

public class PlayerStats : NetworkBehaviour
{
    public const int MaxHealth = 100;

    [Networked]
    [OnChangedRender(nameof(OnHealthChanged))]
    public int Health { get; set; } = 100;

    // Yerel oyuncu hasar alırken çağır
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcTakeDamage(int amount)
    {
        Health = Mathf.Max(0, Health - amount);
    }

    private void OnHealthChanged()
    {
        if (Object.HasInputAuthority) // sadece kendi HUD’unu güncelle
            FusionHUD.Instance?.UpdateHealth(Health);
    }
}