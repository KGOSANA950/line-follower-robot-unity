# Robot Simulator — Student Guide

You program the robot in C#. The simulator handles the physics, motors and sensors. Your job is to decide what the motors should do based on what the sensors report.

## Quick start

1. Open `Assets/Scenes/MazeArena.unity`.
2. Open `Assets/StudentPrograms/MyRobotProgram.cs` and read it — it's a complete working example.
3. Select the robot (**DifferentialRobot**) in the Hierarchy. In the Inspector:
   - untick **Keyboard Teleop**
   - choose **Add Component → My Robot Program**
4. Press **Play**.
5. Edit your code, then press Play again (or press **R** while playing to restart your program).

Only **one** program should be ticked on the robot at a time.

To make a new program, create a new C# file in `Assets/StudentPrograms/` and copy the shape of `MyRobotProgram`.

## Robot types

There are five robots. Each one is a different kind of wheeled drive.

To try them while playing, press the number keys **1–5**. The robot is replaced with a fresh one at the start, driven by Keyboard Teleop.

To use one with your own program, use **Robot Sim → Create Robot → …** in the menu, delete the old robot, and add your program to the new one.

| Key | Robot | How it moves | Your program inherits from | Driving commands |
|---|---|---|---|---|
| 1 | **Differential** (orange) | Two driven wheels and a caster. Spins on the spot. | `DifferentialDriveProgram` | `Drive.SetPower(left, right)` |
| 2 | **Single Wheel** (teal tricycle) | The front wheel is steered *and* driven; the rear wheels just roll. At ±90° steering it pivots on the spot around the middle of the rear axle. | `SingleWheelDriveProgram` | `Drive.SetPower(p)`, `Drive.SetSteering(angle)` |
| 3 | **Synchro** (purple, round) | Three wheels, all driven by one motor and all steered together. It can drive in **any direction**, but its body never turns. | `SynchroDriveProgram` | `Drive.SetPower(p)`, `Drive.SetSteering(direction)` |
| 4 | **Ackermann** (red car) | Rear wheels driven, front wheels steered like a car. It can't turn on the spot; the tightest turn has about a 0.15 m radius. | `AckermannDriveProgram` | `Drive.SetPower(p)`, `Drive.SetSteering(angle)` |
| 5 | **Tracked** (green tank) | Two tank tracks. It skids sideways to turn. | `TrackedDriveProgram` (or `DifferentialDriveProgram`) | `Drive.SetPower(left, right)` |

`SteeredDriveProgram` works on any of the three steered robots (single wheel, synchro and Ackermann). It offers `Drive.SetPower`, `Drive.SetSteering`, `Drive.SteerAngle` and `Drive.MaxSteerAngle`. Programs written for differential drive also run on the tracked robot.

If you put a program on the wrong kind of robot, you get a clear error message telling you which drive it needs.

> **Angles.** Every angle in the simulator is in **degrees** and **positive = anticlockwise / to the left**, like in maths. This applies to steering, the gyro, the lidar and odometry. So `SetSteering(20)` steers left and `SetSteering(-20)` steers right. For synchro drive, `SetSteering(0)` means forward, `90` left, `-90` right and `180` backwards.

## Program structure

```csharp
using RobotSim;
using UnityEngine;

public class MyMazeSolver : DifferentialDriveProgram
{
    UltrasonicSensor front;

    protected override void OnStart()            // once, at the start
    {
        front = GetSensor<UltrasonicSensor>("Front");
        front.OnReading += distance => { if (distance < 0.1f) Log("Close!"); };   // an event
    }

    protected override void OnTick(float dt)     // 50 times per second
    {
        if (front.Distance < 0.15f) Drive.SetPower(-0.3f, 0.3f);
        else                        Drive.SetPower(0.5f, 0.5f);
    }

    protected override void OnCollision(Collision c) { Log("Bump!"); }   // when you hit something
    protected override void OnStop() { }                                 // when stopped / reset
}
```

The same idea on a car:

```csharp
public class MyCar : AckermannDriveProgram
{
    protected override void OnTick(float dt)
    {
        Drive.SetSteering(20f);   // steer 20° left
        Drive.SetPower(0.4f);     // drive forward: the car goes round in a circle
    }
}
```

| Callback | When it runs |
|---|---|
| `OnStart()` | Once, when the program starts (and again after pressing **R**). Find sensors and subscribe to events here. |
| `OnTick(float dt)` | Repeatedly, at `ControlRateHz` times per second (default 50). `dt` is the time between ticks in seconds. |
| `OnCollision(Collision c)` | Each time the robot bumps into something. |
| `OnStop()` | When the program is stopped or the robot is reset. |

**Don't** use Unity's own `Update`, `FixedUpdate` or `Start` methods in robot programs. Use the callbacks above.

**Events vs. polling.** You can read any sensor property at any time inside `OnTick` (this is *polling*). Or you can subscribe to an event, and your code then runs automatically every time the sensor updates or something changes. Both styles are fine, and you can mix them.

## Motors

| Code | Meaning |
|---|---|
| `Drive.SetPower(left, right)` | Differential and tracked: set both motors at once. Each value runs from -1 (full reverse) to 1 (full forward). |
| `Drive.SetPower(p)` | Steered robots: set the single drive motor. |
| `Motors["Left"].Power = 0.5f;` | Set one motor by name. The differential and tracked robots have `"Left"` and `"Right"`; the steered robots have `"Drive"`. |
| `Drive.Left`, `Drive.Right` / `Drive.DriveMotor` | The motor objects. |
| `Stop()` | Set every motor's power to 0. |

**Reading motors back** (all are read-only):

| Property | Meaning |
|---|---|
| `Power` | The power you last set. |
| `WheelSpeed` | Wheel speed in rad/s. |
| `Rpm` | Wheel speed in revolutions per minute. |
| `EncoderTicks` | Encoder count (360 per wheel revolution). |
| `Angle` | Total wheel rotation in radians. |
| `Distance` | How far the wheel has rolled, in metres. |
| `Torque` | Torque the motor is producing, in N·m. |

`ResetEncoder()` zeroes `EncoderTicks`, `Angle` and `Distance`.

**Odometry.** For differential drive, `Drive.WheelRadius` (0.035 m) and `Drive.TrackWidth` (0.17 m, the distance between the wheels) let you work out the heading:

```csharp
float heading = (Drive.Right.Distance - Drive.Left.Distance) / Drive.TrackWidth; // radians, + = turned left
```

This is accurate on the wheeled robot. On the tracked robot it isn't, because the tracks skid when turning. Use the gyro there.

**How the motors behave.** They act like real geared DC motors:
- They push hardest from standstill.
- Setting power to 0 **brakes** them.
- Small powers (below about 0.1) may not move the robot at all, because of friction.

## Steering servos

The single-wheel, synchro and Ackermann robots have a steering servo called `"Steer"`. You can use it directly with `Servos["Steer"]`, though `Drive.SetSteering(angle)` does the same thing.

| Property | Meaning |
|---|---|
| `TargetAngle` | Where you've asked the servo to go, in degrees. Requests outside the range are clamped. |
| `Angle` | Where the servo actually is right now. |
| `AtTarget` | True once it has arrived. |
| `MinAngle`, `MaxAngle` | Its range. |
| `MaxSpeed` | How fast it turns, in degrees per second. |

Servos don't move instantly — they turn at `MaxSpeed`, like a real servo. Check `Angle` or `AtTarget` if you need to wait.

| Robot | Steering range | Speed |
|---|---|---|
| Single wheel | ±85° | 240°/s |
| Ackermann | ±40° (this is the angle of an imaginary wheel in the middle of the front axle) | 200°/s |
| Synchro | All the way round; it always takes the short way | 180°/s |

**Ackermann extras.** The steering linkage turns the inner front wheel more sharply than the outer one, as on a real car. You can see this in `Drive.LeftWheelAngle` and `Drive.RightWheelAngle`. `Drive.TurningRadius` gives the current turning radius in metres.

**Synchro extras.** `Drive.SteeringReady` is true when the wheels have finished turning. A good habit is to wait for it before driving off.

## Sensors

Every sensor has a **name**: the name of its GameObject under the robot. Every robot has this kit:

| Name | Type | Where |
|---|---|---|
| `Front` | `UltrasonicSensor` | front, facing forward |
| `Left` | `UltrasonicSensor` | left side, facing left |
| `Right` | `UltrasonicSensor` | right side, facing right |
| `Back` | `UltrasonicSensor` | **synchro only**, facing backwards |
| `LineArray` | `LineSensorArray` | under the front, 5 sensors |
| `Colour` | `ColourSensor` | underneath, facing down |
| `Metal` | `MetalDetector` | coil near the floor |
| `Lidar` | `Lidar` | on top |
| `Gyro` | `GyroSensor` | on the deck |

Get a sensor by name, `GetSensor<UltrasonicSensor>("Front")`, or by type when there's only one, `GetSensor<GyroSensor>()`. If you misspell a name, the error message lists the names that exist.

**Settings every sensor has:**
- `UpdateRateHz`: samples per second.
- `NoiseStdDev`: random noise added to readings. It's 0 by default; turn it up for a challenge.
- `ShowDebug`: draws the sensor's rays in the Game view.

You can change these in the Inspector or in code.

### UltrasonicSensor — distance ahead
- `Distance`: metres to the nearest object in a ±15° beam. Reads 4.0 (`MaxRange`) when nothing is in range.
- `InRange`: whether something was detected.
- Event `OnReading(float distance)`.

### LineSensorArray — follow a line
- `Values[i]`: reflectance of each sensor, left to right. Black line ≈ 0.05, white floor ≈ 0.9.
- `IsOnLine(i)`: whether sensor `i` sees the line.
- `Position`: where the line is. -1 means under the leftmost sensor, 0 the centre, +1 the rightmost. When the line is lost, it keeps the last known value.
- `LineDetected`: whether any sensor sees the line.
- `Threshold`: the reflectance below which a sensor counts as on the line.
- Events `OnReading(float[] values)`, `OnLineFound()`, `OnLineLost()`.

### LineSensor — a single line sensor (add via the menu)
- `Reflectance` and `OnLine`.
- Events `OnReading(float)`, `OnLineEnter()`, `OnLineLost()`.

### ColourSensor — floor colour
- `Colour`: RGB colour, with each channel from 0 to 1. Also available as `Red`, `Green` and `Blue`.
- `Named`: one of `NamedColour.Black`, `White`, `Grey`, `Red`, `Orange`, `Yellow`, `Green`, `Blue`, `Purple` or `None`.
- Events `OnReading(Color)` and `OnColourChanged(NamedColour)`.

In the maze, the start pad is **Blue** and the goal pad is **Green**.

### MetalDetector — find hidden metal
- `Strength`: from 0 (nothing) to 1 (right on top of metal).
- `Detected`: true when `Strength` is above `Threshold`.
- Events `OnReading(float)`, `OnMetalDetected()`, `OnMetalLost()`.

The maze has metal plates hidden under the floor.

### Lidar — 360° distance scan
- `Ranges[i]`: distance in metres at angle `i * AngleIncrement` degrees. There are 360 rays, 1° apart, and a full scan takes 10 per second.
  - 0° is straight ahead and angles go anticlockwise: 90° is left, 180° is behind, 270° is right.
- `GetRange(angleDegrees)`: the reading nearest to an angle.
- `AngleOf(i)`: the angle of ray `i`.
- `MaxRange`: 6 m. Rays that hit nothing read this value.
- Event `OnScan(float[] ranges)`.

### GyroSensor — which way am I facing?
- `Heading`: degrees turned since the program started, from -180 to 180. Positive means turned left.
- `TotalRotation`: the same, but without wrapping, so two full left turns read 720.
- `YawRate`: how fast the robot is turning, in degrees per second.
- `ResetHeading()`: makes the current direction 0.
- `DriftDegreesPerSecond`: simulated drift, like a cheap real gyro. Set it above 0 for a challenge.
- Event `OnReading(float heading)`.

## Useful helpers

| Helper | What it does |
|---|---|
| `Log("text")` | Prints to the on-screen console (and the Unity Console). |
| `LogWarning("text")` | Prints a warning to the same places. |
| `Plot("name", value)` | Adds a point to a live graph on screen. Great for tuning controllers. |
| `SimTime` | Seconds since your program started. |
| `ControlRateHz` | How often `OnTick` runs. You can change it. |

## Errors

If your code throws an exception:
- the robot **stops**, and the steering freezes where it is
- the error appears in red on screen, with the line of your code that caused it
- the simulator keeps running

Fix the code and press **R** to restart (or stop and start Play mode after editing).

## Keys while playing

| Key | Action |
|---|---|
| R | Reset the robot to the start and restart the program |
| Space | Pause / resume |
| + / - | Slow down / speed up simulation (0.25× to 4×) |
| C | Switch camera: follow, top-down, orbit (right-drag to rotate, scroll to zoom) |
| G | Show/hide sensor rays |
| H | Show/hide the on-screen panels |
| 1–5 | Swap to a different robot type (see *Robot types*) |
| WASD / arrows | Drive (only with the Keyboard Teleop program); Shift = full power |

**Keyboard Teleop on each robot:**
- **Differential and tracked:** W/S drive, A/D turn.
- **Single wheel and Ackermann:** W/S drive, A/D steer.
- **Synchro:** the keys pick a direction (W forward, A left, W+D diagonally forward-right, and so on). The wheels swing round to face it before the robot moves.

## Keyboard Teleop

Every robot comes with the **Keyboard Teleop** program ticked, so you can drive it by hand with WASD and get a feel for its motors, steering and sensors. Untick it when you add your own program. `MyRobotProgram.cs` in `Assets/StudentPrograms/` is a small starting example.

## Maze facts

| Thing | Size / position |
|---|---|
| Grid | 8 × 8 cells, each 0.5 m |
| Walls | 0.15 m tall and 0.02 m thick |
| Start | South-west corner, robot facing north |
| Goal | North-east corner (green pad) |
| Robots | About 0.2 m wide and 0.22–0.26 m long |

Your teacher can generate a new maze by selecting **Maze** and pressing **Random Seed + Generate**.
