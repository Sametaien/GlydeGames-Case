#region

using Fusion;
using UnityEngine;

#endregion

public class ItemHolder : NetworkBehaviour
{
    [Header("Holder Props")] [SerializeField]
    private float maxGrabDistance = 40f;

    [SerializeField] private float minGrabDistance = 1f;
    [SerializeField] private LineRenderer holdLine;

    [Header("Spring Settings")] [SerializeField]
    private float springStrength = 50f;

    [SerializeField] private float damperStrength = 10f;
    [SerializeField] private float torqueStrength = 50f;
    [SerializeField] private float angularDamper = 5f;
    private GrabbableState _grabbableState;
    
    
    
    private NetworkObject _holdedNetworkObject;
    private Rigidbody _holdedObject;
    private Camera _mainCamera;
    private float _pickDistance;
    private Vector3 _pickOffset;
    private Quaternion _rotationOffset;

    // Networked properties for LineRenderer synchronization
    [Networked] private NetworkBool IsLineActive { get; set; }
    [Networked] private Vector3 LinePoint0 { get; set; }
    [Networked] private Vector3 LinePoint1 { get; set; }
    [Networked] private Vector3 LinePoint2 { get; set; }

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (holdLine == null)
        {
            var obj = new GameObject("HoldLine");
            holdLine = obj.AddComponent<LineRenderer>();
            holdLine.startWidth = 0.02f;
            holdLine.endWidth = 0.02f;
            holdLine.useWorldSpace = true;
            holdLine.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!HasInputAuthority) return;

        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            var ray = _mainCamera.ViewportPointToRay(Vector3.one * 0.5f);
            var inputData = new NetworkInputData
            {
                origin = ray.origin,
                direction = ray.direction
            };
            Debug.Log($"Requesting hold: Ray origin={ray.origin}, direction={ray.direction}");
            RpcRequestHold(inputData);
        }

        if (Input.GetKeyUp(KeyCode.Mouse0))
        {
            Debug.Log("Releasing object via Mouse0 up");
            RpcRelease();
        }

        var scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0f) RpcAdjustDistance(scroll);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _holdedObject == null) return;

        // Calculate line points for synchronization
        var barrelPos = transform.position;
        var midpoint = _mainCamera.transform.position + _mainCamera.transform.forward * _pickDistance * 0.5f;
        var endPoint = _holdedObject.position + _holdedObject.transform.TransformVector(_pickOffset);

        // Update networked properties
        IsLineActive = true;
        LinePoint0 = barrelPos;
        LinePoint1 = midpoint;
        LinePoint2 = endPoint;

        var targetPos = _mainCamera.transform.position + _mainCamera.transform.forward * _pickDistance -
                        _holdedObject.transform.TransformVector(_pickOffset);
        var error = targetPos - _holdedObject.position;
        var force = error * springStrength - _holdedObject.velocity * damperStrength;
        _holdedObject.AddForce(force, ForceMode.Force);

        var desiredRot = _mainCamera.transform.rotation * _rotationOffset;
        var delta = desiredRot * Quaternion.Inverse(_holdedObject.rotation);
        delta.ToAngleAxis(out var angleDeg, out var axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        if (axis.sqrMagnitude > 0.001f)
        {
            var torque = axis.normalized * (angleDeg * Mathf.Deg2Rad) * torqueStrength
                         - _holdedObject.angularVelocity * angularDamper;
            _holdedObject.AddTorque(torque, ForceMode.Force);
        }

        if (_holdedObject.isKinematic)
            Debug.LogWarning($"Held object {_holdedObject.name} is unexpectedly kinematic during hold!");
    }

    public override void Render()
    {
        // Update LineRenderer on all clients
        holdLine.gameObject.SetActive(IsLineActive);
        if (IsLineActive) DrawQuadraticBezierCurve(holdLine, LinePoint0, LinePoint1, LinePoint2);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcRequestHold(NetworkInputData input)
    {
        Debug.Log($"RpcRequestHold called: origin={input.origin}, direction={input.direction}");
        if (!Physics.Raycast(input.origin, input.direction, out var hit, maxGrabDistance))
        {
            Debug.Log("Raycast failed: No hit within maxGrabDistance");
            return;
        }

        if (hit.rigidbody == null || hit.rigidbody.CompareTag("Player"))
        {
            Debug.Log($"Raycast hit invalid: rigidbody={hit.rigidbody}, tag={hit.collider?.tag}");
            return;
        }

        var netObj = hit.rigidbody.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.Log("Raycast hit object has no NetworkObject");
            return;
        }

        var grabbable = hit.rigidbody.GetComponent<GrabbableState>();
        if (grabbable == null)
        {
            Debug.Log("Raycast hit object has no GrabbableState component");
            return;
        }

        if (!CanBeGrabbed(grabbable))
        {
            Debug.Log($"Object {netObj.name} is already held by another player");
            return;
        }

        Debug.Log($"Holding object: {netObj.name}, rigidbody={hit.rigidbody.name}");
        netObj.RequestStateAuthority();
        _holdedNetworkObject = netObj;
        _holdedObject = hit.rigidbody;
        _grabbableState = grabbable;
        _pickOffset = hit.transform.InverseTransformVector(hit.point - hit.transform.position);
        _rotationOffset = Quaternion.Inverse(_mainCamera.transform.rotation) * hit.rigidbody.rotation;
        _pickDistance = Mathf.Clamp(hit.distance, minGrabDistance, maxGrabDistance);

        SetHeldState(grabbable, true, netObj);
        holdLine.gameObject.SetActive(true);
        IsLineActive = true;

        Debug.Log(
            $"Hold setup: isKinematic={_holdedObject.isKinematic}, useGravity={_holdedObject.useGravity}, freezeRotation={_holdedObject.freezeRotation}");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RpcRelease()
    {
        Debug.Log(
            $"RpcRelease called: _holdedObject={_holdedObject?.name}, _holdedNetworkObject={_holdedNetworkObject?.name}");
        if (_holdedObject != null && _grabbableState != null)
        {
            holdLine.gameObject.SetActive(false);
            IsLineActive = false;
            SetHeldState(_grabbableState, false, null);

            Debug.Log(
                $"{_holdedObject.name} physics state reset in RpcRelease: isKinematic={_holdedObject.isKinematic}, useGravity={_holdedObject.useGravity}, freezeRotation={_holdedObject.freezeRotation}");
        }

        _holdedObject = null;
        _holdedNetworkObject = null;
        _grabbableState = null;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcAdjustDistance(float delta)
    {
        _pickDistance = Mathf.Clamp(_pickDistance + delta, minGrabDistance, maxGrabDistance);
        Debug.Log($"Adjusting pick distance: new distance={_pickDistance}");
    }

    private bool CanBeGrabbed(GrabbableState grabbable)
    {
        return !grabbable.IsHeld;
    }

    private void SetHeldState(GrabbableState grabbable, bool isHeld, NetworkObject holder)
    {
        grabbable.IsHeld = isHeld;
        grabbable.HeldBy = isHeld ? holder : null;
        Debug.Log(
            $"{grabbable.gameObject.name} held state set: IsHeld={isHeld}, HeldBy={(isHeld ? holder?.name : "none")}");

        var rb = grabbable.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = !isHeld;
            rb.freezeRotation = isHeld;
            rb.collisionDetectionMode = isHeld ? CollisionDetectionMode.Continuous : CollisionDetectionMode.Discrete;
            rb.isKinematic = false;
            if (!isHeld) rb.WakeUp();

            Debug.Log(
                $"{rb.name} physics state set: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, freezeRotation={rb.freezeRotation}");
        }
    }

    private void DrawQuadraticBezierCurve(LineRenderer line, Vector3 point0, Vector3 point1, Vector3 point2)
    {
        line.positionCount = 20;
        var t = 0f;
        var B = new Vector3(0, 0, 0);
        for (var i = 0; i < line.positionCount; i++)
        {
            B = (1 - t) * (1 - t) * point0 + 2 * (1 - t) * t * point1 + t * t * point2;
            line.SetPosition(i, B);
            t += 1 / (float)line.positionCount;
        }
    }

    private struct NetworkInputData : INetworkStruct
    {
        public Vector3 origin;
        public Vector3 direction;
    }
}