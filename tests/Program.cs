using System;
using FarFutureWeaponry.Core;

// Tests for the pure logic (src/Core): antimatter energy, containment, effect curves, colours, trail points.
// Run: DOTNET_ROLL_FORWARD=Major dotnet run --project tests/FarFutureWeaponry.Tests.csproj -c Release
static class Program
{
    static int passed, failed;

    static void Check(bool ok, string what)
    {
        if (ok) passed++;
        else { failed++; Console.WriteLine("FAIL: " + what); }
    }

    static bool Near(double a, double b, double tol = 1e-6) => Math.Abs(a - b) <= tol * Math.Max(1, Math.Abs(b));

    static void AntimatterTests()
    {
        // FFT: 36,000,000 kJ per unit; 4,184 kJ per kg TNT.
        Check(Near(Antimatter.TntKgPerUnit, 36e6 / 4184), "TNT per unit");
        Check(Near(Antimatter.TntKgPerUnit, 8604.2, 1e-4), "one unit is about 8.6 t of TNT");
        Check(Near(Antimatter.TntKg(Antimatter.UnitsForTntKg(3)), 3), "units <-> TNT round trip");
        // The rifle round: 0.00035 units, about 3 kg (Bullets.cfg tntMass = 3).
        Check(Math.Abs(Antimatter.TntKg(0.00035) - 3) < 0.05, "rifle round is about 3 kg TNT (" + Antimatter.TntKg(0.00035) + ")");
        Check(Near(Antimatter.HeatKW(0.001, 0.02), 0.001 * 36e6 / 0.02), "heat flux");
        Check(Antimatter.HeatKW(1, 0) == 0, "no heat over zero time");

        Check(Antimatter.ShotsLeft(0.01, 0.00035) == 28, "rifle magazine holds 28 rounds (" + Antimatter.ShotsLeft(0.01, 0.00035) + ")");
        Check(Antimatter.ShotsLeft(0.2, 0.00035) == 571, "trap holds 571 rounds");
        Check(Antimatter.ShotsLeft(0.00035 * 0.996, 0.00035) == 1, "99.6% of a shot still fires (BDArmory's 0.995)");
        Check(Antimatter.ShotsLeft(0.00035 * 0.99, 0.00035) == 0, "99% of a shot doesn't");
        Check(Antimatter.ShotsLeft(1, 0) == 0 && Antimatter.ShotsLeft(-1, 1) == 0, "degenerate shot counts");
    }

    static void ContainmentTests()
    {
        const double dt = 0.02;
        var c = new Containment { EcPerSecond = 2, GracePeriod = 1, AnnihilationRate = 0.01 };

        Check(c.EcRequest(0, dt) == 0, "empty trap asks for nothing");
        Check(c.Step(0, 0, dt) == 0 && c.Status == ContainmentStatus.Empty, "empty trap is empty");
        Check(Near(c.EcRequest(0.1, dt), 0.04), "request is EC/s x dt");

        Check(c.Step(0.1, 0.04, dt) == 0 && c.Status == ContainmentStatus.Contained, "powered trap holds");

        // Power lost: holds for the grace period, then annihilates at the rate.
        double burnt = 0; int steps = 0;
        double first = 0;
        while (steps < 1000)
        {
            double x = c.Step(0.1, 0, dt); steps++;
            if (c.Status == ContainmentStatus.Annihilating) { first = x; break; }
            burnt += x;
        }
        Check(burnt == 0, "nothing annihilates during the grace period");
        Check(first > 0, "annihilation starts on the step the grace period runs out");
        Check(Math.Abs(steps * dt - 1) < 2 * dt, "grace period lasts about 1 s (" + steps * dt + ")");
        double b = c.Step(0.1, 0, dt);
        Check(Near(b, 0.01 * dt), "annihilates at the rate once failed");
        Check(c.Step(0.0001, 0, dt) == 0.0001, "never more than what's left");

        // Power back: contained again, grace period restarts.
        c.Step(0.1, 0.04, dt);
        Check(c.Status == ContainmentStatus.Contained && c.UnpoweredFor == 0, "power back restores containment");

        // Half power: the shortfall counts as half the time unpowered.
        var h = new Containment { EcPerSecond = 2, GracePeriod = 1 };
        for (int i = 0; i < 50; i++) h.Step(0.1, 0.02, dt);   // 1 s at half power
        Check(Near(h.UnpoweredFor, 0.5, 1e-6), "half power counts half (" + h.UnpoweredFor + ")");
        Check(h.Status == ContainmentStatus.Failing, "half power still failing, not annihilating yet");
        h.Step(0.1, 0.0399, dt);
        Check(h.Status == ContainmentStatus.Failing, "almost-full power is not full power");
        h.Step(0.1, 0.03996, dt);
        Check(h.Status == ContainmentStatus.Contained, "99.9% of the request counts as powered (rounding)");

        // Emptied while failing: resets.
        h.Step(0.1, 0, dt);
        h.Step(0, 0, dt);
        Check(h.Status == ContainmentStatus.Empty && h.UnpoweredFor == 0, "emptying resets");
    }

    static void CurveTests()
    {
        const float d = 0.5f;
        Check(FxCurves.Flash(-0.1f, d) == 0 && FxCurves.Flash(d, d) == 0 && FxCurves.Flash(1, d) == 0, "flash is dark outside its life");
        Check(Near(FxCurves.Flash(0.08f * d, d), 1, 1e-5), "flash peaks at the end of the rise");
        Check(FxCurves.Flash(0.02f, d) < FxCurves.Flash(0.04f, d), "flash rises");
        float prev = 2; bool falling = true;
        for (float t = 0.08f * d; t < d; t += 0.01f) { float v = FxCurves.Flash(t, d); if (v > prev + 1e-6f) falling = false; prev = v; }
        Check(falling, "flash only fades after the peak");
        Check(FxCurves.Flash(d - 1e-4f, d) < 0.01f, "flash reaches ~0 at the end");
        Check(FxCurves.Flash(0.1f, 0) == 0, "zero-length flash is dark");
        Check(FxCurves.FlashSize(0, d) < FxCurves.FlashSize(0.2f, d) && FxCurves.FlashSize(0.2f, d) <= FxCurves.FlashSize(d, d), "flash grows");

        Check(FxCurves.TrailAlpha(0, 3) == 1 && FxCurves.TrailAlpha(0.1f, 3) == 1, "trail holds at first");
        Check(FxCurves.TrailAlpha(3, 3) == 0 && FxCurves.TrailAlpha(5, 3) == 0, "trail gone after fade");
        Check(FxCurves.TrailAlpha(1, 3) > FxCurves.TrailAlpha(2, 3), "trail fades");
        Check(FxCurves.TrailAlpha(-1, 3) == 1, "future points are opaque");
        Check(FxCurves.TrailAlpha(0.01f, 0) == 0, "no fade time: nothing");

        Check(Near(FxCurves.ChargeSparks(0, 2), 0.15, 1e-6), "sparks start as a trickle");
        Check(Near(FxCurves.ChargeSparks(2, 2), 1, 1e-6) && Near(FxCurves.ChargeSparks(5, 2), 1, 1e-6), "full storm when charged");
        Check(FxCurves.ChargeSparks(0.5f, 2) < FxCurves.ChargeSparks(1.5f, 2), "sparks build up");
        Check(FxCurves.ChargeSparks(0, 0) == 1, "no charge time: full rate");
    }

    static void ColourTests()
    {
        var fb = new Rgba(0.1f, 0.2f, 0.3f, 0.4f);
        Rgba c = Rgba.Parse("255, 0, 51, 255", fb);
        Check(Near(c.R, 1) && Near(c.G, 0) && Near(c.B, 0.2) && Near(c.A, 1), "BDArmory 0-255 colour");
        c = Rgba.Parse(" 255,128,0 ", fb);
        Check(Near(c.G, 128 / 255.0) && c.A == 1, "three channels, spaces, default alpha");
        c = Rgba.Parse("#ff8000", fb);
        Check(Near(c.R, 1) && Near(c.G, 128 / 255.0) && c.B == 0 && c.A == 1, "hex colour");
        c = Rgba.Parse("#ff800080", fb);
        Check(Near(c.A, 128 / 255.0), "hex with alpha");
        Check(Rgba.Parse("300, -5, 0", fb).R == 1 && Rgba.Parse("300, -5, 0", fb).G == 0, "channels clamped");
        foreach (string bad in new[] { null, "", "red", "1, 2", "1,2,3,4,5", "#12345", "a, b, c" })
            Check(Rgba.Parse(bad, fb).R == fb.R, "bad colour '" + bad + "' gives the fallback");
    }

    static void TrailTests()
    {
        var t = new TrailPoints { MinSpacing = 2 };
        t.Add(0, 0, 0, 0);
        t.Add(10, 0, 0, 0.1f);
        Check(t.Count == 2, "two points");
        t.Add(11, 0, 0, 0.11f);     // 1 m past the previous stored point's successor: spacing is from point n-2 (0) = 11 m: kept
        Check(t.Count == 3, "far enough from the point before: added");
        t.Add(11.5, 0, 0, 0.12f);   // 1.5 m from point n-2 (10): replaces the head
        Check(t.Count == 3 && t.Points[2].X == 11.5 && t.Points[2].Time == 0.12f, "too close: the head moves");

        t.Prune(0.05f, 1);
        Check(t.Count == 3, "nothing old enough to prune");
        t.Prune(1.105f, 1);
        Check(t.Count == 2 && t.Points[0].X == 10, "old points pruned, keeping one before the first live point");
        Check(!t.Faded(1.105f, 1), "not faded while the head is live");
        Check(t.Faded(1.2f, 1), "faded once the newest point is older than the fade");
        t.Prune(5, 1);
        Check(t.Count == 0, "all pruned");
        Check(t.Faded(0, 1), "empty trail is faded");

        var cap = new TrailPoints { MinSpacing = 0.5, MaxPoints = 10 };
        for (int i = 0; i < 50; i++) cap.Add(i, 0, 0, i);
        Check(cap.Count == 10 && cap.Points[0].X == 40, "point cap keeps the newest");
    }

    static double Rms(float[] s, int from, int to)
    {
        double sum = 0;
        for (int i = from; i < to; i++) sum += s[i] * s[i];
        return Math.Sqrt(sum / Math.Max(1, to - from));
    }

    static void ChargeSoundTests()
    {
        float[] s = ChargeSound.Generate(44100, 2f, 7);
        Check(s.Length == 88200, "sound lasts exactly the charge time (" + s.Length + ")");
        bool inRange = true;
        foreach (float v in s) if (v < -1 || v > 1 || float.IsNaN(v)) inRange = false;
        Check(inRange, "samples within [-1, 1]");
        Check(Math.Abs(s[0]) < 1e-3 && Math.Abs(s[s.Length - 1]) < 1e-3, "starts and ends silent (no clicks)");
        double early = Rms(s, 4410, 22050), late = Rms(s, 66150, 85000);
        Check(late > early * 1.1, $"builds up towards the shot (rms {early:0.000} -> {late:0.000})");
        Check(early > 0.15 && Rms(s, 22050, 66150) > 0.15, $"loud from the start and through the middle (rms {early:0.000})");
        float[] again = ChargeSound.Generate(44100, 2f, 7), other = ChargeSound.Generate(44100, 2f, 8);
        bool same = true, differs = false;
        for (int i = 0; i < s.Length; i++) { if (s[i] != again[i]) same = false; if (s[i] != other[i]) differs = true; }
        Check(same && differs, "same seed same sound; another seed different crackles");
        Check(ChargeSound.Generate(44100, 0, 1).Length == 2205, "zero length gets a 50 ms minimum");
    }

    static void FieldTests()
    {
        Check(FieldSpec.Parse("Gravity") == FieldKind.Gravity && FieldSpec.Parse(" warp ") == FieldKind.Warp &&
              FieldSpec.Parse("tachyon") == FieldKind.Tachyon && FieldSpec.Parse(null) == FieldKind.None && FieldSpec.Parse("x") == FieldKind.None, "field kinds parse");

        // Pull: constant 20 g towards the centre inside 1 km, none outside or at the centre.
        var warp = new FieldSpec { Kind = FieldKind.Warp, Radius = 1000, InnerRadius = 250, AccelG = 20, Duration = 10 };
        Fields.Pull(warp, 600, 0, 800, out double ax, out double ay, out double az);
        double a = Math.Sqrt(ax * ax + ay * ay + az * az);
        Check(Near(a, 20 * 9.81, 1e-9) && ax < 0 && az < 0 && ay == 0 && Near(ax / az, 600.0 / 800, 1e-9), "20 g towards the centre at 1 km");
        Fields.Pull(warp, 100, 0, 0, out ax, out _, out _);
        Check(Near(-ax, 196.2, 1e-9), "the same 20 g close in");
        Fields.Pull(warp, 1001, 0, 0, out ax, out ay, out az);
        Check(ax == 0 && ay == 0 && az == 0, "no pull outside the radius");
        Fields.Pull(warp, 0.5, 0, 0, out ax, out _, out _);
        Check(ax == 0, "no pull in the last metre (no singularity)");
        Check(Fields.Inside(250, 150, 0, 200) && !Fields.Inside(250, 200, 0, 200), "inside the event horizon");

        // Warp timeline: flash, lens forms, holds, collapses at 10 s; pulling and destroying only while it lasts.
        var w0 = Fields.At(warp, 0.01); var w5 = Fields.At(warp, 5); var w99 = Fields.At(warp, 9.99); var w10 = Fields.At(warp, 10.05);
        Check(w0.Flash > 2.5 && w0.LensStrength < 0.05, "warp: blinding flash, lens not formed yet");
        Check(Near(w5.LensStrength, 1, 1e-9) && w5.LensRadius == 1000 && w5.Pulling && w5.Destroying && w5.Flash < 0.01, "warp: full black hole at 5 s, pulling and destroying");
        Check(w99.LensStrength < 0.01 && w99.Pulling, "warp: lens nearly gone at the end of its 10 s");
        Check(!w10.Pulling && !w10.Destroying && w10.Flash > 1 && w10.RingAmp > 0.5, "warp: collapse flash and ripple after 10 s, no more pull");
        Check(Fields.At(warp, Fields.End(warp) + 0.01).Over && !Fields.At(warp, Fields.End(warp) - 0.01).Over, "warp: over after its end");

        // Gravity: bubble shrinks and strengthens while pulling, then a flash.
        var grav = new FieldSpec { Kind = FieldKind.Gravity, Radius = 300, AccelG = 6, Duration = 3, BlastTntKg = 250 };
        var g1 = Fields.At(grav, 0.5); var g2 = Fields.At(grav, 2.5); var g3 = Fields.At(grav, 3.05);
        Check(g1.Pulling && g2.Pulling && !g3.Pulling, "gravity: pulls for 3 s");
        Check(g1.LensRadius > g2.LensRadius && g2.LensStrength > g1.LensStrength && g1.LensRadius <= 300, "gravity: the bubble shrinks and strengthens");
        Check(g3.Flash > 1.5 && g3.LensStrength == 0 && g1.Flash == 0, "gravity: the white flash comes at the collapse");
        Check(g2.LensStrength <= Fields.GravityLens + 1e-9 && g1.CoreShare < 0.02, "gravity: toned-down lens, only a speck of a core");
        var g4 = Fields.At(grav, 3.3); var g5 = Fields.At(grav, 3.8);
        Check(g4.RingRadius < g5.RingRadius && g4.RingAmp > g5.RingAmp && g5.SphereRadius > g4.SphereRadius && g4.SphereAlpha > g5.SphereAlpha && g5.SphereRadius > 0,
              "gravity: the closing pulse spreads out and fades");
        Check(Fields.At(grav, Fields.End(grav) + 0.01).Over && !Fields.At(grav, 3 + Fields.PulseTime).Over, "gravity: over once the pulse has spread");
        // Air drawn in: rings sweep inward while pulling.
        var a1 = Fields.At(grav, 1.0); var a2 = Fields.At(grav, 1.3);
        Check(a1.InRingAmp > 0 && a2.InRingRadius < a1.InRingRadius && a1.InRingRadius <= 300, "gravity: rings of air sweep inward");
        Check(Fields.At(grav, 3.2).InRingAmp == 0, "gravity: no inflow after the collapse");
        var w3 = Fields.At(warp, 3); var w33 = Fields.At(warp, 3.2);
        Check(w3.InRingAmp > 0 && w33.InRingRadius < w3.InRingRadius && w3.CoreShare > 0.1, "warp: air sweeps inward round a real black hole");

        // Tachyon: time slows to a stop over 1.2 s, holds, returns over 2 s; the bomb's clock runs forward, holds, plays back.
        var tach = new FieldSpec { Kind = FieldKind.Tachyon, Radius = 500, Duration = 10 };
        double D = tach.Duration;
        Check(Fields.TimeSpeed(0, D) == 1 && Fields.TimeSpeed(0.6, D) > 0.3 && Fields.TimeSpeed(0.6, D) < 0.7 && Fields.TimeSpeed(1.2, D) == 0 && Fields.TimeSpeed(5, D) == 0,
              "time slows smoothly to a stop and holds");
        Check(Fields.TimeSpeed(10, D) == 0 && Fields.TimeSpeed(11, D) > 0.3 && Fields.TimeSpeed(11, D) < 0.7 && Fields.TimeSpeed(12, D) == 1 && Fields.TimeSpeed(20, D) == 1,
              "time comes back smoothly over 2 s");
        bool monotone = true; double prevS = 2;
        for (double t = 0; t < 1.2; t += 0.01) { double v = Fields.TimeSpeed(t, D); if (v > prevS + 1e-12) monotone = false; prevS = v; }
        Check(monotone, "slowing never speeds up");
        double held = Fields.BombClock(5, D);
        Check(Near(held, Fields.SlowDown / 2, 1e-9) && Near(Fields.BombClock(1.2, D), held, 1e-9), "the bomb's clock stops at half the slow-down (" + held + ")");
        Check(Fields.BombClock(0.3, D) < Fields.BombClock(0.6, D) && Fields.BombClock(0.6, D) - Fields.BombClock(0.3, D) < 0.3, "its clock runs slower as time slows");
        Check(Fields.BombClock(10.5, D) < held && Fields.BombClock(11.5, D) < Fields.BombClock(10.5, D) && Fields.BombClock(12, D) == 0 && Fields.BombClock(13, D) == 0,
              "it plays back to the start while time returns");
        var t0 = Fields.At(tach, 0.02); var t1 = Fields.At(tach, 1); var t5 = Fields.At(tach, 5); var t9 = Fields.At(tach, 9.9);
        var t11 = Fields.At(tach, 11); var t12 = Fields.At(tach, 12.1);
        Check(t0.Freezing && t0.Flash > 2 && t0.SphereRadius < 50, "tachyon: catches craft at once, green flash, sphere starting");
        Check(Near(t1.SphereRadius, 500, 1e-9) && Near(t9.SphereRadius, 500, 1e-9) && t9.Freezing && t9.Speed == 0, "tachyon: sphere holds at full size, time stopped");
        Check(t5.Flash > 0.4 && Near(t5.Flash, t9.Flash, 1e-9) && Near(t5.Tau, held, 1e-9), "tachyon: the flash hangs, frozen, in the middle");
        Check(!t11.Freezing && t11.Slowed && t11.Speed > 0 && t11.Speed < 1 && t11.SphereRadius < 500 && t11.Flash > t5.Flash, "tachyon: time returning, flash playing back (brighter), sphere shrinking");
        Check(!t12.Slowed && t12.Speed == 1 && t12.Flash == 0 && t12.SphereRadius == 0, "tachyon: all back to normal after the speed-up");
        Check(!Fields.At(tach, Fields.End(tach) - 0.01).Over && Fields.At(tach, Fields.End(tach) + 0.01).Over, "tachyon: over after its end");
        Check(!Fields.At(tach, -1).Freezing && !Fields.At(tach, -1).Over, "nothing before the detonation");

        // Blue Rinse: the sphere spreads to 1 km in 2.5 s (killing as it goes), then fades.
        var rinse = new FieldSpec { Kind = FieldKind.Rinse, Radius = 1000 };
        Check(FieldSpec.Parse("rinse") == FieldKind.Rinse && FieldSpec.Parse("Seismic") == FieldKind.Seismic, "rinse and seismic parse");
        var r1 = Fields.At(rinse, 0.5); var r2 = Fields.At(rinse, 2.5); var r3 = Fields.At(rinse, 3);
        Check(r1.SphereRadius > 300 && r1.SphereRadius < 1000 && r1.Killing && Near(r2.SphereRadius, 1000) && r2.Killing, "rinse: spreads fast, slowing, to 1 km");
        var r4 = Fields.At(rinse, 3.49);
        Check(!r3.Killing && r3.SphereRadius > 1050 && r3.SphereAlpha < r2.SphereAlpha && r3.SphereAlpha > 0.2, "rinse: keeps spreading past the radius as it fades, killing no more");
        Check(r4.SphereAlpha < 0.01 && r4.SphereRadius > r3.SphereRadius && Fields.At(rinse, Fields.End(rinse) + 0.01).Over, "rinse: gone about a second past the radius");
        // Seismic charge: a ring at 400 m/s out to 1 km (2.5 s), fading over the last fifth.
        var seis = new FieldSpec { Kind = FieldKind.Seismic, Radius = 1000, RingSpeed = 400 };
        var s1 = Fields.At(seis, 1); var s2 = Fields.At(seis, 2.45); var s3 = Fields.At(seis, 2.55);
        Check(Near(s1.RingRadius2, 400) && s1.RingAlpha2 == 1 && s2.RingRadius2 > 950 && s2.RingAlpha2 < 0.5, "seismic: ring spreads at 400 m/s and fades near 1 km");
        Check(s3.RingRadius2 == 1000 && s3.RingAlpha2 == 0 && Fields.At(seis, 2.7).Over, "seismic: gone after reaching 1 km");
    }

    static void ShatterTests()
    {
        // Two clusters of triangles far apart, 2 fragments: every triangle near the same seed goes to the same group.
        var c = new System.Collections.Generic.List<float>();
        for (int i = 0; i < 20; i++) { c.Add(i * 0.01f); c.Add(0); c.Add(0); }
        for (int i = 0; i < 20; i++) { c.Add(100 + i * 0.01f); c.Add(0); c.Add(0); }
        int[] g = Shatter.Assign(c.ToArray(), 10, 3);
        Check(g.Length == 40, "a group for each triangle");
        bool inRange = true; foreach (int x in g) if (x < 0 || x >= 10) inRange = false;
        Check(inRange, "groups within 0..count-1");
        int[] again = Shatter.Assign(c.ToArray(), 10, 3);
        bool same = true; for (int i = 0; i < g.Length; i++) if (g[i] != again[i]) same = false;
        Check(same, "same seed, same split");
        Check(Shatter.Assign(c.ToArray(), 1, 3)[39] == 0 && Shatter.Assign(new float[0], 5, 1).Length == 0, "one fragment or no triangles");
        // Nearest seed: a triangle is never in a group whose seed is further away than another group's (spot check: the
        // two far clusters never share a group unless the seeds all landed on one side).
        var groupsLeft = new System.Collections.Generic.HashSet<int>(); var groupsRight = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 20; i++) groupsLeft.Add(g[i]);
        for (int i = 20; i < 40; i++) groupsRight.Add(g[i]);
        groupsLeft.IntersectWith(groupsRight);
        Check(groupsLeft.Count == 0, "fragments are spatial chunks (the two far clusters share no fragment)");

        Check(Shatter.Spread(0, 10) == 0 && Near(Shatter.Spread(Shatter.BurstTime, 10), 1, 1e-9) && Near(Shatter.Spread(5, 10), 1, 1e-9), "fragments fly out in the burst, then hang");
        Check(Shatter.Spread(0.1, 10) > 0.5, "fast burst (ease out)");
        Check(Shatter.Spread(10.1, 10) > 0.9 && Shatter.Spread(10.5, 10) < 0.5 && Shatter.Spread(10 + Shatter.ReturnTime, 10) < 1e-6, "they fly back as the field collapses, slow then snapping together");
        Check(!Shatter.Reassembled(10.5, 10) && Shatter.Reassembled(10.6, 10), "reassembled after the return");
    }

    static void FieldSoundTests()
    {
        float[] r = FieldSound.Rumble(44100, 3, true, 1);
        bool ok = r.Length == 132300;
        foreach (float v in r) if (v < -1 || v > 1 || float.IsNaN(v)) ok = false;
        Check(ok, "rumble: right length, in range");
        Check(Math.Abs(r[0]) < 1e-3 && Math.Abs(r[r.Length - 1]) < 1e-3, "rumble: no clicks at the ends");
        Check(Rms(r, 110000, 130000) > Rms(r, 20000, 40000), "rumble swells towards the collapse");
        float[] sw = FieldSound.Sweep(44100, 0.8f, 1400, 140, 2);
        ok = sw.Length == 35280;
        foreach (float v in sw) if (v < -1 || v > 1 || float.IsNaN(v)) ok = false;
        Check(ok && Math.Abs(sw[0]) < 1e-3, "sweep: right length, in range, no click");
        Check(Rms(sw, 1000, 8000) > Rms(sw, 28000, 35000), "sweep fades out");
        float[] se = FieldSound.Seismic(44100, 4, 7);
        ok = se.Length == 176400;
        foreach (float v in se) if (v < -1 || v > 1 || float.IsNaN(v)) ok = false;
        Check(ok && Math.Abs(se[se.Length - 1]) < 1e-3, "seismic sound: right length, in range, no click at the end");
        Check(Rms(se, 0, 5000) < 1e-6 && Rms(se, 6000, 20000) > 0.05, "seismic sound: a beat of silence, then the strike");
        Check(Rms(se, 6000, 20000) > Rms(se, 140000, 170000), "seismic sound rings out");
    }

    static void TrackingTests()
    {
        Check(Tracking.Parse("Optical") == TrackerKind.Optical && Tracking.Parse("commnet") == TrackerKind.CommNet &&
              Tracking.Parse(" consciousness ") == TrackerKind.Consciousness && Tracking.Parse("radar") == TrackerKind.None, "trackers parse");
        var near = new TargetSense { Distance = 3000, Connected = true, Crew = 2 };
        Check(Tracking.CanSee(TrackerKind.Optical, near) && Tracking.CanSee(TrackerKind.CommNet, near) && Tracking.CanSee(TrackerKind.Consciousness, near), "a near crewed connected craft: all see it");
        var far = near; far.Distance = 25000;
        Check(!Tracking.CanSee(TrackerKind.Optical, far) && Tracking.CanSee(TrackerKind.CommNet, far) && !Tracking.CanSee(TrackerKind.Consciousness, far), "beyond 20 km: optical blind; consciousness only to 5 km");
        var cloaked = near; cloaked.Cloaked = true;
        Check(!Tracking.CanSee(TrackerKind.Optical, cloaked) && Tracking.CanSee(TrackerKind.Consciousness, cloaked), "a cloak fools optical only");
        var drone = near; drone.Crew = 0;
        Check(!Tracking.CanSee(TrackerKind.Consciousness, drone) && Tracking.CanSee(TrackerKind.Optical, drone), "no Kerbals: no consciousness to sense");
        var offline = near; offline.Connected = false;
        Check(!Tracking.CanSee(TrackerKind.CommNet, offline), "no link: CommNet can't follow");
        var scrambled = near; scrambled.Scrambled = true;
        Check(!Tracking.CanSee(TrackerKind.CommNet, scrambled), "scrambled: CommNet can't follow");
        Check(!Tracking.CanSee(TrackerKind.None, near), "no tracker sees nothing");

        // Lead: a target 1000 m ahead crossing at 100 m/s, missile at 500 m/s.
        Tracking.Lead(0, 0, 1000, 100, 0, 0, 500, 60, out double ax, out double ay, out double az);
        double t = az / 500 > 0 ? Math.Sqrt(ax * ax + az * az) / 500 : 0;
        Check(ax > 150 && ax < 250 && Near(az, 1000, 1e-9) && Near(Math.Sqrt(ax * ax + az * az), 500 * ax / 100, 1e-6), "lead point meets the target (" + ax.ToString("0.0") + " m ahead)");
        Tracking.Lead(0, 0, 1000, 0, 0, 900, 500, 60, out ax, out ay, out az);
        Check(ax == 0 && az == 1000, "can't catch it: aim at it");
        Tracking.Lead(0, 0, 1000, 0, 0, -200, 500, 60, out ax, out ay, out az);
        Check(Near(az, 1000 - 200 * (1000.0 / 700), 1e-6), "head-on: meets it early");
        Tracking.Lead(0, 0, 1e6, 100, 0, 0, 500, 5, out ax, out ay, out az);
        Check(Near(ax, 500, 1e-9), "lead capped at maxSeconds");
    }

    static void RustDartSlowTests()
    {
        Check(Near(Rust.Add(0, 0.1), 0.1) && Near(Rust.Add(0.5, 0.1), 0.55) && Rust.Add(0.99, 0.5) <= 1 && Rust.Add(1, 1) == 1, "rust builds up, slowing near full");
        Check(Rust.Add(-1, 2) == 1 && Rust.Add(0.3, -1) == 0.3, "rust amounts clamped");
        Rust.Tint(0, out double r0, out double g0, out double b0);
        Rust.Tint(1, out double r1, out double g1, out double b1);
        Check(r0 == 1 && g0 == 1 && b0 == 1 && r1 > g1 && g1 > b1 && r1 < 1 && b1 > 0.2, "rust tints towards orange-brown, never black");
        Check(Rust.Shine(0) == 1 && Rust.Shine(1) < 0.2, "rust is matte");

        double[] cone = Darts.Cone(20, 3);
        bool unit = true, inside = true;
        for (int i = 0; i < 20; i++)
        {
            double x = cone[i * 3], y = cone[i * 3 + 1], z = cone[i * 3 + 2];
            if (Math.Abs(x * x + y * y + z * z - 1) > 1e-9) unit = false;
            if (Math.Acos(z) * 180 / Math.PI > 3 + 1e-9) inside = false;
        }
        Check(cone.Length == 60 && unit && inside, "20 unit directions inside a 3 degree cone");
        double minSep = 10;
        for (int i = 0; i < 20; i++) for (int j = i + 1; j < 20; j++)
            {
                double dot = cone[i * 3] * cone[j * 3] + cone[i * 3 + 1] * cone[j * 3 + 1] + cone[i * 3 + 2] * cone[j * 3 + 2];
                minSep = Math.Min(minSep, Math.Acos(Math.Min(1, dot)) * 180 / Math.PI);
            }
        Check(minSep > 0.3, "no two darts on the same line (" + minSep.ToString("0.00") + " degrees apart at least)");
        Check(Darts.Damage(80, false, 5) == 80 && Darts.Damage(80, true, 5) == 400, "darts shred fragile parts");

        Check(Slow.Profile(0, 0.5, 10) == 1 && Near(Slow.Profile(1, 0.5, 10), 0.5) && Near(Slow.Profile(9.9, 0.5, 10), 0.5), "Chronos: eases down to half speed and holds");
        Check(Slow.Profile(10.6, 0.5, 10) > 0.5 && Slow.Profile(10.6, 0.5, 10) < 1 && Near(Slow.Profile(Slow.End(10), 0.5, 10), 1) && Slow.End(10) == 11.2, "then eases back up to normal");
        Check(Near(Slow.Profile(0.5, 0.9, 2), 0.9) && Slow.Profile(0.2, 0.9, 2) > 0.9 && Slow.Profile(0.2, 0.9, 2) < 1, "gun: 10% slower within 0.4 s");
        Check(Slow.Profile(0.1, 0.5, 0.05) > 0.5, "a hold shorter than the ramp never reaches the factor");
        bool mono = true; double prev = 2;
        for (double t = 0; t < 10; t += 0.05) { double v = Slow.Profile(t, 0.5, 10); if (v > prev + 1e-12) mono = false; prev = v; }
        Check(mono, "slowing never speeds up before the hold ends");
        Check(Slow.NewSpeed(250, 1) == 249 && Slow.NewSpeed(0.5, 1) == 0 && Slow.NewSpeed(10, -3) == 10, "each hit takes 1 m/s, never past a stop");
        Check(Near(Slow.Factor(250, 1), 0.996) && Slow.Factor(0, 1) == 1 && Slow.Factor(0.5, 1) == 0, "velocity factor");
    }

    static int Main()
    {
        TrackingTests();
        RustDartSlowTests();
        FieldTests();
        ShatterTests();
        FieldSoundTests();
        ChargeSoundTests();
        AntimatterTests();
        ContainmentTests();
        CurveTests();
        ColourTests();
        TrailTests();
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
