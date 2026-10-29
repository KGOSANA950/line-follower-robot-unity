using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>Message log shown in the on-screen HUD (and mirrored to the Unity Console).</summary>
    public static class RobotConsole
    {
        public readonly struct Entry
        {
            public readonly float Time;
            public readonly string Message;
            public readonly LogType Type;

            public Entry(float time, string message, LogType type)
            {
                Time = time;
                Message = message;
                Type = type;
            }
        }

        const int MaxEntries = 200;
        static readonly List<Entry> entries = new List<Entry>();

        public static IReadOnlyList<Entry> Entries => entries;
        public static event Action<Entry> EntryAdded;

        public static void Log(string message, LogType type = LogType.Log)
        {
            var entry = new Entry(UnityEngine.Time.time, message, type);
            entries.Add(entry);
            if (entries.Count > MaxEntries)
                entries.RemoveAt(0);

            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    Debug.LogError("[Robot] " + message);
                    break;
                case LogType.Warning:
                    Debug.LogWarning("[Robot] " + message);
                    break;
                default:
                    Debug.Log("[Robot] " + message);
                    break;
            }
            EntryAdded?.Invoke(entry);
        }

        public static void Clear() => entries.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            entries.Clear();
            EntryAdded = null;
        }
    }

    /// <summary>Live time-series graphs shown in the HUD. Add points with <c>Plot(name, value)</c>.</summary>
    public static class RobotTelemetry
    {
        public const int Capacity = 300;

        public class Series
        {
            public readonly string Name;
            readonly float[] values = new float[Capacity];
            int head;

            public int Count { get; private set; }
            public float Latest { get; private set; }

            internal Series(string name) => Name = name;

            internal void Add(float value)
            {
                values[head] = value;
                head = (head + 1) % Capacity;
                Count = Mathf.Min(Count + 1, Capacity);
                Latest = value;
            }

            /// <summary>Value <paramref name="i"/> samples old-to-new (0 = oldest kept).</summary>
            public float this[int i] => values[(head - Count + i + Capacity * 2) % Capacity];
        }

        static readonly List<Series> series = new List<Series>();
        public static IReadOnlyList<Series> All => series;

        public static void Plot(string name, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return;
            foreach (var s in series)
            {
                if (s.Name == name)
                {
                    s.Add(value);
                    return;
                }
            }
            var created = new Series(name);
            created.Add(value);
            series.Add(created);
        }

        public static void Clear() => series.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => series.Clear();
    }

    /// <summary>Thrown when student code asks for something that doesn't exist (e.g. a misspelt sensor name).</summary>
    public class RobotException : Exception
    {
        public RobotException(string message) : base(message) { }
    }
}
