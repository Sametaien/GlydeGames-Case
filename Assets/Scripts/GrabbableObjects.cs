using Fusion;
using UnityEngine;

/// <summary>
/// We are using this to manage the state of the grabbable object.
/// </summary>
public class GrabbableState : NetworkBehaviour
{
    [Networked] public bool IsHeld { get; set; }
    [Networked] public NetworkObject HeldBy { get; set; }
}