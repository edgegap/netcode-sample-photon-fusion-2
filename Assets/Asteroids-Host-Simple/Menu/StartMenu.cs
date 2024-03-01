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
        
        private bool startDeploy = false;
        private bool tryJoinEdgegap = false;

        //You can use the value of your choice here
        private ushort serverPort = 5050;

        private NetworkRunner _runnerInstance = null;

        private void Start()
        {
            _EdgegapConnectStatus.text = "";

            if (EdgegapManager.IsServer())
            {
                var getPortAndStartServer = EdgegapAPIInterface.GetPublicIpAndPortFromServer((ip, port) =>
                {
                    var serverAddress = NetAddress.CreateFromIpPort(ip, port);
                    StartGame(GameMode.Server, EdgegapManager.EdgegapRoomCode, _gameSceneName, serverAddress);
                });

                StartCoroutine(getPortAndStartServer);
            }
        }

        private void Update()
        {
            if (EdgegapManager.EdgegapPreServerMode && EdgegapManager.TransferingToEdgegapServer)
            {
                // launch game again with edgegap server room code
                Debug.Log("Transferring to Edgegap");
                _EdgegapConnectStatus.text = "Deployment ready, connecting...";

                EdgegapManager.TransferingToEdgegapServer = false;
                EdgegapManager.EdgegapPreServerMode = false;
                startDeploy = false;

                var launchAfterDelay = RunAfterTime(0.2f, () => StartGame(GameMode.Client, EdgegapManager.EdgegapRoomCode, _gameSceneName));
                StartCoroutine(launchAfterDelay);
            }
            else if(tryJoinEdgegap)
            {
                tryJoinEdgegap = false;

                Debug.Log($"checking for room: {_roomName.text}");
                _EdgegapConnectStatus.text = $"Attempting to connect to room {_roomName.text} with Edgegap...";

                StartGame(GameMode.Client, _roomName.text, _gameSceneName);
            }
            else if (startDeploy && playerData.GetIpAddress() is not null)
            {
                startDeploy = false;

                Debug.Log("Initiating deployment");
                _EdgegapConnectStatus.text = $"Room {_roomName.text} not found, deploying Edgegap server...";

                string[] ips = { playerData.GetIpAddress() };
                StartCoroutine(EdgegapManager.Instance.Deploy(_roomName.text, ips, OnEdgegapServerReady));
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
            playerData = FindObjectOfType<PlayerData>();
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

            if (EdgegapManager.EdgegapPreServerMode)
            {
                var crtn = EdgegapManager.Instance.GetPublicIpAddress(ip => playerData.SetIpAddress(ip));
                StartCoroutine(crtn);
            }
        }

        private async void StartGame(GameMode mode, string roomName, string sceneName, NetAddress? serverAddress = null)
        {
            _runnerInstance = FindObjectOfType<NetworkRunner>();
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
                startDeploy = true;
            }
            else
            {
                startDeploy = false;
                EdgegapManager.EdgegapPreServerMode = false;

                if (_runnerInstance.IsServer)
                {
                    await _runnerInstance.LoadScene(sceneName);
                }
            }
        }

        public void StartEdgegap()
        {
            EdgegapManager.EdgegapPreServerMode = true;
            SetPlayerData();
            tryJoinEdgegap = true;
        }

        IEnumerator RunAfterTime(float timeInSeconds, Action action)
        {
            yield return new WaitForSeconds(timeInSeconds);
            action();
        }

        public void OnEdgegapServerReady(string roomCode)
        {
            EdgegapManager.EdgegapRoomCode = roomCode;
            EdgegapManager.TransferingToEdgegapServer = true;
        }
    }
}