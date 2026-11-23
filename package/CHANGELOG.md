# Changelog

## 0.2.0

Five bombs and a countermeasure, on placeholder models. Needs VFX Reconfigured 0.8.0 or newer (its effects API draws
the lenses, flashes and weapon looks).

- **Gravity Bomb**: pulls everything within 300 m at 6 g for 3 s inside a shrinking lens, then a white flash and a
  250 kg TNT blast.
- **Warp Bomb**: a black hole for 10 s (VFX Reconfigured's gravitational lens, 1 km): 20 g pull within 1 km,
  everything within 250 m destroyed, with flashes and ripples as it forms and collapses.
- **Tachyon Bomb**: every craft within 500 m is frozen for 10 s inside a green sphere, then released at its own
  speed. The bomb shatters, hangs in its own field, reassembles and drops as a dud.
- **Antimatter Bomb** (500 Mt) and **Mushroom Bomb** (2 Mt, green flames and smoke).
- **Tachyon-be-gone**: immunity to tachyon weapons.
- Sounds made in code for the fields: a deep rumble for the gravity and warp fields, falling and rising sweeps for the
  tachyon field.
- The main-menu check also warns when VFX Reconfigured is older than 0.8.0.

## 0.1.1

- Gauss Cannon and Turret: the blue beam now starts at the muzzle. Before, it started tens of metres out, where the
  slug was by the first frame.
- Gauss Cannon and Turret: a new charge-up sound lasts the whole 2 s charge: a rising whine, a hum and crackles that
  build up to the shot. The old clip was only half a second long.

## 0.1.0

First version: the five simplest weapons, on placeholder models.

- **Antimatter-ial Rifle**: antimatter rounds (about 3 kg TNT each) with a pink impact flash. Fed from its own
  containment trap and from any antimatter on the craft.
- **Neutron Gun**: thin blue pulse beam; each pulse explodes with a bright flash.
- **Gauss Cannon** and **Gauss Cannon Turret**: 2 s charge with sparks, a penetrating slug, and a fading blue beam.
- **Point-Defense Laser Array**: missile and bomb defence with magenta heat pulses, out to 2.5 km.
- **Antimatter Containment Trap**: 0.2 units of antimatter. Containment needs power, or the antimatter annihilates.
- Works with BDArmory Lite and BDArmory. Warns at the main menu if BDArmory, VFX Reconfigured, Physics Range
  Extender or ModuleManager is missing.
