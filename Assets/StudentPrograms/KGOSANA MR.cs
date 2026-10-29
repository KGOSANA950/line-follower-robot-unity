using RobotSim;
using UnityEngine;

/*
 * Name: Kgosana MR
 *
 * Q1 Approach: (we will write this last)
 * Q2 Debugging: (we will write this last)
 * Q3 Tuning: (we will write this last)
 */

public class OKGOSANA: DifferentialDriveProgram
{
    enum State { FollowLine, Coast, SearchFirst, SearchSecond, Settle, Finished }

    // ---- Parameters (public so they can be tuned in the Inspector) ----
    public float straightPower = 0.4f;       // forward motor power on a straight (line centred), range 0 to 1
    public float bendPower = 0.2f;           // forward motor power in a bend (line near the sensor edge), range 0 to 1
    public float bendPosition = 0.5f;        // |line position| at which the speed has dropped to bendPower (-1 to +1 scale)
    public float kp = 0.35f;                 // P gain: motor power per unit of line position (-1 to +1)
    public float kd = 0.02f;                 // D gain: motor power per (position unit per second)
    public float maxCorrection = 0.3f;       // limit on the steering correction (motor power)
    public float coastPower = 0.2f;          // forward motor power while coasting after the line ends, range 0 to 1
    public float coastDistanceM = 0.08f;     // drive this far straight before declaring the line lost (metres)
    public float searchPower = 0.25f;        // spin motor power while searching for the line, range 0 to 1
    public float firstSweepS = 2.5f;         // spin toward the side the line was last seen (seconds)
    public float secondSweepS = 5.0f;        // then spin the other way, past the start heading (seconds)
    public float settleDistanceM = 0.10f;    // drive this far onto the green pad before stopping (metres)
    public float metalSpacingM = 0.15f;      // detections closer together than this count as the same plate (metres)

    // ---- Sensors ----
    LineSensorArray lineArray;
    ColourSensor colourSensor;
    MetalDetector metalDetector;

    // ---- State ----
    State state = State.FollowLine;
    float lastPosition;                      // line position on the previous tick (-1 to +1)
    float coastStartM;                       // TravelledM() when the coast began (metres)
    float settleStartM;                      // TravelledM() when green was first seen (metres)
    float searchDir;                         // +1 = spin left, -1 = spin right, toward where the line was last seen
    float searchStartS;                      // SimTime when the current sweep began (seconds)
    int plateCount;                          // metal plates found so far
    float lastPlateM = -100f;                // TravelledM() at the last counted plate (metres)

    protected override void OnStart()
    {
        lineArray = GetSensor<LineSensorArray>("LineArray");
        colourSensor = GetSensor<ColourSensor>("Colour");
        metalDetector = GetSensor<MetalDetector>("Metal");

        // Event: runs every time the colour under the robot changes
        colourSensor.OnColourChanged += colour =>
        {
            if (colour == NamedColour.Green)
                ReachGoal();
        };

        // Event: runs when the detector finds a metal plate
        metalDetector.OnMetalDetected += OnMetalFound;

        Log("STATE: " + state);
    }

    protected override void OnTick(float dt)
    {
        // Backup for the event: also check the colour every tick
        if (colourSensor.Named == NamedColour.Green)
            ReachGoal();

        switch (state)
        {
            case State.FollowLine: DoFollowLine(dt); break;
            case State.Coast: DoCoast(); break;
            case State.SearchFirst: DoSearch(searchDir, firstSweepS, State.SearchSecond); break;
            case State.SearchSecond: DoSearch(-searchDir, secondSweepS, State.SearchFirst); break;
            case State.Settle: DoSettle(); break;
            case State.Finished: Drive.SetPower(0f, 0f); break;
        }
    }

    void ChangeState(State next)
    {
        state = next;
        Log("STATE: " + next);
    }

    // Average of both wheel encoders: spinning on the spot adds nothing, so this is progress along the route
    float TravelledM()
    {
        return (Drive.Left.Distance + Drive.Right.Distance) / 2f;
    }

    // Count each plate once, even if the detector reports it on several ticks
    void OnMetalFound()
    {
        if (state == State.Finished) return;

        float d = TravelledM();
        if (d - lastPlateM < metalSpacingM) return;

        lastPlateM = d;
        plateCount++;
        Log("METAL #" + plateCount + " at " + d.ToString("F2") + " m");
    }

    // Green seen: start driving onto the pad (the colour sensor is at the front, the body is not on it yet)
    void ReachGoal()
    {
        if (state == State.Settle || state == State.Finished) return;

        settleStartM = TravelledM();
        ChangeState(State.Settle);
    }

    void DoSettle()
    {
        if (TravelledM() - settleStartM < settleDistanceM)
        {
            Drive.SetPower(coastPower, coastPower);
            return;
        }

        Stop();
        ChangeState(State.Finished);
        Log("GOAL REACHED");
        Log("SUMMARY: time=" + SimTime.ToString("F1") + " s, distance=" +
            TravelledM().ToString("F2") + " m, plates=" + plateCount);
    }

    void DoFollowLine(float dt)
    {
        if (!lineArray.LineDetected)
        {
            coastStartM = TravelledM();
            ChangeState(State.Coast);
            return;
        }

        float position = lineArray.Position;
        float derivative = (position - lastPosition) / dt;
        lastPosition = position;

        float correction = Mathf.Clamp(kp * position + kd * derivative,
                                       -maxCorrection, maxCorrection);

        // Adaptive speed: fast when the line is centred, slower the further it drifts to the edge
        float bendFraction = Mathf.Clamp01(Mathf.Abs(position) / bendPosition);
        float speed = Mathf.Lerp(straightPower, bendPower, bendFraction);

        Drive.SetPower(speed + correction, speed - correction);

        Plot("Line position", position);
        Plot("Speed", speed);
    }

    // The line just ended: drive straight a short way before giving up on it.
    void DoCoast()
    {
        if (lineArray.LineDetected)
        {
            lastPosition = lineArray.Position;   // avoid a false derivative spike
            ChangeState(State.FollowLine);
            return;
        }

        if (TravelledM() - coastStartM >= coastDistanceM)
        {
            // Line position > 0 means the line was to the right, so spin right (-1)
            searchDir = (lastPosition > 0f) ? -1f : 1f;
            searchStartS = SimTime;
            Log("LINE LOST");
            ChangeState(State.SearchFirst);
            return;
        }

        Drive.SetPower(coastPower, coastPower);
    }

    // Spin on the spot (dir = +1 left, -1 right) until the line is seen or the sweep time runs out.
    void DoSearch(float dir, float sweepS, State nextSweep)
    {
        if (lineArray.LineDetected)
        {
            lastPosition = lineArray.Position;   // avoid a false derivative spike
            Log("LINE FOUND");
            ChangeState(State.FollowLine);
            return;
        }

        if (SimTime - searchStartS >= sweepS)
        {
            searchStartS = SimTime;
            ChangeState(nextSweep);
            return;
        }

        Drive.SetPower(-dir * searchPower, dir * searchPower);
    }
}