#region

using Fusion;
using TMPro;
using UnityEngine;

#endregion

/// <summary>
/// Controls the door's opening and closing behavior.
/// </summary>
public class DoorController : NetworkBehaviour
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float closeAngle;
    [SerializeField] private float openSpeed = 5f;
    [SerializeField] private Vector3 boxColliderSize = new(1f, 1f, 1f);
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private GameObject interactionTrigger;

    private BoxCollider _interactionCollider;
    private bool _isPlayerInRange; 
    private bool _isToggling;
    private bool _prevIsOpen;
    private Quaternion _targetRotation;
    [Networked] private bool IsOpen { get; set; }

    private void Awake()
    {
        _targetRotation = transform.localRotation;

        if (interactionTrigger != null)
        {
            _interactionCollider = interactionTrigger.AddComponent<BoxCollider>();
            _interactionCollider.size = boxColliderSize;
            _interactionCollider.isTrigger = true;
            Debug.Log(
                $"Interaction trigger set up. Size: {_interactionCollider.size}, IsTrigger: {_interactionCollider.isTrigger}");
        }

        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_isPlayerInRange && !_isToggling && Input.GetKeyDown(KeyCode.E)) RpcRequestToggleDoor(!IsOpen);
    }

    // We use this solution because we need to separate the trigger collider from the door itself.
    private void OnTriggerEnter(Collider other)
    {
        OnPlayerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        OnPlayerExit(other);
    }

    public override void FixedUpdateNetwork()
    {
        if (_isToggling)
        {
            transform.localRotation =
                Quaternion.RotateTowards(transform.localRotation, _targetRotation, openSpeed * Runner.DeltaTime * 15f);
            if (Quaternion.Angle(transform.localRotation, _targetRotation) < 0.1f)
            {
                transform.localRotation = _targetRotation;
                _isToggling = false;
            }
        }

        if (IsOpen != _prevIsOpen)
        {
            _prevIsOpen = IsOpen;
            UpdateInteractionText();
        }
    }


    public void OnPlayerEnter(Collider other)
    {
        var netObj = other.GetComponent<NetworkObject>();

        if (netObj != null && netObj.HasInputAuthority)
        {
            Debug.Log($"OnPlayerEnter: {other.name}, NetworkObject is null: {netObj == null}");
            _isPlayerInRange = true;
            if (interactionText != null)
            {
                interactionText.text = IsOpen ? "Press E to Close" : "Press E to Open";
                interactionText.gameObject.SetActive(true);
            }
        }
    }

    public void OnPlayerExit(Collider other)
    {
        var netObj = other.GetComponent<NetworkObject>();

        if (netObj != null && netObj.HasInputAuthority)
        {
            Debug.Log($"OnPlayerExit: {other.name}, NetworkObject is null: {netObj == null}");
            _isPlayerInRange = false;
            if (interactionText != null) interactionText.gameObject.SetActive(false);
        }
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcRequestToggleDoor(bool open)
    {
        IsOpen = open;
        _targetRotation = open ? Quaternion.Euler(0, openAngle, 0) : Quaternion.Euler(0, closeAngle, 0);
        _isToggling = true;
    }

    private void UpdateInteractionText()
    {
        if (interactionText != null && _isPlayerInRange)
            interactionText.text = IsOpen ? "Press E to Close" : "Press E to Open";
    }
}