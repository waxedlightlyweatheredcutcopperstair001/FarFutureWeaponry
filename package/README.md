# Far Future Weaponry 0.1.1

Experimental weapons for Kerbal Space Program 1.12, built on BDArmory. Works with **BDArmory Lite** and **BDArmory**.

This is an early version: the first five weapons. The models are placeholders, retinted from BDArmory, BDArmory
Extended and North Kerbin Dynamics parts.

## Requirements

- BDArmory Lite, or BDArmory (Continued)
- VFX Reconfigured
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
