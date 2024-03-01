using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace Asteroids.HostSimple
{
    public class GameStateController : NetworkBehaviour
    {
        enum GameState
        {
            Starting,
            Running,
            Ending
        }

        [SerializeField] private float _startDelay = 4.0f;
        [SerializeField] private float _endDelay = 4.0f;
        [SerializeField] private float _gameSessionLength = 180.0f;

        [SerializeField] private TextMeshProUGUI _startEndDisplay = null;
        [SerializeField] private TextMeshProUGUI _ingameTimerDisplay = null;

        [Networked] private TickTimer _timer { get; set; }
        [Networked] private GameState _gameState { get; set; }

        [Networked] private NetworkBehaviourId _winner { get; set; }

        private List<NetworkBehaviourId> _playerDataNetworkedIds = new List<NetworkBehaviourId>();

        public override void Spawned()
        {
            // --- This section is for all information which has to be locally initialized based on the networked game state
            // --- when a CLIENT joins a game

            _startEndDisplay.gameObject.SetActive(true);
            _ingameTimerDisplay.gameObject.SetActive(false);

            // If the game has already started, find all currently active players' PlayerDataNetworked component Ids
            if (_gameState != GameState.Starting)
            {
                foreach (var player in Runner.ActivePlayers)
                {
                    if (Runner.TryGetPlayerObject(player, out var playerObject) == false) continue;
                    TrackNewPlayer(playerObject.GetComponent<PlayerDataNetworked>().Id);
                }
            }

            // Set is Simulated so that FixedUpdateNetwork runs on every client instead of just the Host
            Runner.SetIsSimulated(Object, true);

            // --- This section is for all networked information that has to be initialized by the HOST
            if (Object.HasStateAuthority == false) return;

            // Initialize the game state on the host
            _gameState = GameState.Starting;
            _timer = TickTimer.CreateFromSeconds(Runner, _startDelay);
        }

        public override void FixedUpdateNetwork()
        {
            // Update the game display with the information relevant to the current game state
            switch (_gameState)
            {
                case GameState.Starting:
                    UpdateStartingDisplay();
                    break;
                case GameState.Running:
                    UpdateRunningDisplay();
                    // Ends the game if the game session length has been exceeded
                    if (_timer.ExpiredOrNotRunning(Runner))
                    {
                        GameHasEnded();
                    }

                    break;
                case GameState.Ending:
                    UpdateEndingDisplay();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateStartingDisplay()
        {
            // --- Host & Client
            // Display the remaining time until the game starts in seconds (rounded down to the closest full second)

            _startEndDisplay.text = $"Game Starts In {Mathf.RoundToInt(_timer.RemainingTime(Runner) ?? 0)}";

            // --- Host
            if (Object.HasStateAuthority == false) return;
            if (_timer.ExpiredOrNotRunning(Runner) == false) return;

            // Starts the Spaceship and Asteroids spawners once the game start delay has expired
            FindObjectOfType<SpaceshipSpawner>().StartSpaceshipSpawner(this);
            FindObjectOfType<AsteroidSpawner>().StartAsteroidSpawner();

            // Switches to the Running GameState and sets the time to the length of a game session
            _gameState = GameState.Running;
            _timer = TickTimer.CreateFromSeconds(Runner, _gameSessionLength);
        }

        private void UpdateRunningDisplay()
        {
            // --- Host & Client
            // Display the remaining time until the game ends in seconds (rounded down to the closest full second)
            _startEndDisplay.gameObject.SetActive(false);
            _ingameTimerDisplay.gameObject.SetActive(true);
            _ingameTimerDisplay.text =
                $"{Mathf.RoundToInt(_timer.RemainingTime(Runner) ?? 0).ToString("000")} seconds left";
        }

        private void UpdateEndingDisplay()
        {
            // --- Host & Client
            // Display the results and
            // the remaining time until the current game session is shutdown

            if (Runner.TryFindBehaviour(_winner, out PlayerDataNetworked playerData) == false) return;

            _startEndDisplay.gameObject.SetActive(true);
            _ingameTimerDisplay.gameObject.SetActive(false);
            _startEndDisplay.text =
                $"{playerData.NickName} won with {playerData.Score} points. Disconnecting in {Mathf.RoundToInt(_timer.RemainingTime(Runner) ?? 0)}";
            _startEndDisplay.color = SpaceshipVisualController.GetColor(playerData.Object.InputAuthority.PlayerId);

            // --- Host
            // Shutdowns the current game session.
            // The disconnection behaviour is found in the OnServerDisconnect.cs script
            if (_timer.ExpiredOrNotRunning(Runner) == false) return;

            Runner.Shutdown();
        }

        // Called from the ShipController when it hits an asteroid
        public void CheckIfGameHasEnded()
        {
            if (Object.HasStateAuthority == false) return;

            int playersAlive = 0;

            for (int i = 0; i < _playerDataNetworkedIds.Count; i++)
            {
                if (Runner.TryFindBehaviour(_playerDataNetworkedIds[i],
                        out PlayerDataNetworked playerDataNetworkedComponent) == false)
                {
                    _playerDataNetworkedIds.RemoveAt(i);
                    i--;
                    continue;
                }

                if (playerDataNetworkedComponent.Lives > 0) playersAlive++;
            }

            // If more than 1 player is left alive, the game continues.
            // If only 1 player is left, the game ends immediately.
            if (playersAlive > 1 || (Runner.ActivePlayers.Count() == 1 && playersAlive == 1)) return;

            foreach (var playerDataNetworkedId in _playerDataNetworkedIds)
            {
                if (Runner.TryFindBehaviour(playerDataNetworkedId,
                        out PlayerDataNetworked playerDataNetworkedComponent) ==
                    false) continue;

                if (playerDataNetworkedComponent.Lives > 0 == false) continue;

                _winner = playerDataNetworkedId;
            }

            if (_winner == default) // when playing alone in host mode this marks the own player as winner
            {
                _winner = _playerDataNetworkedIds[0];
            }

            GameHasEnded();
        }

        private void GameHasEnded()
        {
            _timer = TickTimer.CreateFromSeconds(Runner, _endDelay);
            _gameState = GameState.Ending;
        }

        public void TrackNewPlayer(NetworkBehaviourId playerDataNetworkedId)
        {
            _playerDataNetworkedIds.Add(playerDataNetworkedId);
        }
    }
}
