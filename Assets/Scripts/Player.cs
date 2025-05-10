#region

using Fusion;
using TMPro;
using UnityEngine;

#endregion

[RequireComponent(typeof(CharacterController))]
public class Player : NetworkBehaviour
{
    [Header("Movement")] [SerializeField] private float playerSpeed = 2f;

    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravityValue = -9.81f;

    [Header("Interaction")] [SerializeField]
    private TextMeshProUGUI interactionText;

    [SerializeField] private float pickupDistance = 2f;
    [SerializeField] private float dropForceMagnitude = 5f;
    [SerializeField] private PlayerName playerName;

    private CharacterController _controller;
    private bool _jumpPressed;
    private PickupItem _nearbyItem;
    private bool _pickupPressed, _dropPressed;
    private Vector3 _velocity;

    [Networked] private NetworkObject HeldItem { get; set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!HasInputAuthority || !HasStateAuthority)
            return;

        if (Input.GetButtonDown("Jump")) _jumpPressed = true;
        if (Input.GetKeyDown(KeyCode.E)) _pickupPressed = true;
        if (Input.GetKeyDown(KeyCode.F)) _dropPressed = true;

        UpdateInteractionText();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasInputAuthority || !HasStateAuthority) return;
        var item = other.GetComponent<PickupItem>();
        if (item != null && !item.IsPickedUp) _nearbyItem = item;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!HasInputAuthority || !HasStateAuthority) return;
        var item = other.GetComponent<PickupItem>();
        if (item == _nearbyItem) _nearbyItem = null;
    }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        var fps = FindFirstObjectByType<FPSCameraController>();
        if (fps == null) return;

        playerName.SetPlayerName($"Player_{Random.Range(1000, 9999)}");
        fps.playerTransform = transform;
        fps.InitializeCamera();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority || !HasStateAuthority) return;

        if (_controller.isGrounded) _velocity.y = -1f;
        var inAx = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        var move = Vector3.zero;
        if (inAx.sqrMagnitude > 0f)
            if (Camera.main != null)
            {
                var cf = Camera.main.transform.forward;
                cf.y = 0;
                cf.Normalize();
                var cr = Camera.main.transform.right;
                cr.y = 0;
                cr.Normalize();
                move = (cf * inAx.z + cr * inAx.x).normalized * Runner.DeltaTime * playerSpeed;
            }

        _velocity.y += gravityValue * Runner.DeltaTime;
        if (_jumpPressed && _controller.isGrounded) _velocity.y += jumpForce;
        _controller.Move(move + _velocity * Runner.DeltaTime);
        _jumpPressed = false;

        if (_pickupPressed && HeldItem == null && _nearbyItem != null)
        {
            var dist = Vector3.Distance(transform.position, _nearbyItem.transform.position);
            if (dist <= pickupDistance)
            {
                Debug.Log($"Picking up {_nearbyItem.name}");
                _nearbyItem.Object.RequestStateAuthority();
                _nearbyItem.RpcSyncPickup(Object);
                HeldItem = _nearbyItem.Object;
            }
        }

        _pickupPressed = false;

        if (_dropPressed && HeldItem != null)
        {
            Debug.Log("Dropping held item");
            if (Camera.main != null)
            {
                var force = Camera.main.transform.forward * dropForceMagnitude;
                HeldItem.GetComponent<PickupItem>().RpcSyncDrop(force);
            }

            HeldItem.ReleaseStateAuthority();
            HeldItem = null;
        }

        _dropPressed = false;
    }

    private void UpdateInteractionText()
    {
        if (interactionText == null || !HasInputAuthority || !HasStateAuthority) return;

        if (HeldItem != null)
        {
            interactionText.text = "Press F to Drop";
            interactionText.gameObject.SetActive(true);
        }
        else if (_nearbyItem != null &&
                 Vector3.Distance(transform.position, _nearbyItem.transform.position) <= pickupDistance)
        {
            interactionText.text = "Press E to Pickup";
            interactionText.gameObject.SetActive(true);
        }
        else
        {
            interactionText.gameObject.SetActive(false);
        }
    }
}