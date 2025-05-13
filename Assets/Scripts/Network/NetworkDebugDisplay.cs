#region

using Network;
using TMPro;
using UnityEngine;

#endregion

public class NetworkDebugDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;

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

        NetworkManager.Instance.OnStageChanged.AddListener(UpdateDebugText);
        UpdateDebugText(NetworkManager.Instance.CurrentStage);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null) NetworkManager.Instance.OnStageChanged.RemoveListener(UpdateDebugText);
    }

    private void UpdateDebugText(NetworkManager.NetworkStage stage)
    {
        if (debugText == null) return;

        var debugInfo = $"Network Stage: {stage}\n" +
                        $"Session Name: {(string.IsNullOrEmpty(NetworkManager.Instance.runnerInstance?.SessionInfo.Name) ? "N/A" : NetworkManager.Instance.runnerInstance?.SessionInfo.Name)}\n" +
                        $"Max Players: {NetworkManager.Instance.runnerInstance?.SessionInfo.MaxPlayers}";
        debugText.text = debugInfo;
    }
}