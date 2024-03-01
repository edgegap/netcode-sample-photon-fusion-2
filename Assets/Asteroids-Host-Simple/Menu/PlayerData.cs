using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Asteroids.HostSimple
{
    // This class functions as an Instance Singleton (no-static references)
    // and holds information about the local player in-between scene loads.
    public class PlayerData : MonoBehaviour
    {
        private string _nickName = null;
        private string ipAddress = null;

        private void Start()
        {
            var count = FindObjectsOfType<PlayerData>().Length;
            if (count > 1)
            {
                Destroy(gameObject);
                return;
            }

            ;

            DontDestroyOnLoad(gameObject);
        }

        public void SetNickName(string nickName)
        {
            _nickName = nickName;
        }

        public string GetNickName()
        {
            if (string.IsNullOrWhiteSpace(_nickName))
            {
                _nickName = GetRandomNickName();
            }

            return _nickName;
        }

        public static string GetRandomNickName()
        {
            var rngPlayerNumber = Random.Range(0, 9999);
            return $"Player {rngPlayerNumber.ToString("0000")}";
        }

        public void SetIpAddress(string ip)
        {
            ipAddress = ip;
        }

        public string GetIpAddress()
        {
            return ipAddress;
        }
    }
}