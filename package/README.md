# Far Future Weaponry 0.6.0

Experimental weapons for Kerbal Space Program 1.12, built on BDArmory. Works with **BDArmory Lite** and **BDArmory**.

This is an early version: every weapon from the design is in (guns, bombs, missiles), plus the Tachyon-be-gone. The other countermeasures come next. The models are placeholders, retinted from BDArmory, BDArmory
Extended and North Kerbin Dynamics parts.

## Requirements

- BDArmory Lite, or BDArmory (Continued)
- VFX Reconfigured 0.11.0 or newer
- Physics Range Extender
- ModuleManager

Optional: Far Future Technologies (its antimatter tanks feed the Antimatter-ial Rifle).

## Parts

All of them are in the Utility category and in BDArmory's own part filter.

| Part | What it does |
|---|---|
| **Antimatter-ial Rifle** | 5-round magazine of antimatter rounds. Each one annihilates on impact with about 3 kg of TNT's force and a bright pink flash. Fed from its own small containment trap (about 28 rounds) and from any antimatter on the craft. 25 EC per shot. |
| **Neutron Gun** | Thin electric-blue pulse beam. Every pulse sets off a small explosion with a bright flash where it lands. 200 EC/s while firing. |
| **Gauss Cannon** | Charges for 2 s (sparks crackle along the barrel), then fires a 6 kg slug at 4.5 km/s that punches through several parts. No splash. Leaves a blue beam in the air that slowly fades. 800 EC per shot. |
| **Gauss Cannon Turret** | The same gun on a fast 360° turret. |
| **Point-Defense Laser Array** | Shoots down incoming missiles and bombs within 2.5 km with rapid magenta heat pulses. It also engages craft when nothing is incoming. 160 EC/s while firing. |
| **Antimatter Containment Trap** | Holds 0.2 units of antimatter (about 570 rifle rounds). |

| **Tachyon Gun** | Rapid thin green pulses. Each hit takes 1 m/s off the target's speed, does a little damage and rusts the part it hits, with a burst of green sparkles. Rust spreads in patches and is saved with the craft. Parts it destroys break apart rather than explode (unless they hold something like antimatter). No effect on craft with an active Tachyon-be-gone. |
| **Gravitolium Repulsor Beam** | A dark violet beam of bent space that pushes (or pulls: switch it in the part's window) whatever it holds, hard. Barely any damage. |

### Missiles

FFW's own trackers go for your KSP target (right-click a craft, Set as Target), or the weapon manager's target in guard mode. Flares, chaff and jamming don't fool them. The weapon manager lists them as "Unguided" because BDArmory doesn't know these trackers.

| Missile | Tracking | What it does |
|---|---|---|
| **Long Range Nuclear Cruise Missile** | GPS | Flies low to a GPS target up to 100 km away on a nuclear ramjet (works only in air), then 1 megaton. |
| **Quantum Phase-Shift Missile** | Optical (20 km; fooled by cloaks) | 400 m out it vanishes in a flash of bent, colour-torn space, reappears inside the target's hull and goes off there. |
| **Kessler Swarm Pod** | Radar (lock the target first) | 1.5 km out it splits into 20 tungsten darts that shred solar panels, radiators, wings and engines. |
| **Telemetry Leech Missile** | CommNet (craft with a CommNet link) | An explosion, then 30 s without antennas and 2000 ElectricCharge drained. |
| **Chronos Rust-Dart** | Optical | Little damage; the part it hits rusts and its craft slows. |
| **Kraken Missile** | Consciousness (crewed craft within 5 km) | Next to no blast; every Kerbal aboard the craft it hits dies, as a thin red wave crawls over its hull. |

### Bombs

Drop them like any BDArmory bomb.

| Bomb | What it does |
|---|---|
| **Gravity Bomb** | A collapsing bubble of bent space pulls everything within 300 m towards it at 6 g for 3 s, with air rushing in. Then a white flash and a pulse (a 250 kg TNT blast) hit whatever it gathered. |
| **Warp Bomb** | A black hole for 10 s, with air rushing in. Everything within 1 km is pulled in at 20 g, and anything that gets within 250 m is destroyed. |
| **Tachyon Bomb** | A green dome (everything inside tinted green) stops time for every craft within 500 m, yours included. They slow to a stop: their engines, plumes, sounds, landing gear and kerbals all freeze, and nothing burns fuel. Explosions inside stop too, damage included. Set how long it lasts in the part's window, from 5 to 60 s. Then time speeds back up, and each craft carries on as it was. The bomb's flash and pieces hang frozen in the middle, play backwards at the end, and the bomb drops as a harmless dud. No damage. |
| **Antimatter Bomb** | About 500 megatons, drawn as an antimatter blast. Anything loaded within about 40 km is destroyed. |
| **Mushroom Bomb** | About 2 megatons, with a green flash, green flames and a dark green cloud. Danger radius about 9 km. |

### Countermeasures

| Part | What it does |
|---|---|
| **Tachyon-be-gone** | While it's on, the craft is immune to tachyon weapons: the Tachyon Bomb doesn't freeze it. |

## Antimatter

Antimatter uses Far Future Technologies' units: 1 unit releases 36 GJ, about 8.6 t of TNT.
Anything that holds antimatter (the rifle, the trap, FFT's tanks) needs ElectricCharge for its magnetic containment
all the time, even when idle: 0.5 EC/s for the rifle, 2 EC/s for the trap.
**If the power runs out**, the containment holds for 2–3 seconds. Then the antimatter starts annihilating against
the walls and heats the part until it explodes. Keep batteries charged, or empty the trap.

Without Far Future Technologies (or the Community Resource Pack), this mod defines the Antimatter resource itself.

## Licence

Code: MIT. Configs and assets: CC BY-SA 4.0. The placeholder models come from BDArmory (CC BY-SA 2.0), BDArmory
Extended (CC BY-SA 4.0) and North Kerbin Dynamics Renewed (CC BY-SA 4.0); see `LICENSE` and `Licences/`.
