using Asteroids.HostSimple;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EdgegapServerManager : MonoBehaviour
{
    public static EdgegapServerManager Instance { get; private set; }
    private NetworkRunner _runnerInstance = null;
    private bool _gameStarted = false;
    private bool _isServer = false;
    [SerializeField] private NetworkRunner _networkRunnerPrefab = null;
    [SerializeField] private string _gameSceneName = null;
    [SerializeField] private string _EdgegapPortMapName = "GAMEPORT";
    [SerializeField] private ushort _serverPort = 5050;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        _isServer = Application.isBatchMode;

        if (_isServer && !_gameStarted)
        {
            string ip = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP");
            string portAsStr = Environment.GetEnvironmentVariable($"ARBITRIUM_PORT_{_EdgegapPortMapName.ToUpper()}_EXTERNAL");
            string requestId = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID");

            if (portAsStr == null)
            {
                throw new Exception($"Could not find port mapping, make sure your app version port name matches with \"{_EdgegapPortMapName}\"");
            }

            if (ip == null || !ushort.TryParse(portAsStr, out ushort port) || requestId == null)
            {
                throw new Exception("Unable to process Edgegap environment variables.");
            }

            NetAddress serverAddress = NetAddress.CreateFromIpPort(ip, port);
            //string roomCode = $"{requestId}.pr.edgegap.net";
            Debug.Log($"Starting server room with code {requestId}");
            StartServer(requestId, _gameSceneName, serverAddress);
        }
        else
        {
            Debug.Log($"Game started previously: {_gameStarted}");
        }
    }

    private async void StartServer(string roomName, string sceneName, NetAddress serverAddress)
    {
        _runnerInstance = FindObjectOfType<NetworkRunner>();
        if (_runnerInstance == null)
        {
            _runnerInstance = Instantiate(_networkRunnerPrefab);
        }

        _runnerInstance.ProvideInput = true;

        var startGameArgs = new StartGameArgs()
        {
            GameMode = GameMode.Server,
            SessionName = roomName,
            ObjectProvider = _runnerInstance.GetComponent<NetworkObjectPoolDefault>(),
            Address = NetAddress.Any(_serverPort),
            CustomPublicAddress = serverAddress,
        };

        var result = await _runnerInstance.StartGame(startGameArgs);

        if (!result.Ok)
        {
            Debug.LogError($"{result.ErrorMessage}");
        }
        else
        {
            if (_runnerInstance.IsServer)
            {
                _gameStarted = true;
                await _runnerInstance.LoadScene(sceneName);
            }
        }
    }
}
