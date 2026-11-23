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
        Check(late > early * 1.5, $"builds up towards the shot (rms {early:0.000} -> {late:0.000})");
        Check(Rms(s, 22050, 66150) > 0.05, "audible through the middle of the charge");
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
        Check(g3.Flash > 2 && g3.LensStrength == 0 && g1.Flash == 0, "gravity: the white flash comes at the collapse");

        // Tachyon: sphere grows in half a second, holds at 500 m, closes; freezing only during the 10 s.
        var tach = new FieldSpec { Kind = FieldKind.Tachyon, Radius = 500, Duration = 10 };
        var t0 = Fields.At(tach, 0.02); var t1 = Fields.At(tach, 1); var t9 = Fields.At(tach, 9.9); var t10 = Fields.At(tach, 10.3); var t11 = Fields.At(tach, 10.65);
        Check(t0.Freezing && t0.Flash > 2 && t0.SphereRadius < 50, "tachyon: freezes at once, green flash, sphere starting");
        Check(Near(t1.SphereRadius, 500, 1e-9) && t9.Freezing && Near(t9.SphereRadius, 500, 1e-9), "tachyon: holds at full size");
        Check(!t10.Freezing && t10.SphereRadius < 500 && t10.SphereRadius > 0, "tachyon: released at 10 s, sphere closing");
        Check(t11.SphereRadius == 0 && t11.Flash > 0.5, "tachyon: a flash as the sphere closes");
        Check(!Fields.At(tach, Fields.End(tach) - 0.01).Over && Fields.At(tach, Fields.End(tach) + 0.01).Over, "tachyon: over after its end");
        Check(!Fields.At(tach, -1).Freezing && !Fields.At(tach, -1).Over, "nothing before the detonation");
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
    }

    static int Main()
    {
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
