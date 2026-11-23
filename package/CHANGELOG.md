# Changelog

## 0.7.1

- Kessler Swarm Pod: 100 darts instead of 20, in a tighter cone, aimed where the target will be when they arrive. Each
  dart goes through up to three parts (a third less damage each time) instead of stopping at the first.

## 0.7.0

- **Chronos Rust-Dart**:
  - Time on the craft it hits now runs at half speed for 10 s, like the Tachyon Bomb but slowed instead of stopped:
    its motion, sounds, plumes and animations. Then it speeds back up.
  - Rust creeps over the whole craft meanwhile, more of it than before.
  - A little less damage.
- **Tachyon Gun**:
  - Each hit slows time on the target by 10% for 2 s instead of taking 1 m/s. Hits while it's slowed extend that,
    and it eases back afterwards.
  - Each hit rusts the whole craft a little. Damage still goes only to the part hit.
- **Rust**: more opaque, with crisper patch edges.
- **Quantum Phase-Shift Missile**: stays gone for 1.5 s between vanishing and reappearing inside its target.
- **Kraken Missile**: even less blast (0.1 kg).
- **Kessler Swarm Pod**: splits 500 m from its target instead of 1.5 km.
- While a craft is slowed, it keeps the course it had when hit (like a craft frozen by the Tachyon Bomb).

## 0.6.0

Needs VFX Reconfigured 0.11.0 or newer. The mod now has its own shaders, built with Unity on Windows like VFX
Reconfigured's.

- **Tachyon Gun**:
  - Rust now shows, as patches that spread as it builds up (new rust shader). Each hit adds more than before.
  - Green sparkles where it hits.
  - Parts it destroys break apart instead of exploding, unless they hold a warhead or an exotic fuel like antimatter.
- **Quantum Phase-Shift Missile**: 400 m from its target it vanishes and reappears inside the hull, then goes off.
  Both ends show a flash of bent space with the colours torn apart (new phase lens shader).
- **Kraken Missile**: much less blast. Its red wave is now a thin layer of light crawling over the hit craft's own
  parts from where it struck (new contour wave shader).
- **Kessler Swarm Pod**: radar-guided instead of anti-radiation. Lock the target with your radar before firing.

## 0.5.0

Every weapon from the design is now in. Placeholder models as before.

- **Tracking systems**:
  - Optical: 20 km; ignores flares and chaff; fooled by cloaks.
  - CommNet: follows craft that have a CommNet link.
  - Consciousness: finds crewed craft within 5 km.
  - Missiles using them go for your KSP target, or the weapon manager's target in guard mode.
- **Missiles**: Long Range Nuclear Cruise Missile, Quantum Phase-Shift Missile, Kessler Swarm Pod, Telemetry Leech,
  Chronos Rust-Dart, Kraken Missile. Each has its own trail:
  - a flickering cyan trail (Phase-Shift);
  - orange dart streaks (Kessler);
  - a crackling cyan ion trail (Leech);
  - a razor-thin green trail (Chronos);
  - a long red trail (Kraken).
- **Tachyon Gun**: each hit slows the target by 1 m/s, damages it and rusts the part. Rust is saved with the craft.
- **Gravitolium Repulsor Beam**: pushes or pulls (switch in the part's window), with space bent along the beam.
- Tachyon-be-gone also protects against the Tachyon Gun and the Chronos Rust-Dart.

## 0.4.1

- Tachyon Bomb: the rising sweep plays while the bomb's pieces fly back together, ending as they snap into one,
  instead of as soon as time starts to return.
- Fire, smoke and flying wreckage inside the tachyon dome now stop with the rest (VFX Reconfigured 0.10.1).

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
