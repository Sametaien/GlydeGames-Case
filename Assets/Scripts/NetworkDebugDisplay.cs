using UnityEngine;
using TMPro;

public class NetworkDebugDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText; // Reference to TextMeshProUGUI component

    private void Start()
    {
        if (debugText == null)
        {
            Debug.LogError("Debug Text (TextMeshProUGUI) is not assigned in NetworkDebugDisplay.");
            return;
        }

        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager instance not found.");
            debugText.text = "Error: NetworkManager not found.";
            return;
        }

        // Subscribe to stage changes
        NetworkManager.Instance.OnStageChanged.AddListener(UpdateDebugText);
        // Initial update
        UpdateDebugText(NetworkManager.Instance.CurrentStage);
    }

    private void UpdateDebugText(NetworkManager.NetworkStage stage)
    {
        if (debugText == null) return;

        // Gather debug information
        string debugInfo = $"Network Stage: {stage}\n" +
                           $"Session Name: {(string.IsNullOrEmpty(NetworkManager.Instance.defaultSessionName) ? "N/A" : NetworkManager.Instance.defaultSessionName)}\n" +
                           $"Max Players: {NetworkManager.Instance.maxPlayerCount}\n" +
                           $"Scene Index: {NetworkManager.Instance.defaultSceneIndex}";

        debugText.text = debugInfo;
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnStageChanged.RemoveListener(UpdateDebugText);
        }
    }
}