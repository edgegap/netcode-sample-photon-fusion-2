using Asteroids.HostSimple;
using Fusion;
using Fusion.Sockets;
using System;
using UnityEngine;

public class EdgegapServerManager : MonoBehaviour
{
    public static EdgegapServerManager Instance { get; private set; }
    public static bool EdgegapEnabled = true;
    private NetworkRunner _runnerInstance = null;
    [SerializeField] private NetworkRunner _networkRunnerPrefab = null;
    [SerializeField] private string _gameSceneName = null;
    [SerializeField] private ushort _serverPort = 7777;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
    }

    void Start()
    {
        if (Application.isBatchMode)
        {
            string ip = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP");
            string portAsStr = Environment.GetEnvironmentVariable("ARBITRIUM_PORT_GAMEPORT_EXTERNAL");
            string requestId = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID");

            if (portAsStr == null)
            {
                throw new Exception(
                    "Could not find port mapping, make sure your app version port name is `gameport`"
                );
            }

            if (ip == null || !ushort.TryParse(portAsStr, out ushort port) || requestId == null)
            {
                throw new Exception("Unable to process Edgegap environment variables.");
            }

            NetAddress serverAddress = NetAddress.CreateFromIpPort(ip, port);
            string roomCode = $"{requestId}.pr.edgegap.net";
            Debug.Log($"Starting server room with code {roomCode}");
            StartServer(roomCode, _gameSceneName, serverAddress);
        }
    }

    private async void StartServer(string roomName, string sceneName, NetAddress serverAddress)
    {
        _runnerInstance = FindFirstObjectByType<NetworkRunner>();

        if (_runnerInstance == null)
        {
            _runnerInstance = Instantiate(_networkRunnerPrefab);
        }

        _runnerInstance.ProvideInput = true;

        var startGameArgs = new StartGameArgs()
        {
            GameMode = GameMode.Server,
            SessionName = roomName,
            Address = NetAddress.Any(_serverPort),
            CustomPublicAddress = serverAddress,
        };

        var result = await _runnerInstance.StartGame(startGameArgs);

        if (!result.Ok)
        {
            Debug.LogError($"ERROR while starting session: {result.ErrorMessage}");
        }
        else
        {
            if (_runnerInstance.IsServer)
            {
                await _runnerInstance.LoadScene(sceneName);
            }
        }
    }
}
