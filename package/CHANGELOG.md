# Changelog

## 0.4.0

Needs VFX Reconfigured 0.10.0 or newer (its shaders changed: rebuild it with Unity).

- **Tachyon Bomb**:
  - Time slows smoothly to a stop over about a second and speeds back up over two, instead of stopping and starting
    at once.
  - Everything on a caught craft slows and stops with it: sounds, engine plumes (stock particles and Waterfall),
    landing gear and other animations, kerbals.
  - Its part modules are switched off while time is stopped, so engines and converters use no fuel or power, and
    weapons and guidance wait.
  - BDArmory explosions inside the dome stop while time is stopped, damage included, and carry on afterwards. VFX
    Reconfigured's explosion visuals slow and stop with the field.
  - The bomb's own flash slows to a standstill in the middle, round its frozen pieces. Both play backwards as time
    returns.
  - The dome tints everything inside it green and has a crisp edge and a faint lattice, visible from inside (VFX
    Reconfigured's new tint sphere).
  - Freeze time can be set in the part's window, from 5 to 60 seconds.
- **Gravity and Warp Bombs**: the inward-sweeping rings keep a steady width.
- **Gauss Cannon and Turret**: the charge sound is loud from the first moment, rises in pitch more slowly and
  steadily, and crackles all the way through.

## 0.3.0

Needs VFX Reconfigured 0.9.0 or newer.

- **Tachyon Bomb**:
  - Goes off when it hits the ground, even dropped from a craft on the ground. It arms after 0.2 s instead of 1 s
    and 10 m. Before, it broke unarmed with no effect.
  - Does no damage: no stock explosion, and next to no charge.
  - Frozen craft and debris really stay still. They're held in place instead of having their speed reset each step,
    which let them drift down.
  - Kerbals on EVA can't move while frozen, and every kerbal's animations stop (EVA and in cockpits).
  - Green glints shimmer all through the dome, and more finely round the camera when it's inside.
- **Gravity Bomb**: no black hole, only a speck of a core, and gentler lensing. The closing blast is now a pulse: a
  ring of bent air and a pale shell racing outward, with a white flash, instead of a fireball.
- **Gravity and Warp Bombs**: air rushes in towards the centre, as wisps and rings of bent air sweeping inward (a
  shockwave played backwards).
- **Antimatter Bomb**: a pink flash and a much thinner cloud (VFX Reconfigured 0.9.0).
- **Smoother detonations**:
  - Field sounds are made when a bomb loads, not as it goes off.
  - The big flash lights are gone when VFX Reconfigured draws the flashes. A light hundreds of metres wide made
    everything around it draw again, which caused a hitch of up to a second as the bombs went off.

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
