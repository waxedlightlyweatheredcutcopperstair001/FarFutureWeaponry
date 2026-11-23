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
        static FieldInfo poolList;                       // BDArmory.Utils.ObjectPool.pool (List<GameObject>)

        // Bullets: BDArmory.Bullets.PooledBulletManager.Instance.activeBullets (HashSet<PooledBullet>).
        static FieldInfo bulletManagerInstance, activeBulletsField;
        static FieldInfo bulletInfoField, bulletTimeAlive;    // PooledBullet.bullet (BulletInfo), .timeAlive
        static FieldInfo bulletSourceWeapon, bulletVelocity;  // PooledBullet.sourceWeapon (Part), .currentVelocity
        static PropertyInfo bulletInfoName;                   // BulletInfo.name

        // Weapons: BDArmory.Weapons.ModuleWeapon.isCharging (private bool), .ChargeTime.
        public static Type ModuleWeaponType { get; private set; }
        static FieldInfo weaponIsCharging, weaponChargeTime, weaponAmmoName, weaponAmmoPerShot, weaponFireTransforms;

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

        // ---- Weapons ----

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
