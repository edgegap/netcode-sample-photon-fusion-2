using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Asteroids.HostSimple
{
    // Class controlling the visual representation of the spaceship (turning the 3D model on / off)
    // and visual feedback for the player (engine & destruction VFX)
    public class SpaceshipVisualController : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _spaceshipModel = null;
        [SerializeField] private ParticleSystem _destructionVFX = null;
        [SerializeField] private ParticleSystem _engineTrailVFX = null;

        // Colors the ship in the color assigned to the PlayerRef's index
        public void SetColorFromPlayerID(int playerID)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                r.material.color = GetColor(playerID);
            }
        }

        public void TriggerSpawn()
        {
            _spaceshipModel.enabled = true;
            _engineTrailVFX.Play();
            _destructionVFX.Stop(); 
        }

        public void TriggerDestruction()
        {
            _spaceshipModel.enabled = false;
            _engineTrailVFX.Stop();
            _destructionVFX.Play();
        }

        // Defines a set of colors to distinguish between players
        // N.B.: The Asteroid's NetworkProjectConfig is set to only supports up to 4 players by default!
        public static Color GetColor(int player)
        {
            switch (player%8)
            {
                case 0: return Color.red;
                case 1: return Color.green;
                case 2: return Color.blue;
                case 3: return Color.yellow;
                case 4: return Color.cyan;
                case 5: return Color.grey;
                case 6: return Color.magenta;
                case 7: return Color.white;
            }
            return Color.black;
        }
    }
}