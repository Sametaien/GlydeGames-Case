#region

using UnityEngine;

#endregion

public class FPSCameraController : MonoBehaviour
{
    [SerializeField] public Transform playerTransform;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private Vector3 cameraOffset = new(0, 0.8f, 0); // Eye-level position
    [SerializeField] private float pitchMin = -80f;
    [SerializeField] private float pitchMax = 80f;

    private Camera _mainCamera;
    private float _pitch;

    private void Update()
    {
        if (_mainCamera == null || playerTransform == null) return;

        var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        playerTransform.Rotate(0, mouseX, 0);

        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        _mainCamera.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void InitializeCamera()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("Main Camera not found. Ensure the camera is tagged as MainCamera.");
            return;
        }

        if (playerTransform == null)
        {
            Debug.LogError("Player Transform not assigned. Assign the player in the Inspector.");
            return;
        }

        _mainCamera.transform.SetParent(playerTransform);
        _mainCamera.transform.localPosition = cameraOffset;
        _mainCamera.transform.localRotation = Quaternion.identity;

        _pitch = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}