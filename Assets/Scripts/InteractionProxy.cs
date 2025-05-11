using UnityEngine;

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