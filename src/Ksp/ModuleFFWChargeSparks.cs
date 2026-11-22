using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Electric sparks crackling along a barrel while its BDArmory weapon charges (ChargeTime), building up to the
    /// shot, with a flickering light. Reads the weapon's charging state; adds nothing to the weapon itself.
    /// </summary>
    public class ModuleFFWChargeSparks : PartModule
    {
        [KSPField] public string sparkTransformName = "fireTransform";
        [KSPField] public string color = "110, 170, 255, 255";
        [KSPField] public float rate = 160;            // sparks per second just before the shot
        [KSPField] public float barrelLength = 1.5f;   // m behind the transform the sparks spread along
        [KSPField] public float radius = 0.15f;        // m round the barrel's axis
        [KSPField] public float sparkSpeed = 5;
        [KSPField] public float sparkLife = 0.22f;
        [KSPField] public float sparkSize = 0.04f;
        [KSPField] public float lightRange = 8;
        [KSPField] public float lightIntensity = 2.5f;

        PartModule weapon;
        ParticleSystem sparks;
        Light glow;
        Material mat;
        bool wasCharging;
        float chargeStart, chargeTime;

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            weapon = Bda.FindWeapon(part);
            Transform tf = part.FindModelTransform(sparkTransformName);
            if (weapon == null || tf == null)
            {
                Log.Warn($"{part.partInfo?.title}: no {(weapon == null ? "BDArmory weapon" : "transform " + sparkTransformName)}; no charge sparks.");
                enabled = false;
                return;
            }
            Color c = Rgba.Parse(color, new Rgba(0.43f, 0.67f, 1, 1)).ToColor();
            var go = new GameObject("FFW charge sparks");
            go.transform.SetParent(tf, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1;
            main.startLifetime = new ParticleSystem.MinMaxCurve(sparkLife * 0.4f, sparkLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(sparkSpeed * 0.3f, sparkSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(sparkSize * 0.5f, sparkSize);
            main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 400;
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0, 0, -barrelLength * 0.5f);
            shape.scale = new Vector3(radius * 2, radius * 2, barrelLength);
            shape.randomDirectionAmount = 1;
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var emission = sparks.emission;
            emission.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 2;
            mat = Visuals.Additive(Visuals.Glow, new Color(c.r, c.g, c.b, 0.5f));
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            sparks.Play();

            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = c;
            glow.range = lightRange;
            glow.intensity = 0;
            glow.shadows = LightShadows.None;
        }

        void Update()
        {
            if (sparks == null) return;
            bool charging = Bda.IsCharging(weapon) && !vessel.packed;
            if (charging && !wasCharging)
            {
                chargeStart = Time.time;
                chargeTime = Bda.ChargeTime(weapon);
            }
            wasCharging = charging;
            float level = charging ? FxCurves.ChargeSparks(Time.time - chargeStart, chargeTime) : 0;
            var emission = sparks.emission;
            emission.rateOverTime = rate * level;
            glow.intensity = charging ? lightIntensity * level * Random.Range(0.4f, 1.2f) : 0;
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
