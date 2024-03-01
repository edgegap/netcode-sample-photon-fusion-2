using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

namespace Asteroids.HostSimple
{
    // The AsteroidBehaviour holds in the information about the asteroid
    public class AsteroidBehaviour : NetworkBehaviour
    {
        // The _points variable can be a local private variable as it will only be used to add points to the score
        // The score itself is networked and any increase or decrease will be propagated automatically.
        [SerializeField] private int _points = 1;

        // The IsBig variable is Networked as it can be used to evaluate and derive visual information for an asteroid locally.
        [HideInInspector] [Networked] public NetworkBool IsBig { get; set; }

        // Used to delay the despawn after the hit and play the destruction animation.
        [Networked] private NetworkBool _wasHit { get; set; }

        [Networked] private TickTimer _despawnTimer { get; set; }

        private NetworkRigidbody3D _networkRigidbody;

        public bool IsAlive => !_wasHit;

        public override void Spawned()
        {
            _networkRigidbody = GetComponent<NetworkRigidbody3D>();
            _networkRigidbody.InterpolationTarget.localScale = Vector3.one;
        }

        // When the asteroid gets hit by another object, this method is called to decide what to do next.
        public void HitAsteroid(PlayerRef player)
        {
            // The asteroid hit only triggers behaviour on the host and if the asteroid had not yet been hit.
            if (Object == null) return;
            if (Object.HasStateAuthority == false) return;
            if (_wasHit) return;

            // If this hit was triggered by a projectile, the player who shot it gets points
            // The player object is retrieved via the Runner.
            if (Runner.TryGetPlayerObject(player, out var playerNetworkObject))
            {
                playerNetworkObject.GetComponent<PlayerDataNetworked>().AddToScore(_points);
            }

            _wasHit = true;
            _despawnTimer = TickTimer.CreateFromSeconds(Runner, .2f);
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority && _wasHit && _despawnTimer.Expired(Runner))
            {
                _wasHit = false;
                _despawnTimer = TickTimer.None;

                // Big asteroids tell the AsteroidSpawner to spawn multiple small asteroids as it breaks up.
                if (IsBig)
                {
                    FindObjectOfType<AsteroidSpawner>().BreakUpBigAsteroid(transform.position);
                }

                Runner.Despawn(Object);
            }
        }

        public override void Render()
        {
            if (_wasHit && _despawnTimer.IsRunning)
            {
                _networkRigidbody.InterpolationTarget.localScale *= .95f;
            }
        }
    }
}
