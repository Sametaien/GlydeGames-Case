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

    [Header("HUD")] [SerializeField] private FusionHUD fusionHud;

    [Header("Configuration")] [SerializeField]
    private NetworkRunner runnerPrefab;

    [SerializeField] private string defaultSessionName = "DefaultSession";
    [SerializeField] private int maxPlayerCount = 4;
    [SerializeField] private int defaultSceneIndex = 1;
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private string initialScenePath;

    [Header("Scene Events")] [SerializeField]
    private UnityEvent onSceneReady;

    [Header("UI References")] [SerializeField]
    private Button exitButton; 

    private NetworkStage currentStage = NetworkStage.Disconnected;
    public NetworkRunner runnerInstance;

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
            LogError("NetworkRunner prefab is not assigned.", "Configuration Error");
            CurrentStage = NetworkStage.None;
            return;
        }

        CurrentStage = NetworkStage.Disconnected;
        SetupExitButton();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        CleanupRunner();
    }

    public async Task<bool> StartSharedClient(string sessionName = null)
    {
        if (!CanStartSession())
            return false;

        CurrentStage = NetworkStage.Connecting;
        ToggleLoadingScreen(true);

        try
        {
            if (!await InitializeRunnerAsync())
                return HandleFailure("Failed to initialize runner.");

            sessionName ??= defaultSessionName;
            var sceneRef = SceneRef.FromIndex(defaultSceneIndex);
            var success = await StartGameAsync(GameMode.Shared, sceneRef, sessionName);

            CurrentStage = success ? NetworkStage.Connected : NetworkStage.Disconnected;
            ToggleLoadingScreen(false);
            return success;
        }
        catch (Exception e)
        {
            return HandleFailure($"Failed to start shared game: {e.Message}");
        }
    }

    public async Task<bool> JoinSharedClient(string sessionName)
    {
        if (!CanStartSession() || string.IsNullOrEmpty(sessionName))
        {
            LogError("Session name cannot be empty.", "Invalid Session Name");
            return false;
        }

        CurrentStage = NetworkStage.Connecting;
        ToggleLoadingScreen(true);

        try
        {
            if (!await InitializeRunnerAsync())
                return HandleFailure("Failed to initialize runner.");

            var sceneRef = SceneRef.FromIndex(defaultSceneIndex);
            var success = await StartGameAsync(GameMode.Shared, sceneRef, sessionName);

            CurrentStage = success ? NetworkStage.Connected : NetworkStage.Disconnected;
            ToggleLoadingScreen(false);
            return success;
        }
        catch (Exception e)
        {
            return HandleFailure($"Failed to join shared game: {e.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (CurrentStage == NetworkStage.Disconnected || runnerInstance == null)
        {
            CurrentStage = NetworkStage.Disconnected;
            return;
        }

        CurrentStage = NetworkStage.Disconnecting;
        ToggleLoadingScreen(true);

        try
        {
            await runnerInstance.Shutdown(shutdownReason: ShutdownReason.Ok);
            CleanupRunner();

            if (!string.IsNullOrEmpty(initialScenePath))
            {
                var asyncOp = SceneManager.LoadSceneAsync(initialScenePath);
                if (asyncOp != null)
                    while (!asyncOp.isDone)
                        await Task.Yield();
            }

            CurrentStage = NetworkStage.Disconnected;
            ToggleLoadingScreen(false);
        }
        catch (Exception e)
        {
            LogError($"Failed to disconnect: {e.Message}", "Disconnect Error");
            CurrentStage = NetworkStage.Disconnected;
            ToggleLoadingScreen(false);
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
        fusionHud.InjectRunner(runnerInstance);

        return true;
    }

    private async Task<bool> StartGameAsync(GameMode gameMode, SceneRef sceneRef, string sessionName)
    {
        if (runnerInstance == null)
        {
            LogError("NetworkRunner instance is null.", "Runner Error");
            return false;
        }

        try
        {
            ConfigureRunnerComponents();
            var startArgs = new StartGameArgs
            {
                SessionName = sessionName,
                GameMode = gameMode,
                PlayerCount = maxPlayerCount,
                Scene = sceneRef,
                SceneManager = runnerInstance.GetComponent<INetworkSceneManager>(),
                ObjectProvider = runnerInstance.GetComponent<INetworkObjectProvider>(),
                Address = NetAddress.Any()
            };

            await runnerInstance.StartGame(startArgs);
            await WaitForSceneLoadAsync();
            onSceneReady?.Invoke();

            return true;
        }
        catch (Exception e)
        {
            LogError($"Failed to start game: {e.Message}", "Start Game Error");
            return false;
        }
    }

    private void ConfigureRunnerComponents()
    {
        var sceneManager = runnerInstance.GetComponent<INetworkSceneManager>();
        if (sceneManager == null)
            sceneManager = runnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>();

        var objectProvider = runnerInstance.GetComponent<INetworkObjectProvider>();
        if (objectProvider == null)
            objectProvider = runnerInstance.gameObject.AddComponent<NetworkObjectProviderDefault>();
    }

    private async Task WaitForSceneLoadAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            tcs.SetResult(true);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        await tcs.Task;
        await Task.Yield(); 
    }

    private void SetupExitButton()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(() =>
            {
                debugWindow.TurnOn();
                debugText.text = "Exiting game...";
                _ = DisconnectAsync();
            });
        }
    }

    private bool CanStartSession()
    {
        if (CurrentStage != NetworkStage.Disconnected)
        {
            LogError($"Cannot start/join game while in stage: {CurrentStage}", "Invalid Stage");
            return false;
        }

        return true;
    }

    private bool HandleFailure(string errorMessage)
    {
        LogError(errorMessage, "Operation Failed");
        CurrentStage = NetworkStage.Disconnected;
        ToggleLoadingScreen(false);
        return false;
    }

    private void LogError(string message, string debugTitle)
    {
        Debug.LogError(message);
        debugWindow.TurnOn();
        debugText.text = $"<size=60>{debugTitle}</size>\n\n{message}";
    }

    private void ToggleLoadingScreen(bool active)
    {
        if (loadingScreen != null)
            loadingScreen.SetActive(active);
    }

    private void CleanupRunner()
    {
        if (runnerInstance != null)
        {
            fusionHud.RemoveRunner();
            Destroy(runnerInstance.gameObject);
            runnerInstance = null;
        }
    }
}