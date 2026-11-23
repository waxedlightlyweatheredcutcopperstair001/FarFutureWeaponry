using System.Collections.Generic;
using CommNet;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// What FFW's trackers sense about craft (Core/Tracking decides), and which craft a weapon goes for: the KSP target
    /// of the craft that fired it, or, in guard mode, its weapon manager's target.
    /// Also the antenna blackouts of the Telemetry Leech (and later the signal scrambler).
    /// </summary>
    internal static class Targets
    {
        static readonly Dictionary<Vessel, float> blackoutUntil = new Dictionary<Vessel, float>();
        static readonly List<(ModuleDataTransmitter m, bool enabled, bool isEnabled, Vessel v)> silenced = new List<(ModuleDataTransmitter, bool, bool, Vessel)>();

        public static TargetSense Sense(Vector3 from, Vessel target)
        {
            var s = new TargetSense();
            if (target == null) return s;
            s.Distance = Vector3.Distance(from, target.CoM);
            s.Cloaked = Bda.Cloaked(target);
            s.Connected = Connected(target);
            s.Crew = target.GetCrewCount();
            return s;
        }

        public static bool CanSee(TrackerKind kind, Vector3 from, Vessel target) =>
            target != null && target.loaded && Tracking.CanSee(kind, Sense(from, target));

        /// <summary>
        /// On the CommNet: connected (with CommNet off in the difficulty settings: has a working antenna). Never while its
        /// antennas are blacked out.
        /// </summary>
        public static bool Connected(Vessel v)
        {
            if (v == null || BlackedOut(v)) return false;
            if (CommNetScenario.CommNetEnabled) return v.connection != null && v.connection.IsConnected;
            foreach (Part p in v.parts)
                foreach (PartModule m in p.Modules)
                    if (m is ModuleDataTransmitter t && t.isEnabled && t.enabled) return true;
            return false;
        }

        /// <summary>The craft a weapon fired from <paramref name="source"/> goes for: its KSP target, else its guard-mode target.</summary>
        public static Vessel Choose(Vessel source)
        {
            if (source == null) return null;
            Vessel t = null;
            if (source.isActiveVessel && FlightGlobals.fetch != null) t = FlightGlobals.fetch.VesselTarget?.GetVessel();
            if (t == null) t = source.targetObject?.GetVessel();
            if (t == null) t = Bda.GuardTarget(source);
            return t != null && t != source ? t : null;
        }

        // ---- Antenna blackouts ----

        public static bool BlackedOut(Vessel v) => v != null && blackoutUntil.TryGetValue(v, out float until) && Time.time < until;

        /// <summary>Switch a craft's antennas off for a while (the Telemetry Leech): no CommNet link, can't be CommNet-tracked.</summary>
        public static void Blackout(Vessel v, float seconds)
        {
            if (v == null || seconds <= 0) return;
            bool already = BlackedOut(v);
            blackoutUntil[v] = Mathf.Max(Time.time + seconds, already ? blackoutUntil[v] : 0);
            if (already) return;
            foreach (Part p in v.parts)
                foreach (PartModule m in p.Modules)
                    if (m is ModuleDataTransmitter t)
                    {
                        silenced.Add((t, t.enabled, t.isEnabled, v));
                        t.isEnabled = false; t.enabled = false;
                    }
            Log.Info($"{v.vesselName}: antennas blacked out for {seconds:0} s.");
        }

        /// <summary>Each frame (FieldManager): give antennas back when their blackout ends.</summary>
        public static void Tick()
        {
            for (int i = silenced.Count - 1; i >= 0; i--)
            {
                var (m, en, isEn, v) = silenced[i];
                if (m == null) { silenced.RemoveAt(i); continue; }
                if (BlackedOut(v)) continue;
                m.enabled = en; m.isEnabled = isEn;
                silenced.RemoveAt(i);
            }
        }

        public static void Clear()
        {
            foreach (var (m, en, isEn, _) in silenced) if (m != null) { m.enabled = en; m.isEnabled = isEn; }
            silenced.Clear();
            blackoutUntil.Clear();
        }
    }
}
