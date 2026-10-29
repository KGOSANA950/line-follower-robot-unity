using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Tank tracks (skid steering): a motor drives each track. It is commanded exactly like a differential
    /// drive — <c>SetPower(left, right)</c> — but to turn, the tracks must slide sideways over the floor.
    /// That makes turning slower and wheel-encoder odometry less accurate than on a wheeled robot
    /// (a gyroscope helps!). Programs written for differential drive also run on tracked robots.
    /// </summary>
    [AddComponentMenu("Robot Sim/Tracked Drive")]
    public class TrackedDrive : DifferentialDrive
    {
        public override string DriveType => "Tracked (Skid Steer)";
    }
}
