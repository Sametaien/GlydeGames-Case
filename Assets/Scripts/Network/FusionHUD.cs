#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using Fusion.Sockets;
using PlayerRelated;
using TMPro;
using UnityEngine;

#endregion

namespace Network
{
    public class FusionHUD : MonoBehaviour, INetworkRunnerCallbacks
    {
        private const float LOG_DURATION = 5f;
        private const int MAX_LOGS = 5;

        [Header("UI")] [SerializeField] private TMP_Text playerCountText;

        [SerializeField] private TMP_Text pingText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text eventsText;
        private readonly List<LogEntry> _eventLog = new();
        private string _lastPingText;
        private int _playerCount;

        private NetworkRunner _runner;

        public static FusionHUD Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            playerCountText.text = null;
            pingText.text = null;
            healthText.text = null;
            eventsText.text = null;
        }

        private void Update()
        {
            if (_runner == null) return;

            playerCountText.text = $"Oyuncular: {_playerCount}";

            if (_runner.IsRunning)
            {
                var rtt = _runner.GetPlayerRtt(_runner.LocalPlayer) * 1000f;
                var newPingText = rtt >= 0 ? $"Ping: {rtt:F1} ms" : "Ping: -";
                if (_lastPingText != newPingText)
                {
                    _lastPingText = newPingText;
                    pingText.text = newPingText;
                }
            }
            else
            {
                pingText.text = "Ping: -";
            }

            CleanOldLogs();
        }

        public void InjectRunner(NetworkRunner runner)
        {
            if (_runner != null) _runner.RemoveCallbacks(this);
            _runner = runner;
            _runner.AddCallbacks(this);
            _playerCount = _runner.ActivePlayers.Count();
        }

        public void RemoveRunner()
        {
            if (_runner == null) return;
            _runner.RemoveCallbacks(this);
            _runner = null;
            _playerCount = 0;
            _eventLog.Clear();
            eventsText.text = string.Empty;
            playerCountText.text = string.Empty;
            pingText.text = string.Empty;
            healthText.text = string.Empty;
        }

        public void LogEvent(string msg)
        {
            _eventLog.Add(new LogEntry
            {
                Message = $"{msg}\n",
                Timestamp = Time.time
            });

            if (_eventLog.Count > MAX_LOGS) _eventLog.RemoveAt(0);

            UpdateEventText();
        }

        private void CleanOldLogs()
        {
            var updated = false;
            for (var i = _eventLog.Count - 1; i >= 0; i--)
                if (Time.time - _eventLog[i].Timestamp > LOG_DURATION)
                {
                    _eventLog.RemoveAt(i);
                    updated = true;
                }

            if (updated) UpdateEventText();
        }

        private void UpdateEventText()
        {
            var sb = new StringBuilder();
            foreach (var log in _eventLog) sb.Append(log.Message);
            eventsText.text = sb.ToString();
        }

        public void UpdateHealth(int hp)
        {
            healthText.text = $"Can: {hp}/{PlayerStats.MaxHealth}";
        }

        private struct LogEntry
        {
            public string Message;
            public float Timestamp;
        }

        #region INetworkRunnerCallbacks

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            _playerCount = runner.ActivePlayers.Count();
            LogEvent($"{player} joined.");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            _playerCount = runner.ActivePlayers.Count();
            LogEvent($"{player} left.");
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        #endregion
    }
}