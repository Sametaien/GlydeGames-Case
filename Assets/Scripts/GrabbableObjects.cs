using Fusion;
using UnityEngine;

public class GrabbableState : NetworkBehaviour
{
    [Networked] public bool IsHeld { get; set; }
    [Networked] public NetworkObject HeldBy { get; set; }
}