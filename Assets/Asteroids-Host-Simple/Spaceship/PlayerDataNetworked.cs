using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace Asteroids.HostSimple
{
    // Holds the player's information and ensures it is replicated to all clients.
    public class PlayerDataNetworked : NetworkBehaviour
    {
        // Global static setting
        private const int STARTING_LIVES = 3;

        // Local Runtime references
        private PlayerOverviewPanel _overviewPanel = null;

        private ChangeDetector _changeDetector;

        // Game Session SPECIFIC Settings are used in the UI.
        // The method passed to the OnChanged attribute is called everytime the [Networked] parameter is changed.
        [HideInInspector]
        [Networked]
        public NetworkString<_16> NickName { get; private set; }

        [HideInInspector]
        [Networked]
        public int Lives { get; private set; }

        [HideInInspector]
        [Networked]
        public int Score { get; private set; }

        public override void Spawned()
        {
            // --- Client
            // Find the local non-networked PlayerData to read the data and communicate it to the Host via a single RPC 
            if (Object.HasInputAuthority)
            {
                var nickName = FindObjectOfType<PlayerData>().GetNickName();
                RpcSetNickName(nickName);
            }

            // --- Host
            // Initialized game specific settings
            if (Object.HasStateAuthority)
            {
                Lives = STARTING_LIVES;
                Score = 0;
            }

            // --- Host & Client
            // Set the local runtime references.
            _overviewPanel = FindObjectOfType<PlayerOverviewPanel>();
            // Add an entry to the local Overview panel with the information of this spaceship
            _overviewPanel.AddEntry(Object.InputAuthority, this);
            
            // Refresh panel visuals in Spawned to set to initial values.
            _overviewPanel.UpdateNickName(Object.InputAuthority, NickName.ToString());
            _overviewPanel.UpdateLives(Object.InputAuthority, Lives);
            _overviewPanel.UpdateScore(Object.InputAuthority, Score);
            
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        }
        
        public override void Render()
        {
            foreach (var change in _changeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
            {
                switch (change)
                {
                    case nameof(NickName):
                        _overviewPanel.UpdateNickName(Object.InputAuthority, NickName.ToString());
                        break;
                    case nameof(Score):
                        _overviewPanel.UpdateScore(Object.InputAuthority, Score);
                        break;
                    case nameof(Lives):
                        _overviewPanel.UpdateLives(Object.InputAuthority, Lives);
                        break;
                }
            }
        }

        // Remove the entry in the local Overview panel for this spaceship
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _overviewPanel.RemoveEntry(Object.InputAuthority);
        }

        // Increase the score by X amount of points
        public void AddToScore(int points)
        {
            Score += points;
        }

        // Decrease the current Lives by 1
        public void SubtractLife()
        {
            Lives--;
        }

        // RPC used to send player information to the Host
        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        private void RpcSetNickName(string nickName)
        {
            if (string.IsNullOrEmpty(nickName)) return;
            NickName = nickName;
        }
    }
}