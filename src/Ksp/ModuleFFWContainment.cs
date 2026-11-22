using FarFutureWeaponry.Core;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Magnetic containment for the antimatter a part holds (the Antimatter-ial rifle's magazine, the antimatter trap).
    /// Works like Far Future Technologies' tanks: a flat ElectricCharge cost while any antimatter is held; without
    /// power, after a short grace period, the antimatter annihilates against the walls and heats the part until it
    /// overheats and explodes. If the part also has a BDArmory weapon that fires this resource, the window shows how
    /// many shots are left.
    /// </summary>
    public class ModuleFFWContainment : PartModule
    {
        [KSPField] public string resourceName = Antimatter.ResourceName;
        [KSPField] public float ecPerSecond = 1;
        [KSPField] public float gracePeriod = 2;
        [KSPField] public float annihilationRate = 0.001f;   // units/s once containment has failed

        [KSPField(guiActive = true, guiName = "Containment")] public string status = "";
        [KSPField(guiActive = false, guiName = "Antimatter rounds")] public string rounds = "";

        readonly Containment trap = new Containment();
        PartResource resource;
        PartModule weapon;
        float perShot;
        ContainmentStatus lastStatus = ContainmentStatus.Empty;

        public override void OnStart(StartState state)
        {
            trap.EcPerSecond = ecPerSecond;
            trap.GracePeriod = gracePeriod;
            trap.AnnihilationRate = annihilationRate;
            resource = part.Resources.Get(resourceName);
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            weapon = Bda.FindWeapon(part);
            if (Bda.AmmoUse(weapon, out string ammo, out float shot) && ammo == resourceName && shot > 0)
            {
                perShot = shot;
                Fields[nameof(rounds)].guiActive = true;
            }
        }

        public override string GetInfo()
        {
            return $"Magnetic antimatter containment.\n<b>Containment:</b> {ecPerSecond:0.##} EC/s while holding {resourceName}.\n" +
                   $"Without power for {gracePeriod:0.#} s the {resourceName} starts to annihilate " +
                   $"({annihilationRate * Antimatter.KJPerUnit / 1000:N0} MW of heat) until the part explodes.\n" +
                   $"1 unit = {Antimatter.TntKgPerUnit / 1000:0.#} t TNT.";
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || resource == null || vessel == null) return;
            double dt = TimeWarp.fixedDeltaTime;
            double amount = resource.amount;
            double request = trap.EcRequest(amount, dt);
            double got = request > 0 ? part.RequestResource("ElectricCharge", request, ResourceFlowMode.ALL_VESSEL) : 0;
            double burnt = trap.Step(amount, got, dt);
            if (burnt > 0)
            {
                burnt = part.RequestResource(resourceName, burnt, ResourceFlowMode.NO_FLOW);
                part.AddThermalFlux(Antimatter.HeatKW(burnt, dt));
            }
            if (trap.Status != lastStatus)
            {
                if (trap.Status == ContainmentStatus.Failing && vessel.isActiveVessel)
                    ScreenMessages.PostScreenMessage($"{part.partInfo.title}: antimatter containment is losing power!", 4, ScreenMessageStyle.UPPER_CENTER);
                if (trap.Status == ContainmentStatus.Annihilating)
                {
                    Log.Warn($"{part.partInfo.title} on {vessel.vesselName}: containment failed, {amount:0.####} {resourceName} annihilating.");
                    if (vessel.isActiveVessel)
                        ScreenMessages.PostScreenMessage($"{part.partInfo.title}: CONTAINMENT FAILED. Antimatter is annihilating!", 5, ScreenMessageStyle.UPPER_CENTER);
                }
                lastStatus = trap.Status;
            }
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || resource == null) return;
            switch (trap.Status)
            {
                case ContainmentStatus.Empty: status = "Empty"; break;
                case ContainmentStatus.Contained: status = $"Contained ({ecPerSecond:0.##} EC/s)"; break;
                case ContainmentStatus.Failing: status = $"NO POWER ({gracePeriod - trap.UnpoweredFor:0.0} s)"; break;
                default: status = "FAILED: annihilating"; break;
            }
            if (perShot > 0) rounds = Antimatter.ShotsLeft(resource.amount, perShot).ToString();
        }
    }
}
