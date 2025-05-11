using Fusion;
using UnityEngine;

namespace UsableItems
{
    /// <summary>
    /// This class is responsible for holding and manipulating grabbable objects.
    /// </summary>

    [DefaultExecutionOrder(-6)]
    public class ItemHolder : NetworkBehaviour
    {
        [Header("Holder Props")]
        [SerializeField] private float maxGrabDistance = 40f;
        [SerializeField] private float minGrabDistance = 1f;
        [SerializeField] private LineRenderer holdLine;

        [Header("Spring Settings")]
        [SerializeField] private float springStrength = 30f;
        [SerializeField] private float damperStrength = 20f;
        [SerializeField] private float maxForceMagnitude = 100f;

        [Header("Rotation Settings")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero; 
        [SerializeField] private Vector3 forwardAxis = Vector3.forward; 
        [SerializeField] private Vector3 upAxis = Vector3.up; 
        [SerializeField] private float rotationSmoothing = 15f; 

        [Header("Push Settings")]
        [SerializeField] private float pushForce = 1f;
        [SerializeField] private float minPushVelocity = 2f;

        private readonly float _smoothTime = 0.05f;

        private GrabbableState _grabbableState;
        private NetworkObject _holdedNetworkObject;
        private Rigidbody _holdedObject;
        private Camera _mainCamera;
        private float _pickDistance;
        private Vector3 _pickOffset;
        private Quaternion _currentRotation; 

        private Vector3 _smoothedLinePoint0, _smoothedLinePoint1, _smoothedLinePoint2;
        private Vector3 _velocity0, _velocity1, _velocity2;

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
                RpcRequestHold(inputData);
            }

            if (Input.GetKeyUp(KeyCode.Mouse0)) RpcRelease();

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0f) RpcAdjustDistance(scroll);

            if (_holdedObject != null && Input.GetKeyDown(KeyCode.E))
            {
                var usable = _holdedObject.GetComponent<IUsable>();
                if (usable != null) RpcUseItem();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _holdedObject == null) return;

            // Pozisyon güncelleme
            var barrelPos = transform.position;
            var midpoint = _mainCamera.transform.position + _mainCamera.transform.forward * _pickDistance * 0.5f;
            var endPoint = _holdedObject.position;

            IsLineActive = true;
            LinePoint0 = barrelPos;
            LinePoint1 = midpoint;
            LinePoint2 = endPoint;

            var targetPos = _mainCamera.transform.position + _mainCamera.transform.forward * _pickDistance;
            var error = targetPos - _holdedObject.position;
            var force = Vector3.ClampMagnitude(error * springStrength - _holdedObject.velocity * damperStrength,
                maxForceMagnitude);
            _holdedObject.AddForce(force, ForceMode.Force);

            var cameraForward = _mainCamera.transform.forward;
            var cameraUp = _mainCamera.transform.up;

            var forward = _holdedObject.transform.TransformDirection(-forwardAxis);
            var targetRot = Quaternion.FromToRotation(forward, cameraForward) * _holdedObject.rotation;
            targetRot *= Quaternion.Euler(rotationOffset); 

            var up = _holdedObject.transform.TransformDirection(upAxis);
            var targetUp = cameraUp;
            var upRot = Quaternion.FromToRotation(up, targetUp) * targetRot;

            _currentRotation = Quaternion.Slerp(_currentRotation, upRot, rotationSmoothing * Runner.DeltaTime);
            _holdedObject.MoveRotation(_currentRotation);
        }
    
        public override void Render()
        {
            holdLine.gameObject.SetActive(IsLineActive);
            if (!IsLineActive) return;

            var targetPoint0 = LinePoint0;
            var targetPoint1 = LinePoint1;
            var targetPoint2 = LinePoint2;

            if (HasInputAuthority)
            {
                targetPoint0 = transform.position;
                targetPoint1 = _mainCamera.transform.position + _mainCamera.transform.forward * _pickDistance * 0.5f;
                targetPoint2 = _holdedObject != null ? _holdedObject.position : LinePoint2;
            }

            _smoothedLinePoint0 = Vector3.SmoothDamp(_smoothedLinePoint0, targetPoint0, ref _velocity0, _smoothTime);
            _smoothedLinePoint1 = Vector3.SmoothDamp(_smoothedLinePoint1, targetPoint1, ref _velocity1, _smoothTime);
            _smoothedLinePoint2 = Vector3.SmoothDamp(_smoothedLinePoint2, targetPoint2, ref _velocity2, _smoothTime);

            DrawQuadraticBezierCurve(holdLine, _smoothedLinePoint0, _smoothedLinePoint1, _smoothedLinePoint2);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestHold(NetworkInputData input)
        {
            if (!Physics.Raycast(input.origin, input.direction, out var hit, maxGrabDistance)) return;

            if (hit.rigidbody == null || hit.rigidbody.CompareTag("Player")) return;

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
                Debug.Log($"Object {netObj.name} is already holded by another player");
                return;
            }

            Debug.Log($"Holding object: {netObj.name}, rigidbody={hit.rigidbody.name}");
            netObj.RequestStateAuthority();
            _holdedNetworkObject = netObj;
            _holdedObject = hit.rigidbody;
            _grabbableState = grabbable;
            _pickOffset = Vector3.zero;
            _pickDistance = Mathf.Clamp(hit.distance, minGrabDistance, maxGrabDistance);
            _currentRotation = _holdedObject.rotation; 

            SetHeldState(grabbable, true, netObj);
            holdLine.gameObject.SetActive(true);
            IsLineActive = true;

            var collisionHandler = _holdedObject.gameObject.AddComponent<HeldObjectCollisionHandler>();
            collisionHandler.Initialize(this, pushForce, minPushVelocity);

            Debug.Log(
                $"Hold setup: isKinematic={_holdedObject.isKinematic}, useGravity={_holdedObject.useGravity}, freezeRotation={_holdedObject.freezeRotation}");
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcUseItem()
        {
            if (_holdedObject != null)
            {
                var usable = _holdedObject.GetComponent<IUsable>();
                if (usable != null)
                {
                    usable.Use(_holdedNetworkObject, this);
                    Debug.Log($"Item used: {_holdedObject.name}");
                }
            }
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

                var collisionHandler = _holdedObject.GetComponent<HeldObjectCollisionHandler>();
                if (collisionHandler != null) Destroy(collisionHandler);

                Debug.Log(
                    $"{_holdedObject.name} physics state reset in RpcRelease: isKinematic={_holdedObject.isKinematic}, useGravity={_holdedObject.useGravity}, freezeRotation={_holdedObject.freezeRotation}");
            }

            _holdedObject = null;
            _holdedNetworkObject = null;
            _grabbableState = null;
            _currentRotation = Quaternion.identity; // Rotasyonu sıfırla
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcAdjustDistance(float delta)
        {
            _pickDistance = Mathf.Clamp(_pickDistance + delta, minGrabDistance, maxGrabDistance);
            Debug.Log($"Adjusting pick distance: new distance={_pickDistance}");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcApplyPushForce(NetworkId targetId, Vector3 force)
        {
            var targetObj = Runner.FindObject(targetId);
            if (targetObj == null) return;

            var rb = targetObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(force, ForceMode.Impulse);
                Debug.Log($"Applied push force to {targetObj.name}: force={force}");
            }
        }

        // Checks if the player can grab the object
        private bool CanBeGrabbed(GrabbableState grabbable)
        {
            return !grabbable.IsHeld;
        }

        // Sets the held state of the grabbable object
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

        // Draws a quadratic Bezier curve using the LineRenderer
        private void DrawQuadraticBezierCurve(LineRenderer line, Vector3 point0, Vector3 point1, Vector3 point2)
        {
            line.positionCount = 20;
            var t = 0f;
            var B = new Vector3(0, .7f, 0);
            for (var i = 0; i < line.positionCount; i++)
            {
                B = (1 - t) * (1 - t) * point0 + 2 * (1 - t) * t * point1 + t * t * point2;
                line.SetPosition(i, B);
                t += 1 / (float)line.positionCount;
            }
        }

        // Networked struct for input data
        private struct NetworkInputData : INetworkStruct
        {
            public Vector3 origin;
            public Vector3 direction;
        }
    }
}