using System.Collections;
using UnityEngine;
using Fusion;
using TMPro;
using System;
using UnityEngine.UI;

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
        [SerializeField] private Button _HostStartBtn = null;
        [SerializeField] private Button _ClientStartBtn = null;

        [SerializeField] private bool _retryJoin = true;
        [SerializeField] private float _retryAfterSecs = 1.5f;
        private bool _retry;

        private NetworkRunner _runnerInstance = null;

        private void Start()
        {
            UpdateConnectStatusTxt("");
            _EdgegapStartBtn.interactable = true;
            _HostStartBtn.interactable = true;
            _ClientStartBtn.interactable = true;
            _retry = _retryJoin;
        }

        // Attempts to start a new game session 
        public void StartHost()
        {
            EdgegapServerManager.EdgegapEnabled = false;
            _EdgegapStartBtn.interactable = false;
            _HostStartBtn.interactable = false;
            _ClientStartBtn.interactable = false;
            SetPlayerData();
            UpdateConnectStatusTxt($"Attempting to connect to room {_roomName.text} as Host...");
            StartGame(GameMode.AutoHostOrClient, _roomName.text, _gameSceneName);
        }

        public void StartClient()
        {
            EdgegapServerManager.EdgegapEnabled = false;
            _EdgegapStartBtn.interactable = false;
            _HostStartBtn.interactable = false;
            _ClientStartBtn.interactable = false;
            SetPlayerData();
            UpdateConnectStatusTxt($"Attempting to connect to room {_roomName.text} as Client...");
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

        private async void StartGame(GameMode mode, string roomName, string sceneName)
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

            // GameMode.Host = Start a session with a specific name
            // GameMode.Client = Join a session with a specific name
            var result = await _runnerInstance.StartGame(startGameArgs);

            if (!result.Ok)
            {
                if (_retry)
                {
                    _retry = false;
                    Destroy(_runnerInstance);
                    var retryAfterDelay = RunAfterTime(_retryAfterSecs, () => StartGame(mode, roomName, sceneName));
                    StartCoroutine(retryAfterDelay);
                }
                else
                {
                    UpdateConnectStatusTxt($"Unable to join room {roomName} due to {result.ShutdownReason}, see logs.");
                    Debug.LogError($"{result.ErrorMessage}");
                    _EdgegapStartBtn.interactable = true;
                    _HostStartBtn.interactable = true;
                    _ClientStartBtn.interactable = true;
                    _retry = _retryJoin;
                }
            }
            else
            {
                UpdateConnectStatusTxt("Starting game...");

                if (_runnerInstance.IsServer)
                {
                    await _runnerInstance.LoadScene(sceneName);
                }
            }
        }

        public void StartEdgegap()
        {
            EdgegapServerManager.EdgegapEnabled = true;
            _EdgegapStartBtn.interactable = false;
            _HostStartBtn.interactable = false;
            _ClientStartBtn.interactable = false;
            SetPlayerData();
            UpdateConnectStatusTxt($"Attempting to connect to room {_roomName.text} via Edgegap...");
            StartGame(GameMode.Client, _roomName.text, _gameSceneName);
        }

        private void UpdateConnectStatusTxt(string msg)
        {
            _EdgegapConnectStatus.text = msg;
        }

        private IEnumerator RunAfterTime(float timeInSeconds, Action action)
        {
            yield return new WaitForSeconds(timeInSeconds);
            action();
        }
    }
}
