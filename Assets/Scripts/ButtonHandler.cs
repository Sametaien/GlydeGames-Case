#region

using System;
using Lean.Gui;
using Network;
using TMPro;
using UnityEngine;

#endregion

/// <summary>
/// Handles button interactions for starting and joining games.
/// </summary>
public class ButtonHandler : MonoBehaviour
{
    [SerializeField] private LeanButton startGameButton;
    [SerializeField] private LeanButton joinGameButton;
    [SerializeField] private TMP_InputField sessionNameInput;
    [SerializeField] private int sceneIndex = -1;

    private void Start()
    {
        if (startGameButton == null)
        {
            Debug.LogError("Start Game Button is not assigned in ButtonHandler.");
            return;
        }

        if (joinGameButton == null)
        {
            Debug.LogError("Join Game Button is not assigned in ButtonHandler.");
            return;
        }

        if (sessionNameInput == null)
        {
            Debug.LogError("Session Name Input Field is not assigned in ButtonHandler.");
            return;
        }

        startGameButton.OnClick.AddListener(OnStartGameButtonClicked);
        joinGameButton.OnClick.AddListener(OnJoinGameButtonClicked);
    }

    private void OnDestroy()
    {
        if (startGameButton != null)
            startGameButton.OnClick.RemoveListener(OnStartGameButtonClicked);
        if (joinGameButton != null)
            joinGameButton.OnClick.RemoveListener(OnJoinGameButtonClicked);
    }

    private async void OnStartGameButtonClicked()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager instance not found.");
            return;
        }

        startGameButton.interactable = false;
        joinGameButton.interactable = false;

        try
        {
            var success = await NetworkManager.Instance.StartSharedClient();
            Debug.Log(success ? "Shared game started successfully." : "Failed to start shared game.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting shared game: {e}");
        }
    }

    private async void OnJoinGameButtonClicked()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager instance not found.");
            return;
        }

        var sessionName = sessionNameInput.text;


        startGameButton.interactable = false;
        joinGameButton.interactable = false;

        try
        {
            var success = await NetworkManager.Instance.JoinSharedClient(sessionName);
            Debug.Log(
                success ? $"Successfully joined session: {sessionName}" : $"Failed to join session: {sessionName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error joining shared game: {e}");
        }
    }
}