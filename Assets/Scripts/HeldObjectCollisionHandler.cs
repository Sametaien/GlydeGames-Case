#region

using Fusion;
using UnityEngine;

#endregion

public class HeldObjectCollisionHandler : MonoBehaviour
{
    private ItemHolder _itemHolder;
    private float _minPushVelocity;
    private float _pushForce;

    private void OnCollisionEnter(Collision collision)
    {
        if (_itemHolder == null || !_itemHolder.HasStateAuthority) return;

        var otherRb = collision.rigidbody;
        if (otherRb == null || otherRb == _itemHolder.GetComponent<Rigidbody>()) return;

        var relativeVelocity = collision.relativeVelocity.magnitude;
        if (relativeVelocity < _minPushVelocity) return;

        var netObj = otherRb.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.Log($"Collided object {otherRb.name} has no NetworkObject, skipping push");
            return;
        }

        var contact = collision.GetContact(0);
        var forceDirection = (contact.point - otherRb.position).normalized;
        var force = forceDirection * _pushForce;

        _itemHolder.RpcApplyPushForce(netObj.Id, force);
        Debug.Log($"Collision with {otherRb.name}, relative velocity={relativeVelocity}, applying force={force}");
    }

    public void Initialize(ItemHolder itemHolder, float pushForce, float minPushVelocity)
    {
        _itemHolder = itemHolder;
        _pushForce = pushForce;
        _minPushVelocity = minPushVelocity;
    }
}