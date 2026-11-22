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

    static int Main()
    {
        AntimatterTests();
        ContainmentTests();
        CurveTests();
        ColourTests();
        TrailTests();
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
