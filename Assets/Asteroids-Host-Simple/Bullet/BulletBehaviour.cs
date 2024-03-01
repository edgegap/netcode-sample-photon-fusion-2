using Fusion;
using UnityEngine;

namespace Asteroids.HostSimple
{
    // Defines how the bullet behaves
    public class BulletBehaviour : NetworkBehaviour
    {
        // The settings
        [SerializeField] private float _maxLifetime = 3.0f;
        [SerializeField] private float _speed = 200.0f;
        [SerializeField] private LayerMask _asteroidLayer;

        // The countdown for a bullet lifetime.
        [Networked] private TickTimer _currentLifetime { get; set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority == false) return;

            // The network parameters get initializes by the host. These will be propagated to the clients since the
            // variables are [Networked]
            _currentLifetime = TickTimer.CreateFromSeconds(Runner, _maxLifetime);
        }

        public override void FixedUpdateNetwork()
        {
            // If the bullet has not hit an asteroid, moves forward.
            if (HasHitAsteroid() == false)
            {
                transform.Translate(transform.forward * _speed * Runner.DeltaTime, Space.World);
            }
            else
            {
                Runner.Despawn(Object);
                return;
            }

            CheckLifetime();
        }

        // If the bullet has exceeded its lifetime, it gets destroyed
        private void CheckLifetime()
        {
            if (_currentLifetime.Expired(Runner) == false) return;

            Runner.Despawn(Object);
        }

        // Check if the bullet will hit an asteroid in the next tick.
        private bool HasHitAsteroid()
        {
            var hitAsteroid = Runner.LagCompensation.Raycast(transform.position, transform.forward, _speed * Runner.DeltaTime,
                Object.InputAuthority, out var hit, _asteroidLayer);

            if (hitAsteroid == false) return false;

            var asteroidBehaviour = hit.GameObject.GetComponent<AsteroidBehaviour>();

            if (asteroidBehaviour.IsAlive == false)
                return false;

            asteroidBehaviour.HitAsteroid(Object.InputAuthority);

            return true;
        }
    }
}
