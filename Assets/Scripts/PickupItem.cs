#region

using Fusion;
using UnityEngine;

#endregion

[RequireComponent(typeof(Rigidbody))]
public class PickupItem : NetworkBehaviour
{
    [Header("Collision & Mass")] [SerializeField]
    private float mass = 1f;

    [SerializeField] private Vector3 triggerSize = new(2.5f, 2.5f, 2.5f);

    [Header("Carry Settings")] [SerializeField]
    private float holdDistance = 2f;

    [SerializeField] private float springStrength = 50f;
    [SerializeField] private float damperStrength = 10f;
    [SerializeField] private float torqueStrength = 50f;
    [SerializeField] private float angularDamper = 5f;
    private Collider _physCol;

    private Rigidbody _rb;
    private BoxCollider _trigger;

    [Networked] public NetworkBool IsPickedUp { get; set; }
    [Networked] public NetworkObject PickedUpBy { get; set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.mass = mass;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _physCol = GetComponent<Collider>();
        _physCol.isTrigger = false;

        _trigger = gameObject.AddComponent<BoxCollider>();
        _trigger.size = triggerSize;
        _trigger.isTrigger = true;
        _trigger.enabled = false;
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        IsPickedUp = false;
        PickedUpBy = null;
        _trigger.enabled = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsPickedUp) return;

        var cam = Camera.main.transform;
        var targetPos = cam.position + cam.forward * holdDistance;

        var error = targetPos - _rb.position;
        var force = error * springStrength - _rb.velocity * damperStrength;
        _rb.AddForce(force, ForceMode.Force);

        var desiredRot = cam.rotation;
        var delta = desiredRot * Quaternion.Inverse(_rb.rotation);
        delta.ToAngleAxis(out var angleDeg, out var axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        if (axis.sqrMagnitude > 0.001f)
        {
            var torque = axis.normalized * (angleDeg * Mathf.Deg2Rad) * torqueStrength
                         - _rb.angularVelocity * angularDamper;
            _rb.AddTorque(torque, ForceMode.Force);
        }
    }

    public void Pickup(NetworkObject player)
    {
        if (!HasStateAuthority) return;
        IsPickedUp = true;
        PickedUpBy = player;
        _trigger.enabled = false;

        _rb.isKinematic = false;
        _rb.useGravity = false;
    }

    public void Drop(Vector3 dropForce)
    {
        if (!HasStateAuthority) return;
        IsPickedUp = false;
        PickedUpBy = null;
        _trigger.enabled = true;

        _rb.useGravity = true; 
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.AddForce(dropForce, ForceMode.Impulse);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcSyncPickup(NetworkObject pl)
    {
        Pickup(pl);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcSyncDrop(Vector3 f)
    {
        Drop(f);
    }
}