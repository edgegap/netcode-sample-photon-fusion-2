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

        [SerializeField]
        private string _EdgegapPortMapName = "GAMEPORT";

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

            if (!result.Ok && EdgegapManager.EdgegapPreServerMode)
            {
                startDeploy = true;
                /*
                 A typical issue that Fusion users have is a timeout when their STUN port discovery canʼt find out the
                 external port within a pre-specified period of time (hardcoded in Photon). Our sample should include an
                 automated client-side retry to resolve this
                */
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
            //TODO edit
            //start w just inputing roomname, then switch to vvv
            //set playerdata (no need for ip), matchmake, then try StartGame(GameMode.Client, room name from ticket fqdn, _gameSceneName)

            EdgegapManager.EdgegapPreServerMode = true;
            SetPlayerData();
            tryJoinEdgegap = true;
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

        //TODO remove
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