using UnityEngine;

/// <summary>
/// This class is used to manage the interaction with the door.
/// </summary>
public class InteractionProxy : MonoBehaviour
{
    public DoorController door;

    private void OnTriggerEnter(Collider other)
    {
        if (door != null) door.OnPlayerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (door != null) door.OnPlayerExit(other);
    }
}