using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;


namespace Asteroids.HostSimple
{
    enum SpaceshipButtons
    {
        Fire = 0,
    }

    public struct SpaceshipInput : INetworkInput
    {
        public float HorizontalInput;
        public float VerticalInput;
        public NetworkButtons Buttons;
    }
}