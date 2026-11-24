using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
            s.Cloaked = Bda.Cloaked(target) || ModuleFFWCloak.IsCloaked(target);
            s.Scrambled = Hidden(target);
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
                        TimeStop.Suppressed[t] = t.isEnabled;     // (saved as it was, not switched off)
                        t.isEnabled = false; t.enabled = false;
                    }
            Log.Info($"{v.vesselName}: antennas blacked out for {seconds:0} s.");
        }

        /// <summary>Each frame (FieldManager): give antennas back when their blackout ends.</summary>
        public static void Tick()
        {
            float now = Time.time;
            for (int i = radarsOff.Count - 1; i >= 0; i--)
            {
                if (radarsOff[i].radar != null && now < radarsOff[i].until) continue;
                if (radarsOff[i].radar != null) radarsOff[i].radar.GetType().GetMethod("EnableRadar")?.Invoke(radarsOff[i].radar, null);
                radarsOff.RemoveAt(i);
            }
            scratch.Clear();
            foreach (var kv in noControl) if (kv.Key == null || now > kv.Value.until) scratch.Add(kv.Key);
            foreach (Vessel v in scratch) { if (v != null) { v.OnFlyByWire -= noControl[v].cb; Log.Info($"{v.vesselName}: remote control back."); } noControl.Remove(v); }
            for (int i = silenced.Count - 1; i >= 0; i--)
            {
                var (m, en, isEn, v) = silenced[i];
                if (m == null) { silenced.RemoveAt(i); continue; }
                if (BlackedOut(v)) continue;
                m.enabled = en; m.isEnabled = isEn;
                TimeStop.Suppressed.Remove(m);
                silenced.RemoveAt(i);
            }
        }

        // ---- Signal scrambler ----

        static readonly Dictionary<Vessel, float> hiddenUntil = new Dictionary<Vessel, float>();
        static readonly List<(PartModule radar, float until)> radarsOff = new List<(PartModule, float)>();
        static readonly Dictionary<Vessel, (float until, FlightInputCallback cb)> noControl = new Dictionary<Vessel, (float, FlightInputCallback)>();
        static readonly List<Vessel> scratch = new List<Vessel>();

        /// <summary>A craft running a signal scrambler: CommNet tracking can't follow it for a while.</summary>
        public static void Hide(Vessel v, float seconds) { if (v != null) hiddenUntil[v] = Time.time + seconds; }
        public static bool Hidden(Vessel v) => v != null && hiddenUntil.TryGetValue(v, out float until) && Time.time < until;

        /// <summary>Scrambled: the craft's radars go dark for a while (BDArmory's DisableRadar; switched back on after).</summary>
        public static void RadarsOff(Vessel v, float seconds)
        {
            Type radar = Bda.FindType("BDArmory.Radar.ModuleRadar");
            if (v == null || radar == null) return;
            FieldInfo on = radar.GetField("radarEnabled");
            foreach (Part p in v.parts)
                foreach (PartModule m in p.Modules)
                {
                    if (!radar.IsInstanceOfType(m) || on == null || !(bool)on.GetValue(m)) continue;
                    radar.GetMethod("DisableRadar")?.Invoke(m, null);
                    radarsOff.Add((m, Time.time + seconds));
                }
        }

        /// <summary>Scrambled: a remotely controlled craft (no Kerbals aboard) can't be flown for a while (its controls held neutral).</summary>
        public static void NoControl(Vessel v, float seconds)
        {
            if (v == null || v.GetCrewCount() > 0 || v.isEVA) return;
            if (noControl.TryGetValue(v, out var old)) { noControl[v] = (Time.time + seconds, old.cb); return; }
            FlightInputCallback cb = st => { st.Neutralize(); };
            v.OnFlyByWire += cb;
            noControl[v] = (Time.time + seconds, cb);
            Log.Info($"{v.vesselName}: remote control lost for {seconds:0} s.");
        }

        // ---- Breaking locks (the micro-hop drive) ----

        static readonly Dictionary<Vessel, float> lockBroken = new Dictionary<Vessel, float>();

        /// <summary>When the craft last broke every lock on it, or -1.</summary>
        public static float LockBrokenAt(Vessel v) => v != null && lockBroken.TryGetValue(v, out float at) ? at : -1;

        /// <summary>
        /// Break every lock on the craft: FFW seekers lose it (ModuleFFWSeeker checks LockBrokenAt), BDArmory radars
        /// locked on it drop their locks, and BDArmory missiles going for it lose their target.
        /// </summary>
        public static void BreakLocks(Vessel v)
        {
            if (v == null) return;
            lockBroken[v] = Time.time;
            Type radar = Bda.FindType("BDArmory.Radar.ModuleRadar");
            FieldInfo locks = radar?.GetField("lockedTargets", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Type missile = Bda.FindType("BDArmory.Weapons.Missiles.MissileBase");
            PropertyInfo targetVessel = missile?.GetProperty("targetVessel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);   // (a property in both BDArmorys)
            int radars = 0, missiles = 0;
            foreach (Vessel other in FlightGlobals.VesselsLoaded)
            {
                if (other == null || other == v) continue;
                foreach (Part p in other.parts)
                    foreach (PartModule m in p.Modules)
                    {
                        if (radar != null && locks != null && radar.IsInstanceOfType(m) && locks.GetValue(m) is IList list)
                        {
                            bool onUs = false;
                            foreach (object t in list)
                                if (t?.GetType().GetField("vessel")?.GetValue(t) is Vessel tv && tv == v) { onUs = true; break; }
                            if (onUs) { radar.GetMethod("UnlockAllTargets")?.Invoke(m, null); radars++; }
                        }
                        if (missile != null && missile.IsInstanceOfType(m) && Bda.MissileFired(m) && targetVessel != null)
                        {
                            object ti = targetVessel.GetValue(m);
                            if (ti is Component c && c != null && Bda.InfoVessel(ti) is Vessel mv && mv == v)
                            {
                                if (targetVessel.CanWrite) targetVessel.SetValue(m, null, null);     // (drop the target itself)
                                Bda.SteerMissile(m, Bda.MissileTargetPosition(m), Vector3.zero, false);
                                missiles++;
                            }
                        }
                    }
            }
            Log.Info($"{v.vesselName}: locks broken ({radars} radar(s), {missiles} BDArmory missile(s), and FFW seekers).");
        }

        // ---- Holograms ----

        static readonly Dictionary<Vessel, Func<int, Vector3?>> holograms = new Dictionary<Vessel, Func<int, Vector3?>>();

        /// <summary>A craft's hologram generator, while it runs: where hologram i is (null when they're gone).</summary>
        public static void SetHolograms(Vessel v, Func<int, Vector3?> where) { if (v != null) { if (where == null) holograms.Remove(v); else holograms[v] = where; } }
        public static Vector3? Hologram(Vessel v, int i) => v != null && holograms.TryGetValue(v, out var f) ? f(i) : null;

        public static void Clear()
        {
            foreach (var (m, _) in radarsOff) if (m != null) m.GetType().GetMethod("EnableRadar")?.Invoke(m, null);
            radarsOff.Clear();
            foreach (var kv in noControl) if (kv.Key != null) kv.Key.OnFlyByWire -= kv.Value.cb;
            noControl.Clear();
            hiddenUntil.Clear(); lockBroken.Clear(); holograms.Clear();
            foreach (var (m, en, isEn, _) in silenced) if (m != null) { m.enabled = en; m.isEnabled = isEn; TimeStop.Suppressed.Remove(m); }
            silenced.Clear();
            blackoutUntil.Clear();
        }
    }
}
