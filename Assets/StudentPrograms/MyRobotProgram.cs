using RobotSim;
using UnityEngine;

// ============================================================================
//  MY ROBOT PROGRAM — start here!
//
//  1. Select the robot in the Hierarchy.
//  2. Untick "Keyboard Teleop" in the Inspector, then Add Component > My Robot Program.
//  3. Press Play. Press R to restart after changing the code.
//
//  See Assets/RobotSim/Docs/StudentGuide.md for every sensor and command.
//
//  This program is for the differential-drive robot. For the other robots,
//  inherit from SingleWheelDriveProgram, SynchroDriveProgram,
//  AckermannDriveProgram or TrackedDriveProgram instead.
// ============================================================================
public class MyRobotProgram : DifferentialDriveProgram
{
    UltrasonicSensor front;

    // Runs once when the program starts. Find your sensors and subscribe to events here.
    protected override void OnStart()
    {
        front = GetSensor<UltrasonicSensor>("Front");

        // An event: this code runs every time the colour under the robot changes.
        GetSensor<ColourSensor>("Colour").OnColourChanged += colour =>
        {
            Log("I am on " + colour);
            if (colour == NamedColour.Green)
                Log("Goal!");
        };

        Log("Hello from MyRobotProgram!");
    }

    // Runs 50 times per second. Read sensors and set the motors here.
    protected override void OnTick(float dt)
    {
        if (front.Distance < 0.2f)
        {
            // Wall ahead: spin left on the spot.
            Drive.SetPower(-0.3f, 0.3f);
        }
        else
        {
            // Clear: drive forward.
            Drive.SetPower(0.4f, 0.4f);
        }

        Plot("Front distance", front.Distance);
    }

    // Runs when the robot bumps into something.
    protected override void OnCollision(Collision collision)
    {
        Log("Ouch!");
    }
}
