# Changelog

## 0.11.2

- **Kessler Swarm Pod**: splits 50 m from its target (was 500 m), so the dart cone is only a few metres wide when it
  arrives instead of 35 m, and fires 500 darts (was 100). If it's going to pass wider than 50 m, it splits at its
  closest approach instead of flying on. Only every fifth dart draws a trail, and stray darts vanish after 3 s, so
  the extra darts cost next to nothing.

## 0.11.1

- **Kraken Missile**: the glass dome is a darker red and less see-through, so it stands out; the flesh's glow is
  much stronger (brighter, wider veins with a soft halo, and a faint glow over the whole lump), easy to see from a
  distance, and stronger still once launched.

## 0.11.0

- **Kraken Missile**: its own model, replacing the borrowed placeholder. An enlarged Hellfire (2.2 m, 0.25 m across)
  in matte black with red bands, red fin tips and a red ring under a clear, faintly red glass nose dome. Inside the
  dome, a lump of red flesh on a bundle of wires ending in electrodes. It throbs with a slow heartbeat and its veins
  glow, faster and brighter once launched. **Craft already carrying a Kraken Missile need it re-attached** (its size
  and attach point changed).
- Every part's maker is now Bobrigh Weaponry Inc., and every quote in the descriptions is signed by Cave Kerman,
  director of science.

## 0.10.6

- **Cloaking Device**: fixed the darkened scene, white-outs and smearing (seen with Deferred and EVE). In flight, KSP
  hangs the flight camera under the craft's root part, and Deferred and EVE hang helpers on the camera; the cloak hid
  everything under the parts, those helpers included. It now leaves anything on a camera alone. The Phase-Shift
  Missile's vanishing act and the tachyon time-stop had the same blind spot and are fixed too.
- The cloak's log now lists only what it hides beyond the parts' own meshes (effects, interiors).

## 0.10.5

- Cloaking Device: logs every renderer it hides (in KSP.log, `[FFW] cloak hides ...`), to track down the rendering
  glitch some installs show while cloaked.

## 0.10.4

- **Cloaking Device**: fixed the darkened scene, white-outs and vanishing ground seen from most angles. The cloak hid
  the hull but left the cockpits' interiors (IVAs, which stock KSP and FreeIva draw in the flight view) and their
  invisible depth masks in place. With the hull gone, those masks blanked out the scene behind them and, with
  Deferred, upset the lighting. Interiors now vanish with their parts (unless you're looking from inside, in IVA).
- Cloaking Device: stopped an error repeating every frame after switching it (KSP's cached list of part renderers
  still held the cloak's removed overlays). Overlays now let KSP re-gather that list when they're removed.

## 0.10.3

- **Cloaking Device**, force field, rust, Kraken wave and holograms: the overlay shaders could produce invalid pixel
  values (NaN) on meshes without proper normals. With HDR post-processing (TUFX bloom, auto-exposure, anti-aliasing)
  one such pixel darkened, blew out or smeared the whole screen. The shaders now guard against it, and overlays skip
  meshes that have no normals. **Needs the rebuilt shader bundle.**
- Cloaking Device: a "Cloak outline" switch in its part window. Off, the parts still vanish as the ripple passes but
  no outline is drawn.

## 0.10.2

- **Cloaking Device**:
  - No more darkened ground, blinding flash or vanishing scenery when it switches on (seen with Deferred,
    Scatterer and other rendering mods). The cloak no longer copies the screen to bend the view behind the craft;
    what's left is the faint blue outline and the ripple. BDArmory's own cloak is also kept from turning the part
    materials see-through, since the craft is hidden anyway. **Needs the rebuilt shader bundle.**
  - Engine plumes (Waterfall and stock) and other effects vanish the moment it starts cloaking and come back only once
    it has fully uncloaked. They get no outline, so the outline no longer reaches far past where the plumes end.
- Force field, rust, Kraken wave and holograms: plumes and other effects no longer get the overlay or a hologram copy.

## 0.10.1

- **Warp Bomb** model, simpler and more in keeping with KSP:
  - Plain panels only: the busy patchwork panels are gone.
  - The small blocks and towers are gone. Instead, some of the larger panels stand slightly proud of the hull,
    exactly where the texture draws them.
  - The glowing ring is one clean, unbroken strip (no thin dark lines across it).

## 0.10.0

The first of the mod's own models.

- **Warp Bomb**: a new 1.25 m model replaces the borrowed placeholder: a sphere of grey plating in latitude bands,
  covered in blocks and towers, with a dark trench round its equator and a lug on top for the rail. The trench
  glows a pulsing blue once the bomb is dropped and armed. **Craft already carrying a Warp Bomb need it re-attached**
  (its attach point moved).

## 0.9.1

- **Hologram Generator**:
  - No longer switches itself on when the craft loads: only its button (or action group) projects the holograms.
  - No more flares and chaff: the part's dispensers are gone.
  - The holograms now look like the craft: its own colours, tinted a little blue, semi-transparent, with a blue rim,
    faint scanlines and the occasional glitch (needs the rebuilt shader bundle; without it, a plainer see-through copy).
  - They stay fully visible while the craft is cloaked.
- **Blue Rinse**: a solid electric-blue sphere instead of the gridded one, less see-through. It keeps spreading past
  the 1 km kill radius and fades out over about a second.
- **Quantum Phase-Shift Missile**: vanishes 100 m from the target (was 400 m) and reappears 0.5 s later (was 1.5 s).
- **Infinite electricity cheat**: the hologram generator, signal scrambler and micro-hop drive no longer refuse to
  work for lack of charge with it on. (Everything else already respected it.)

## 0.9.0

New parts from the updated design, and one extra.

- **Blue Rinse** (bomb): an electric-blue sphere spreads to 1 km and kills every Kerbal it reaches. No other damage.
- **Seismic Charge** (bomb): a flat electric-blue ring races out to 1 km, level with the ground, with a deep ringing
  "bwaaang". Everything it passes through takes heavy damage; anything above or below it is untouched.
- **Tracking sensors**: the Optical Tracking Camera, CommNet Signal Tracker and Consciousness Field Sensor. FFW
  missiles now need the matching sensor on the craft that fires them to lock on. The sensors show what they see in
  their part window, and the camera turns to follow the target.
- **Cloaking Device**: each part vanishes (or reappears) as the ripple passes over it, instead of the whole craft
  staying visible until the ripple ends. Needs the rebuilt shader bundle (Windows build) for the full effect.
- **Signal Scrambler**: your own interface glitches too: the altimeter, navball, staging list and toolbar jolt,
  stretch and flicker for a moment.
- **Long Range Nuclear Cruise Missile**: leaves a faint glowing trail.
- New descriptions for the Phase-Shift, Telemetry Leech, Micro-Hop Drive and Point-Defense Laser.

## 0.8.1

- Quantum Phase-Shift Missile: its trail vanishes with it, instead of staying behind and moving about while it's gone.

## 0.8.0

The countermeasures: every part from the design is now in. Needs Harmony (HarmonyKSP) for the force field and the
cloak's radar hiding.

- **Cloaking Device**:
  - Light ripples over the craft and it all but vanishes, plumes too, leaving a faint blue outline and a shimmer.
  - Fools FFW optical tracking, BDArmory's heat and visual detection (through BDArmory's own cloak), and
    anti-radiation missiles (no radar pings).
- **Force Field Generator**:
  - Blocks every kind of BDArmory damage, paid for in ElectricCharge in proportion to it.
  - A shimmer as it comes up, then an electric-blue outline over the hull, and blue flashes where hits land.
  - Tachyon weapons and the Quantum Phase-Shift Missile get through.
- **Hologram Generator**:
  - Six translucent, glitching copies of the craft fly in formation round it.
  - FFW optical seekers will likely chase one of them.
  - Flares and chaff come from the holograms for BDArmory's seekers.
- **Signal Scrambler**: radar and CommNet off for craft nearby, remote craft can't be controlled, and you're hidden
  from CommNet tracking. The screen glitches.
- **Alcubierre Micro-Hop Drive**: a 500 m jump along your way, with space imploding at both ends. It breaks every
  lock: FFW trackers, BDArmory radars and missiles.

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
