using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.SceneManagement;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine.UI;
using System.Text.RegularExpressions;

namespace Asteroids.HostSimple
{
    // A utility class which defines the behaviour of the various buttons and input fields found in the Menu scene
    public class StartMenu : MonoBehaviour
    {
        [SerializeField] private NetworkRunner _networkRunnerPrefab = null;
        [SerializeField] private PlayerData _playerDataPrefab = null;

        [SerializeField] private TMP_InputField _nickName = null;

        // The Placeholder Text is not accessible through the TMP_InputField component so need a direct reference
        [SerializeField] private TextMeshProUGUI _nickNamePlaceholder = null;

        [SerializeField] private TMP_InputField _roomName = null;
        [SerializeField] private string _gameSceneName = null;

        private PlayerData playerData;
        [SerializeField] private TextMeshProUGUI _EdgegapConnectStatus = null;

        [SerializeField] private Button _EdgegapStartBtn = null;

        //You can use the value of your choice here
        private ushort serverPort = 5050;
        private bool edgegapMatchmaker = false;
        private bool retryStartGame = false;

        [SerializeField]
        private string _EdgegapPortMapName = "GAMEPORT";

        private NetworkRunner _runnerInstance = null;

        private void Start()
        {
            bool isServer = Application.isBatchMode;
            _EdgegapStartBtn.interactable = true;

            if (isServer)
            {
                string ip = Environment.GetEnvironmentVariable("ARBITRIUM_PUBLIC_IP");
                string portAsStr = Environment.GetEnvironmentVariable($"ARBITRIUM_PORT_{_EdgegapPortMapName}_EXTERNAL");
                string requestId = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID");

                if (ip == null || portAsStr == null || !ushort.TryParse(portAsStr, out ushort port) || requestId == null)
                {
                    throw new Exception("Unable to process Edgegap environment variables.");
                }

                NetAddress serverAddress = NetAddress.CreateFromIpPort(ip, port);
                string roomCode = $"{requestId}.pr.edgegap.net";
                StartGame(GameMode.Server, roomCode, _gameSceneName, serverAddress);
            }
        }

        // Attempts to start a new game session 
        public void StartHost()
        {
            SetPlayerData();
            StartGame(GameMode.AutoHostOrClient, _roomName.text, _gameSceneName);
        }

        public void StartClient()
        {
            SetPlayerData();
            StartGame(GameMode.Client, _roomName.text, _gameSceneName);
        }

        private void SetPlayerData()
        {
            playerData = FindFirstObjectByType<PlayerData>();
            if (playerData == null)
            {
                playerData = Instantiate(_playerDataPrefab);
            }

            if (string.IsNullOrWhiteSpace(_nickName.text))
            {
                playerData.SetNickName(_nickNamePlaceholder.text);
            }
            else
            {
                playerData.SetNickName(_nickName.text);
            }
        }

        private async void StartGame(GameMode mode, string roomName, string sceneName, NetAddress? serverAddress = null)
        {
            _runnerInstance = FindFirstObjectByType<NetworkRunner>();
            if (_runnerInstance == null)
            {
                _runnerInstance = Instantiate(_networkRunnerPrefab);
            }

            // Let the Fusion Runner know that we will be providing user input
            _runnerInstance.ProvideInput = true;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                ObjectProvider = _runnerInstance.GetComponent<NetworkObjectPoolDefault>(),
            };

            if (mode == GameMode.Server && serverAddress != null)
            {
                Debug.Log("Using specific address " + serverAddress);
                startGameArgs.Address = NetAddress.Any(serverPort);
                startGameArgs.CustomPublicAddress = serverAddress;
            }

            // GameMode.Host = Start a session with a specific name
            // GameMode.Client = Join a session with a specific name
            var result = await _runnerInstance.StartGame(startGameArgs);

            if (!result.Ok)
            {
                if (retryStartGame)
                {
                    retryStartGame = false;
                    StartGame(mode, roomName, sceneName);
                }
                else
                {
                    UpdateEdgegapConnectStatusTxt($"Unable to join room {roomName} due to {result.ShutdownReason}, see logs.");
                    Debug.LogError($"{result.ErrorMessage}");

                    if (edgegapMatchmaker)
                    {
                        EdgegapMatchmakerClientHandler.Instance.StopMatchmaking();
                    }
                }
            }
            else
            {
                if (_runnerInstance.IsServer)
                {
                    await _runnerInstance.LoadScene(sceneName);
                }
            }
        }

        public void StartEdgegap()
        {
            //edgegapMatchmaker = true;
            retryStartGame = true;
            SetPlayerData();

            //test
            StartGame(GameMode.Client, _roomName.text, _gameSceneName);
            //EdgegapMatchmakerClientHandler.Instance.InitialiseClient(StartGame, UpdateEdgegapConnectStatusTxt);
        }

        private void UpdateEdgegapConnectStatusTxt(string msg)
        {
            _EdgegapConnectStatus.text = msg;
        }
    }
}