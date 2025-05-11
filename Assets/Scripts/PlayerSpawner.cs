#region

using System;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

#endregion

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkRunner runner;

    private void Awake()
    {
        runner = GetComponent<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogError("NetworkRunner component not found on PlayerSpawner.");
            return;
        }

        if (playerPrefab == null) Debug.LogError("PlayerPrefab is not assigned in PlayerSpawner.");
    }

    public void PlayerJoined(PlayerRef player)
    {
        Debug.Log($"PlayerJoined called for player {player}, LocalPlayer: {runner.LocalPlayer}");

        if (player == runner.LocalPlayer)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("PlayerPrefab is not assigned in PlayerSpawner.");
                return;
            }

            var spawnPosition = new Vector3(Random.Range(23f, 25f), 1f, Random.Range(-8f, -10f));
            try
            {
                var spawnedPlayer = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
                runner.SetPlayerObject(player, spawnedPlayer);
                runner.SetPlayerAlwaysInterested(player, spawnedPlayer,true);

                if (spawnedPlayer == null)
                    Debug.LogError($"Spawned player is null for player {player}.");
                else
                    Debug.Log($"Player {player} spawned at {spawnPosition}. Spawned object: {spawnedPlayer.name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to spawn player {player}: {e.Message}\nStack Trace: {e.StackTrace}");
            }
        }
        else
        {
            Debug.Log($"Player {player} joined. Not the local player; waiting for their client to spawn.");
        }
    }
}