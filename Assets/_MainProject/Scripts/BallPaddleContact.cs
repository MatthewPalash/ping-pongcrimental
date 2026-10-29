using System;
using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class BallPaddleContact : MonoBehaviour, IPaddleHitReceiver
    {
        public event Action<PaddleHitDetector, Vector3> HitByPaddle;
        public int HitCount { get; private set; }

        public void OnPaddleHit(PaddleHitDetector paddle, Vector3 contactPoint)
        {
            HitCount++;
            HitByPaddle?.Invoke(paddle, contactPoint);
        }
    }
}
