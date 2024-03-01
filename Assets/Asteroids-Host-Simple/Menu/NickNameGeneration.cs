using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Asteroids.HostSimple
{
    // Used in the Start Menu scene to general a random placeholder player name
    public class NickNameGeneration : MonoBehaviour
    {
        private void Awake()
        {
            var nickNameInputField = GetComponentInChildren<TextMeshProUGUI>();
            nickNameInputField.text = PlayerData.GetRandomNickName();
        }
    }
}