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
        
        private bool startDeploy = false;
        private bool tryJoinEdgegap = false;
        bool waiting = false;

        //You can use the value of your choice here
        private ushort serverPort = 5050;

        private NetworkRunner _runnerInstance = null;

        private void Start()
        {
            _roomName.onValueChanged.AddListener(ValidateRoomName);
            _EdgegapStartBtn.interactable = false;
            _nickName.onValueChanged.AddListener(value => CheckForSpecialcharacters(value, _nickName));
            _EdgegapConnectStatus.text = "Please enter a room name to test with Edgegap.";
            EdgegapManager.EdgegapPreServerMode = false;
            waiting = false;

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
                _EdgegapConnectStatus.text = "Deployment ready, attempting to connect...";
                startDeploy = false;

                var launchAfterDelay = RunAfterTime(0.5f, () => TryConnectDeployment(EdgegapManager.EdgegapRoomCode, _gameSceneName));
                StartCoroutine(launchAfterDelay);
            }
            else if(tryJoinEdgegap)
            {
                tryJoinEdgegap = false;

                _EdgegapConnectStatus.text = $"Attempting to connect to room {_roomName.text} with Edgegap...";

                StartGame(GameMode.Client, _roomName.text, _gameSceneName);
            }
            else if (startDeploy && playerData.GetIpAddress() is not null)
            {
                startDeploy = false;

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

            if (!result.Ok && EdgegapManager.EdgegapPreServerMode)
            {
                startDeploy = true;
            }
            else
            {
                startDeploy = false;

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

        public void OnEdgegapServerReady(string roomCode)
        {
            EdgegapManager.EdgegapRoomCode = roomCode;
            EdgegapManager.TransferingToEdgegapServer = true;
        }

        IEnumerator RunAfterTime(float timeInSeconds, Action action)
        {
            if (!waiting)
            {
                waiting = true;
                yield return new WaitForSeconds(timeInSeconds);
                action();
            }     
        }

        private async void TryConnectDeployment(string roomName, string sceneName)
        {
            Debug.Log("Attempting to connect...");

            _runnerInstance = FindObjectOfType<NetworkRunner>();
            if (_runnerInstance == null)
            {
                _runnerInstance = Instantiate(_networkRunnerPrefab);
            }
            _runnerInstance.ProvideInput = true;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = roomName,
                ObjectProvider = _runnerInstance.GetComponent<NetworkObjectPoolDefault>(),
            };

            var result = await _runnerInstance.StartGame(startGameArgs);
                
            if (!result.Ok)
            {
                waiting = false;
                return;
            }
            else
            {
                _EdgegapConnectStatus.text = "Game starting...";
                EdgegapManager.TransferingToEdgegapServer = false;
            }

            if (_runnerInstance.IsServer)
            {
                await _runnerInstance.LoadScene(sceneName);
            }
        }

        private void ValidateRoomName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                _EdgegapStartBtn.interactable = false;
                _EdgegapConnectStatus.text = "Please enter a room name to test with Edgegap.";
            }
            else
            {
                _EdgegapStartBtn.interactable = true;
                _EdgegapConnectStatus.text = "";

                CheckForSpecialcharacters(value, _roomName);
            }
        }

        private void CheckForSpecialcharacters(string value, TMP_InputField textfield)
        {
            string newValue = Regex.Replace(value, @"[^0-9a-zA-Z]", string.Empty);
            if (value != newValue)
            {
                Debug.Log("Please do not use special characters in room name or player name.");
                textfield.text = newValue;
            }
        }
    }
}