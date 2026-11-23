using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Read-only access to BDArmory by reflection, so one DLL works with BDArmory Lite and with full BDArmory (both
    /// assemblies are named BDArmory and share these members; nothing here is compiled against either).
    /// Every accessor fails soft: if a member is missing (a BDArmory update renamed it), the feature using it turns
    /// off and the reason is logged once.
    /// </summary>
    internal static class Bda
    {
        static bool initialised;
        public static bool Present { get; private set; }

        // Explosions: BDArmory.FX.ExplosionFx.explosionFXPools (Dictionary<string, ObjectPool>), keyed by model path.
        static IDictionary explosionPools;
        static Type explosionFxType;
        static PropertyInfo explosionStartTime, explosionPosition;
        static FieldInfo explosionHitPart;                     // ExplosionFx.projectileHitPart (the part a laser pulse or shell hit)
        static FieldInfo poolList;                       // BDArmory.Utils.ObjectPool.pool (List<GameObject>)

        // Bullets: BDArmory.Bullets.PooledBulletManager.Instance.activeBullets (HashSet<PooledBullet>).
        static FieldInfo bulletManagerInstance, activeBulletsField;
        static FieldInfo bulletInfoField, bulletTimeAlive;    // PooledBullet.bullet (BulletInfo), .timeAlive
        static FieldInfo bulletSourceWeapon, bulletVelocity;  // PooledBullet.sourceWeapon (Part), .currentVelocity
        static PropertyInfo bulletInfoName;                   // BulletInfo.name

        // Weapons: BDArmory.Weapons.ModuleWeapon.isCharging (private bool), .ChargeTime.
        public static Type ModuleWeaponType { get; private set; }
        static FieldInfo weaponIsCharging, weaponChargeTime, weaponAmmoName, weaponAmmoPerShot, weaponFireTransforms, weaponImpulse, weaponLasers;
        static PropertyInfo explosionHitPartProp;
        static Type missileType, wmType, cloakType;
        static PropertyInfo missileHasFired, missileTargetPosition, missileTargetVelocity, missileTargetAcquired, missileSourceVessel;
        static MethodInfo missileDetonate, addDamage;
        static FieldInfo wmCurrentTarget, cloakEnabled, cloakOptical;

        static readonly HashSet<string> warned = new HashSet<string>();

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static void Init()
        {
            if (initialised) return;
            initialised = true;
            Assembly bda = null;
            foreach (var la in AssemblyLoader.loadedAssemblies)
                if (la.name == "BDArmory") { bda = la.assembly; break; }
            if (bda == null)
            {
                Log.Error("BDArmory is not installed: Far Future Weaponry's weapons need BDArmory or BDArmory Lite.");
                return;
            }
            Present = true;

            explosionFxType = bda.GetType("BDArmory.FX.ExplosionFx");
            explosionPools = explosionFxType?.GetField("explosionFXPools", Any)?.GetValue(null) as IDictionary;
            explosionStartTime = explosionFxType?.GetProperty("StartTime", Any);
            explosionPosition = explosionFxType?.GetProperty("Position", Any);
            explosionHitPart = explosionFxType?.GetField("projectileHitPart", Any);
            if (explosionHitPart == null)
            {
                PropertyInfo hp = explosionFxType?.GetProperty("projectileHitPart", Any);
                if (hp != null) explosionHitPartProp = hp;
            }

            Type missile = bda.GetType("BDArmory.Weapons.Missiles.MissileBase");
            missileType = missile;
            missileHasFired = missile?.GetProperty("HasFired", Any);
            missileTargetPosition = missile?.GetProperty("TargetPosition", Any);
            missileTargetVelocity = missile?.GetProperty("TargetVelocity", Any);
            missileTargetAcquired = missile?.GetProperty("TargetAcquired", Any);
            missileSourceVessel = missile?.GetProperty("SourceVessel", Any);
            missileDetonate = missile?.GetMethod("Detonate", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Type wm = bda.GetType("BDArmory.Control.MissileFire");
            wmType = wm;
            wmCurrentTarget = wm?.GetField("currentTarget", Any);
            Type ext = bda.GetType("BDArmory.Extensions.PartExtensions");
            addDamage = ext?.GetMethod("AddDamage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Part), typeof(float) }, null);
            cloakType = bda.GetType("BDArmory.CounterMeasure.ModuleCloakingDevice");
            cloakEnabled = cloakType?.GetField("cloakEnabled", Any);
            cloakOptical = cloakType?.GetField("OpticalCloaking", Any);
            poolList = bda.GetType("BDArmory.Utils.ObjectPool")?.GetField("pool", Any);

            Type manager = bda.GetType("BDArmory.Bullets.PooledBulletManager");
            Type bullet = bda.GetType("BDArmory.Bullets.PooledBullet");
            bulletManagerInstance = manager?.GetField("Instance", Any);
            activeBulletsField = manager?.GetField("activeBullets", Any);
            bulletInfoField = bullet?.GetField("bullet", Any);
            bulletTimeAlive = bullet?.GetField("timeAlive", Any);
            bulletSourceWeapon = bullet?.GetField("sourceWeapon", Any);
            bulletVelocity = bullet?.GetField("currentVelocity", Any);
            bulletInfoName = bda.GetType("BDArmory.Bullets.BulletInfo")?.GetProperty("name", Any);

            ModuleWeaponType = bda.GetType("BDArmory.Weapons.ModuleWeapon");
            weaponIsCharging = ModuleWeaponType?.GetField("isCharging", Any);
            weaponChargeTime = ModuleWeaponType?.GetField("ChargeTime", Any);
            weaponAmmoName = ModuleWeaponType?.GetField("ammoName", Any);
            weaponAmmoPerShot = ModuleWeaponType?.GetField("requestResourceAmount", Any);
            weaponFireTransforms = ModuleWeaponType?.GetField("fireTransforms", Any);
            weaponImpulse = ModuleWeaponType?.GetField("Impulse", Any);
            weaponLasers = ModuleWeaponType?.GetField("laserRenderers", Any);

            Log.Info($"BDArmory {bda.GetName().Version} found ({(Lite ? "BDArmory Lite" : "BDArmory")}). " +
                     $"Explosions: {Ok(ExplosionsReadable)}, bullets: {Ok(BulletsReadable)}, weapon charge: {Ok(weaponIsCharging != null && weaponChargeTime != null)}, " +
                     $"weapon ammo: {Ok(weaponAmmoName != null && weaponAmmoPerShot != null)}.");
        }

        static string Ok(bool ok) => ok ? "ok" : "MISSING (that feature is off)";

        /// <summary>BDArmory Lite is BDArmory renamed in GameData (same assembly), so tell them apart by folder.</summary>
        public static bool Lite => System.IO.Directory.Exists(System.IO.Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmoryLite"));

        static void WarnOnce(string key, string msg)
        {
            if (warned.Add(key)) Log.Warn(msg);
        }

        // ---- Explosions ----

        public static bool ExplosionsReadable => explosionPools != null && explosionStartTime != null && explosionPosition != null && poolList != null;

        /// <summary>
        /// The pooled explosion objects BDArmory made for <paramref name="modelPath"/> (the weapon's explModelPath), or
        /// null if none has gone off yet.
        /// </summary>
        public static IList ExplosionPool(string modelPath)
        {
            if (!ExplosionsReadable) return null;
            try
            {
                if (!explosionPools.Contains(modelPath)) return null;
                object pool = explosionPools[modelPath];
                return pool == null || (pool is UnityEngine.Object o && o == null) ? null : poolList.GetValue(pool) as IList;
            }
            catch (Exception e)
            {
                WarnOnce("pool", "Couldn't read BDArmory's explosion pools: " + e.Message);
                return null;
            }
        }

        /// <summary>Start time and position of a pooled explosion object, if it's an active explosion.</summary>
        public static bool ReadExplosion(GameObject go, out float startTime, out Vector3 position)
        {
            startTime = 0; position = default;
            if (go == null || !go.activeInHierarchy) return false;
            Component fx = go.GetComponent(explosionFxType);
            if (fx == null) return false;
            startTime = (float)explosionStartTime.GetValue(fx, null);
            position = (Vector3)explosionPosition.GetValue(fx, null);
            return true;
        }

        static IDictionary nukePools;
        static Type nukeFxType;
        static PropertyInfo nukeStartTime;
        static bool nukeLooked;

        /// <summary>
        /// Every live BDArmory explosion (ExplosionFx and NukeFX components, from their pools): to pause their damage,
        /// disable the component, and on resuming move StartTime on by the pause (they time everything from it).
        /// </summary>
        public static void LiveExplosions(List<(Behaviour fx, PropertyInfo startTime)> into)
        {
            into.Clear();
            if (!nukeLooked)
            {
                nukeLooked = true;
                foreach (var la in AssemblyLoader.loadedAssemblies)
                    if (la.name == "BDArmory") { nukeFxType = la.assembly.GetType("BDArmory.FX.NukeFX"); break; }
                nukePools = nukeFxType?.GetField("nukePool", Any)?.GetValue(null) as IDictionary;
                nukeStartTime = nukeFxType?.GetProperty("StartTime", Any);
            }
            Collect(explosionPools, explosionFxType, explosionStartTime, into);
            Collect(nukePools, nukeFxType, nukeStartTime, into);
        }

        static void Collect(IDictionary pools, Type type, PropertyInfo start, List<(Behaviour, PropertyInfo)> into)
        {
            if (pools == null || type == null || start == null || poolList == null) return;
            foreach (object pool in pools.Values)
            {
                if (pool == null || (pool is UnityEngine.Object o && o == null)) continue;
                if (!(poolList.GetValue(pool) is IList list)) continue;
                foreach (object item in list)
                {
                    if (!(item is GameObject go) || go == null || !go.activeInHierarchy) continue;
                    if (go.GetComponent(type) is Behaviour b && b != null) into.Add((b, start));
                }
            }
        }

        /// <summary>The part an explosion's projectile hit (a laser pulse, a shell), or null.</summary>
        public static Part ExplosionHitPart(GameObject go)
        {
            if (go == null || explosionFxType == null) return null;
            Component fx = go.GetComponent(explosionFxType);
            if (fx == null) return null;
            if (explosionHitPart != null) return explosionHitPart.GetValue(fx) as Part;
            return explosionHitPartProp?.GetValue(fx, null) as Part;
        }

        // ---- Damage ----

        /// <summary>Structural damage through BDArmory's hitpoints (PartExtensions.AddDamage).</summary>
        public static void AddDamage(Part p, float damage)
        {
            if (p == null || addDamage == null || damage <= 0) return;
            try { addDamage.Invoke(null, new object[] { p, damage }); }
            catch (Exception e) { WarnOnce("damage", "BDArmory damage failed: " + (e.InnerException ?? e).Message); }
        }

        // ---- Missiles ----

        public static PartModule FindMissile(Part part)
        {
            if (missileType == null || part == null) return null;
            for (int i = 0; i < part.Modules.Count; i++)
                if (missileType.IsInstanceOfType(part.Modules[i])) return part.Modules[i];
            return null;
        }

        public static bool MissileFired(PartModule missile) => missile != null && missileHasFired != null && (bool)missileHasFired.GetValue(missile, null);

        public static Vessel MissileSource(PartModule missile) => missile != null ? missileSourceVessel?.GetValue(missile, null) as Vessel : null;

        public static Vector3 MissileTargetPosition(PartModule missile) =>
            missile != null && missileTargetPosition != null ? (Vector3)missileTargetPosition.GetValue(missile, null) : Vector3.zero;

        /// <summary>Steer a missile (targetingType none: BDArmory leaves these alone) at a point.</summary>
        public static void SteerMissile(PartModule missile, Vector3 position, Vector3 velocity, bool acquired)
        {
            if (missile == null || missileTargetPosition == null) return;
            missileTargetPosition.SetValue(missile, position, null);
            missileTargetVelocity?.SetValue(missile, velocity, null);
            missileTargetAcquired?.SetValue(missile, acquired, null);
        }

        public static void DetonateMissile(PartModule missile)
        {
            if (missile == null || missileDetonate == null) return;
            try { missileDetonate.Invoke(missile, null); }
            catch (Exception e) { WarnOnce("detonate", "BDArmory missile detonation failed: " + (e.InnerException ?? e).Message); }
        }

        /// <summary>The target the craft's weapon manager is engaging (guard mode), or null.</summary>
        public static Vessel GuardTarget(Vessel v)
        {
            if (v == null || wmType == null || wmCurrentTarget == null) return null;
            foreach (Part p in v.parts)
                foreach (PartModule m in p.Modules)
                {
                    if (!wmType.IsInstanceOfType(m)) continue;
                    object info = wmCurrentTarget.GetValue(m);
                    if (info == null || (info is UnityEngine.Object o && o == null)) continue;
                    if (info.GetType().GetProperty("Vessel")?.GetValue(info, null) is Vessel t && t != null) return t;
                }
            return null;
        }

        /// <summary>Whether a BDArmory optical cloak is active on the craft.</summary>
        public static bool Cloaked(Vessel v)
        {
            if (v == null || cloakType == null || cloakEnabled == null) return false;
            foreach (Part p in v.parts)
                foreach (PartModule m in p.Modules)
                    if (cloakType.IsInstanceOfType(m) && (bool)cloakEnabled.GetValue(m) && (cloakOptical == null || (bool)cloakOptical.GetValue(m)))
                        return true;
            return false;
        }

        // ---- Bullets ----

        public static bool BulletsReadable => bulletManagerInstance != null && activeBulletsField != null && bulletInfoField != null && bulletInfoName != null && bulletTimeAlive != null;

        /// <summary>BDArmory's live bullets (PooledBullet components), or null.</summary>
        public static IEnumerable ActiveBullets()
        {
            if (!BulletsReadable) return null;
            object manager = bulletManagerInstance.GetValue(null);
            if (manager == null || (manager is UnityEngine.Object o && o == null)) return null;
            return activeBulletsField.GetValue(manager) as IEnumerable;
        }

        /// <summary>The bullet type's name (the BULLET node's name) and how long it has flown.</summary>
        public static bool ReadBullet(object pooledBullet, out string name, out float timeAlive)
        {
            name = null; timeAlive = 0;
            if (pooledBullet == null) return false;
            object info = bulletInfoField.GetValue(pooledBullet);
            if (info == null) return false;
            name = bulletInfoName.GetValue(info, null) as string;
            timeAlive = (float)bulletTimeAlive.GetValue(pooledBullet);
            return name != null;
        }

        /// <summary>
        /// Where a bullet left its gun: the source weapon's fire transform nearest the bullet (several barrels), or, if
        /// that can't be read, the bullet's position wound back along its velocity by its flight time.
        /// </summary>
        public static bool BulletMuzzle(object pooledBullet, Vector3 bulletPos, float timeAlive, out Vector3 muzzle)
        {
            muzzle = default;
            if (pooledBullet == null) return false;
            var part = bulletSourceWeapon?.GetValue(pooledBullet) as Part;
            if (part != null && weaponFireTransforms != null)
            {
                for (int i = 0; i < part.Modules.Count; i++)
                {
                    if (!ModuleWeaponType.IsInstanceOfType(part.Modules[i])) continue;
                    if (!(weaponFireTransforms.GetValue(part.Modules[i]) is Transform[] tfs)) continue;
                    float best = float.MaxValue;
                    foreach (Transform tf in tfs)
                    {
                        if (tf == null) continue;
                        float d = (tf.position - bulletPos).sqrMagnitude;
                        if (d < best) { best = d; muzzle = tf.position; }
                    }
                    if (best < float.MaxValue) return true;
                }
            }
            if (bulletVelocity != null && timeAlive > 0)
            {
                muzzle = bulletPos - (Vector3)bulletVelocity.GetValue(pooledBullet) * timeAlive;
                return true;
            }
            return false;
        }

        // ---- Warheads ----

        static readonly Dictionary<Type, FieldInfo> detonatedFields = new Dictionary<Type, FieldInfo>();
        static MethodInfo createExplosion;
        static ParameterInfo[] createExplosionParams;

        /// <summary>
        /// Whether a BDArmory warhead on the part (BDExplosivePart, BDModuleNuke, any BDWarheadBase) has gone off: their
        /// hasDetonated flag is set as they detonate, before the part is destroyed. (BDArmory also sets it on scene
        /// changes, so check those separately.)
        /// </summary>
        public static bool Detonated(Part part)
        {
            if (part == null) return false;
            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule m = part.Modules[i];
                if (m == null) continue;
                Type t = m.GetType();
                if (!detonatedFields.TryGetValue(t, out FieldInfo f))
                {
                    f = null;
                    if (t.Assembly.GetName().Name == "BDArmory")
                        for (Type b = t; b != null && f == null; b = b.BaseType)
                            f = b.GetField("hasDetonated", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (f != null && f.FieldType != typeof(bool)) f = null;
                    detonatedFields[t] = f;
                }
                if (f != null && (bool)f.GetValue(m)) return true;
            }
            return false;
        }

        /// <summary>
        /// A BDArmory explosion (damage, sound and its visuals, which VFX Reconfigured replaces) of tntKg at position.
        /// Calls ExplosionFx.CreateExplosion with its defaults for everything not given.
        /// </summary>
        public static bool CreateExplosion(Vector3 position, float tntKg, string modelPath, string soundPath, string sourceVessel, string weapon)
        {
            if (explosionFxType == null) return false;
            try
            {
                if (createExplosion == null)
                {
                    foreach (MethodInfo m in explosionFxType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (m.Name != "CreateExplosion") continue;
                        ParameterInfo[] p = m.GetParameters();
                        if (p.Length >= 5 && p[0].ParameterType == typeof(Vector3) && p[1].ParameterType == typeof(float) && p[4].ParameterType.IsEnum)
                        { createExplosion = m; createExplosionParams = p; break; }
                    }
                    if (createExplosion == null) { WarnOnce("createExplosion", "BDArmory's ExplosionFx.CreateExplosion not found: field bombs make no blast."); return false; }
                }
                var args = new object[createExplosionParams.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    ParameterInfo p = createExplosionParams[i];
                    switch (p.Name)
                    {
                        case "position": args[i] = position; break;
                        case "tntMassEquivalent": args[i] = tntKg; break;
                        case "explModelPath": args[i] = modelPath; break;
                        case "soundPath": args[i] = soundPath; break;
                        case "explosionSourceType": args[i] = Enum.Parse(p.ParameterType, "Missile"); break;
                        case "sourceVesselName": args[i] = sourceVessel; break;
                        case "sourceWeaponName": args[i] = weapon; break;
                        default:
                            args[i] = p.HasDefaultValue ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null);
                            if (args[i] == null && p.ParameterType.IsValueType) args[i] = Activator.CreateInstance(p.ParameterType);
                            // (an enum's default can come back as its underlying number)
                            if (p.ParameterType.IsEnum && args[i] != null && args[i].GetType() != p.ParameterType) args[i] = Enum.ToObject(p.ParameterType, args[i]);
                            break;
                    }
                }
                createExplosion.Invoke(null, args);
                return true;
            }
            catch (Exception e)
            {
                WarnOnce("createExplosionCall", "BDArmory explosion failed: " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        // ---- Weapons ----

        /// <summary>A weapon's impulse (BDArmory impulse lasers push by it; negative pulls).</summary>
        public static float GetImpulse(PartModule weapon) => weapon != null && weaponImpulse != null ? (float)weaponImpulse.GetValue(weapon) : 0;
        public static void SetImpulse(PartModule weapon, float value) { if (weapon != null) weaponImpulse?.SetValue(weapon, value); }

        /// <summary>The ends of a laser's beam while it's drawn (the first beam), or false.</summary>
        public static bool LaserBeam(PartModule weapon, out Vector3 from, out Vector3 to)
        {
            from = to = default;
            if (weapon == null || weaponLasers == null) return false;
            if (!(weaponLasers.GetValue(weapon) is LineRenderer[] lrs) || lrs.Length == 0) return false;
            LineRenderer lr = lrs[0];
            if (lr == null || !lr.enabled || lr.positionCount < 2) return false;
            from = lr.GetPosition(0); to = lr.GetPosition(lr.positionCount - 1);
            if (!lr.useWorldSpace) { from = lr.transform.TransformPoint(from); to = lr.transform.TransformPoint(to); }
            return true;
        }

        public static PartModule FindWeapon(Part part)
        {
            if (ModuleWeaponType == null || part == null) return null;
            for (int i = 0; i < part.Modules.Count; i++)
                if (ModuleWeaponType.IsInstanceOfType(part.Modules[i])) return part.Modules[i];
            return null;
        }

        public static bool IsCharging(PartModule weapon) =>
            weapon != null && weaponIsCharging != null && (bool)weaponIsCharging.GetValue(weapon);

        public static float ChargeTime(PartModule weapon) =>
            weapon != null && weaponChargeTime != null ? (float)weaponChargeTime.GetValue(weapon) : 0;

        /// <summary>The weapon's ammo resource and how much one shot uses, or false.</summary>
        public static bool AmmoUse(PartModule weapon, out string resource, out float perShot)
        {
            resource = null; perShot = 0;
            if (weapon == null || weaponAmmoName == null || weaponAmmoPerShot == null) return false;
            resource = weaponAmmoName.GetValue(weapon) as string;
            perShot = (float)weaponAmmoPerShot.GetValue(weapon);
            return !string.IsNullOrEmpty(resource);
        }
    }
}
