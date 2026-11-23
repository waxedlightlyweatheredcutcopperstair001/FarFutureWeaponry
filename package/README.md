# Far Future Weaponry 0.3.0

Experimental weapons for Kerbal Space Program 1.12, built on BDArmory. Works with **BDArmory Lite** and **BDArmory**.

This is an early version: the first five weapons, five bombs and the Tachyon-be-gone. The models are placeholders, retinted from BDArmory, BDArmory
Extended and North Kerbin Dynamics parts.

## Requirements

- BDArmory Lite, or BDArmory (Continued)
- VFX Reconfigured 0.9.0 or newer
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

### Bombs

Drop them like any BDArmory bomb.

| Bomb | What it does |
|---|---|
| **Gravity Bomb** | A collapsing bubble of bent space pulls everything within 300 m towards it at 6 g for 3 s, with air rushing in. Then a white flash and a pulse (a 250 kg TNT blast) hit whatever it gathered. |
| **Warp Bomb** | A black hole for 10 s, with air rushing in. Everything within 1 km is pulled in at 20 g, and anything that gets within 250 m is destroyed. |
| **Tachyon Bomb** | A shimmering green sphere freezes every craft within 500 m, yours included, for 10 s: kerbals can't move and stop still. Then each craft gets its speed back. No damage. The bomb shatters, its pieces hang in the field, then they fly back together and the bomb drops as a harmless dud (a prop that disappears after two minutes). |
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
