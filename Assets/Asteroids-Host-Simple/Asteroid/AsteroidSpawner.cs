using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace Asteroids.HostSimple
{
    // The AsteroidSpawner does not execute any behaviour on the clients.
    // Therefore all of its parameters can remained local and not networked.
    public class AsteroidSpawner : NetworkBehaviour
    {
        // The Network Object prefabs for small and big asteroids.
        // Using NetworkPrefabRef restricts the parameters to Prefabs which carry a NetworkObject component. 
        [SerializeField] private NetworkPrefabRef _smallAsteroid = NetworkPrefabRef.Empty;
        [SerializeField] private NetworkPrefabRef _bigAsteroid = NetworkPrefabRef.Empty;

        // The minimum and maximum amount of time between each big asteroid spawn.
        [SerializeField] private float _minSpawnDelay = 5.0f;
        [SerializeField] private float _maxSpawnDelay = 10.0f;

        // The minimum and maximum amount of small asteroids a big asteroids spawns when it gets destroyed.
        [SerializeField] private int _minAsteroidSplinters = 3;
        [SerializeField] private int _maxAsteroidSplinters = 6;

        // The TickTimer controls the time lapse between spawns.
        private TickTimer _spawnDelay;

        // The spawn boundaries are based of the camera settings
        private float _screenBoundaryX = 0.0f;
        private float _screenBoundaryY = 0.0f;

        private List<NetworkId> _asteroids = new List<NetworkId>();

        // The spawner is started when the GameStateController switches to GameState.Running.
        public void StartAsteroidSpawner()
        {
            if (Object.HasStateAuthority == false) return;

            // Triggers the delay until the first spawn.
            SetSpawnDelay();

            // The spawn boundaries are based of the camera settings
            _screenBoundaryX = Camera.main.orthographicSize * Camera.main.aspect;
            _screenBoundaryY = Camera.main.orthographicSize;
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority == false) return;

            SpawnAsteroid();

            CheckOutOfBoundsAsteroids();
        }

        // Spawns an big asteroid whenever the _spawnDelay expires
        private void SpawnAsteroid()
        {
            if (_spawnDelay.Expired(Runner) == false) return;

            Vector2 direction = Random.insideUnitCircle;
            Vector3 position = Vector3.zero;

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                // Make it appear on the left/right side
                position = new Vector3(Mathf.Sign(direction.x) * _screenBoundaryX, 0, direction.y * _screenBoundaryY);
            }
            else
            {
                // Make it appear on the top/bottom
                position = new Vector3(direction.x * _screenBoundaryX, 0, Mathf.Sign(direction.y) * _screenBoundaryY);
            }

            // Offset slightly so we are not out of screen at creation time (as it would destroy the asteroid right away)
            position -= position.normalized * 0.1f;

            var rotation = Quaternion.Euler(Random.Range(0.0f, 360.0f), Random.Range(0.0f, 360.0f),
                Random.Range(0.0f, 360.0f));

            var asteroid = Runner.Spawn(_bigAsteroid, position, rotation, PlayerRef.None,
                onBeforeSpawned: SpinBigAsteroid);

            _asteroids.Add(asteroid.Id);

            // Sets the delay until the next spawn.
            SetSpawnDelay();
        }

        private void SetSpawnDelay()
        {
            // Chose a random amount of time until the next spawn.
            var time = Random.Range(_minSpawnDelay, _maxSpawnDelay);
            _spawnDelay = TickTimer.CreateFromSeconds(Runner, time);
        }

        // Checks whether any asteroid left the game boundaries. If it has, the asteroid gets despawned.
        private void CheckOutOfBoundsAsteroids()
        {
            for (int i = 0; i < _asteroids.Count; i++)
            {
                // Checks if an asteroid is still exists.
                if (Runner.TryFindObject(_asteroids[i], out var asteroid) == false)
                {
                    _asteroids.RemoveAt(i);
                    i--;
                    continue;
                }

                if (IsWithinScreenBoundary(asteroid.transform.position)) continue;

                Runner.Despawn(asteroid);
                i--;
            }
        }

        // Checks whether a position is inside the screen boundaries
        private bool IsWithinScreenBoundary(Vector3 asteroidPosition)
        {
            return Mathf.Abs(asteroidPosition.x) < _screenBoundaryX && Mathf.Abs(asteroidPosition.z) < _screenBoundaryY;
        }

        // Adds a random spin to big asteroids
        private void SpinBigAsteroid(NetworkRunner runner, NetworkObject asteroidNetworkObject)
        {
            Vector3 force = -asteroidNetworkObject.transform.position.normalized * 1000.0f;
            Vector3 torque = Random.insideUnitSphere * Random.Range(500.0f, 1500.0f);

            var rb = asteroidNetworkObject.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.AddForce(force);
            rb.AddTorque(torque);

            var asteroidBehaviour = asteroidNetworkObject.GetComponent<AsteroidBehaviour>();
            asteroidBehaviour.IsBig = true;
        }

        // Adds a random spin to small asteroids
        private void SpinSmallAsteroid(NetworkRunner runner, NetworkObject asteroidNetworkObject, Vector3 force,
            Vector3 torque)
        {
            var rb = asteroidNetworkObject.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.AddForce(force);
            rb.AddTorque(torque);

            var asteroidBehaviour = asteroidNetworkObject.GetComponent<AsteroidBehaviour>();
            asteroidBehaviour.IsBig = false;
        }

        // Spawns a random amount of small asteroids when a big asteroids is destroyed.
        public void BreakUpBigAsteroid(Vector3 position)
        {
            int splintersToSpawn = Random.Range(_minAsteroidSplinters, _maxAsteroidSplinters);

            for (int counter = 0; counter < splintersToSpawn; ++counter)
            {
                Vector3 force = Quaternion.Euler(0, counter * 360.0f / splintersToSpawn, 0) * Vector3.forward *
                                Random.Range(0.5f, 1.5f) * 300.0f;
                Vector3 torque = Random.insideUnitSphere * Random.Range(500.0f, 1500.0f);
                Quaternion rotation = Quaternion.Euler(0, Random.value * 180.0f, 0);

                var asteroid = Runner.Spawn(_smallAsteroid, position + force.normalized * 10.0f, rotation,
                    PlayerRef.None,
                    (networkRunner, asteroidNetworkObject) =>
                        SpinSmallAsteroid(networkRunner, asteroidNetworkObject, force, torque));

                _asteroids.Add(asteroid.Id);
            }
        }
    }
}
