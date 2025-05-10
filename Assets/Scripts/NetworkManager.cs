#region

using System;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Lean.Gui;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#endregion

public class NetworkManager : MonoBehaviour
{
    public enum NetworkStage
    {
        None,
        Connecting,
        Connected,
        Disconnecting,
        Disconnected
    }

    [Header("Debug")] [SerializeField] private LeanWindow debugWindow;

    [SerializeField] private Text debugText;

    [Header("Configuration")] [SerializeField]
    private NetworkRunner runnerPrefab;

    [SerializeField] public string defaultSessionName = "DefaultSession";
    [SerializeField] public int maxPlayerCount = 4;
    [SerializeField] public int defaultSceneIndex = 1;
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private string initialScenePath;

    [Header("Scene Events")] [SerializeField]
    private UnityEvent onSceneReady;

    private NetworkStage currentStage = NetworkStage.Disconnected;
    private NetworkRunner runnerInstance;

    #region Singleton

    public static NetworkManager Instance { get; private set; }

    #endregion

    public NetworkStage CurrentStage
    {
        get => currentStage;
        private set
        {
            if (currentStage != value)
            {
                currentStage = value;
                OnStageChanged?.Invoke(value);
            }
        }
    }

    public UnityEvent<NetworkStage> OnStageChanged { get; } = new();

    private void Awake()
    {
        Application.targetFrameRate = 120;
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(initialScenePath))
            initialScenePath = SceneManager.GetActiveScene().path;

        if (runnerPrefab == null)
        {
            Debug.LogError("NetworkRunner prefab is not assigned in NetworkManager.");
            CurrentStage = NetworkStage.None;
            return;
        }

        CurrentStage = NetworkStage.Disconnected;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (runnerInstance != null)
        {
            Destroy(runnerInstance.gameObject);
            runnerInstance = null;
        }
    }

    public async Task<bool> StartSharedClient(string sessionName = null)
    {
        if (CurrentStage != NetworkStage.Disconnected)
        {
            Debug.LogError($"Cannot start shared game while in stage: {CurrentStage}");
            return false;
        }

        CurrentStage = NetworkStage.Connecting;
        if (loadingScreen != null) loadingScreen.SetActive(true);

        try
        {
            if (!await InitializeRunnerAsync())
            {
                CurrentStage = NetworkStage.Disconnected;
                if (loadingScreen != null) loadingScreen.SetActive(false);
                return false;
            }

            if (string.IsNullOrEmpty(sessionName))
                sessionName = defaultSessionName;

            var sceneRef = SceneRef.FromIndex(defaultSceneIndex);
            var success = await StartGameAsync(GameMode.Shared, sceneRef, sessionName);

            CurrentStage = success ? NetworkStage.Connected : NetworkStage.Disconnected;
            if (loadingScreen != null) loadingScreen.SetActive(false);
            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start shared game: {e}");
            debugWindow.TurnOn();
            debugText.text = "Something went wrong while starting the game.";
            CurrentStage = NetworkStage.Disconnected;
            if (loadingScreen != null) loadingScreen.SetActive(false);
            return false;
        }
    }

    public async Task<bool> JoinSharedClient(string sessionName)
    {
        if (CurrentStage != NetworkStage.Disconnected)
        {
            Debug.LogError($"Cannot join shared game while in stage: {CurrentStage}");
            return false;
        }

        if (string.IsNullOrEmpty(sessionName))
        {
            Debug.LogError("[NetworkManager] Session name cannot be empty.");
            debugWindow.TurnOn();
            debugText.text = "<size=60>Error!</size> \n \n Session name cannot be empty.";
            return false;
        }

        CurrentStage = NetworkStage.Connecting;
        if (loadingScreen != null) loadingScreen.SetActive(true);

        try
        {
            if (!await InitializeRunnerAsync())
            {
                CurrentStage = NetworkStage.Disconnected;
                if (loadingScreen != null) loadingScreen.SetActive(false);
                return false;
            }

            var sceneRef = SceneRef.FromIndex(defaultSceneIndex);
            var success = await StartGameAsync(GameMode.Shared, sceneRef, sessionName);

            CurrentStage = success ? NetworkStage.Connected : NetworkStage.Disconnected;
            if (loadingScreen != null) loadingScreen.SetActive(false);
            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join shared game: {e}");
            debugWindow.TurnOn();
            debugText.text = "Something went wrong while joining the game.";
            CurrentStage = NetworkStage.Disconnected;
            if (loadingScreen != null) loadingScreen.SetActive(false);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        Debug.Log("Disconnecting from the game...");

        if (CurrentStage == NetworkStage.Disconnected || runnerInstance == null)
        {
            CurrentStage = NetworkStage.Disconnected;
            return;
        }

        CurrentStage = NetworkStage.Disconnecting;
        if (loadingScreen != null) loadingScreen.SetActive(true);

        try
        {
            await runnerInstance.Shutdown(shutdownReason: ShutdownReason.Ok);
            Destroy(runnerInstance.gameObject);
            runnerInstance = null;

            if (!string.IsNullOrEmpty(initialScenePath))
            {
                var loadSceneTask = SceneManager.LoadSceneAsync(initialScenePath);
                while (!loadSceneTask.isDone) await Task.Yield();
            }

            CurrentStage = NetworkStage.Disconnected;
            if (loadingScreen != null) loadingScreen.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to disconnect: {e}");
            CurrentStage = NetworkStage.Disconnected;
            if (loadingScreen != null) loadingScreen.SetActive(false);
        }
    }

    private async Task<bool> InitializeRunnerAsync()
    {
        if (runnerInstance != null)
        {
            Debug.LogWarning("NetworkRunner instance already exists.");
            return true;
        }

        runnerInstance = Instantiate(runnerPrefab);
        runnerInstance.name = "NetworkRunner";
        runnerInstance.ProvideInput = true;
        DontDestroyOnLoad(runnerInstance.gameObject);

        if (transform.parent != null)
            transform.parent = null;

        DontDestroyOnLoad(gameObject);

        return true;
    }

    private async Task<bool> StartGameAsync(GameMode gameMode, SceneRef sceneRef, string sessionName)
    {
        if (runnerInstance == null)
        {
            Debug.LogError("NetworkRunner instance is null.");
            return false;
        }

        try
        {
            var sceneManager = runnerInstance.GetComponent<INetworkSceneManager>();
            if (sceneManager == null)
                sceneManager = runnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>();

            var objectProvider = runnerInstance.GetComponent<INetworkObjectProvider>();
            if (objectProvider == null)
                objectProvider = runnerInstance.gameObject.AddComponent<NetworkObjectProviderDefault>();

            var startArgs = new StartGameArgs
            {
                SessionName = sessionName,
                GameMode = gameMode,
                PlayerCount = maxPlayerCount,
                Scene = sceneRef,
                SceneManager = sceneManager,
                ObjectProvider = objectProvider,
                Address = NetAddress.Any()
            };

            await runnerInstance.StartGame(startArgs);

            await WaitForSceneLoadAsync();
            onSceneReady?.Invoke();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start game: {e}");
            debugWindow.TurnOn();
            debugText.text = "Something went wrong while starting the game.";
            return false;
        }
    }

    private async Task WaitForSceneLoadAsync()
    {
        while (!SceneManager.GetActiveScene().isLoaded)
            await Task.Yield();
        await Task.Yield(); // optional extra frame to ensure UI elements are ready

        // Add ExitButton event setup
        var exitButton = FindFirstObjectByType<Button>();
        Debug.Log($"ExitButton found: {exitButton != null}");
        exitButton.onClick.RemoveAllListeners();
        Debug.Log("ExitButton listeners cleared");

        try
        {
            Debug.Log("Trying to add ExitButton listener");
            exitButton.onClick.AddListener(() =>
            {
                Debug.Log("ExitButton listener added");
                debugWindow.TurnOn();
                debugText.text = "Exiting game...";
                _ = DisconnectAsync();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}