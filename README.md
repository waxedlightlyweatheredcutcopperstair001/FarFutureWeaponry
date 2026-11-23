# Far Future Weaponry

Experimental weapons and armour for KSP 1.12.5, built on BDArmory. One package works with **BDArmory Lite** and full
**BDArmory**. Requires VFX Reconfigured 0.11.0+, Physics Range Extender, ModuleManager and Harmony (HarmonyKSP; bound by reflection, so not needed to build). The player README and changelog are
in `package/`.

## Layout

| Path | What |
|---|---|
| `src/Core/` | Pure logic, no Unity or KSP (unit-tested): antimatter energy, containment, effect curves, trail points, colours, charge sound, field timelines and pull (`Fields`), shatter grouping, field sounds, tracking and lead (`Tracking`), rust, darts, slowing |
| `src/Ksp/` | KSP glue: `Bda` (BDArmory by reflection), `Vfxr` (VFX Reconfigured's EffectsApi by reflection), `Startup` (dependency checks), `ImpactFlashes`, `BulletTrails`, `ModuleFFWChargeSparks`, `ModuleFFWContainment`, `ModuleFFWBomb` + `ModuleFFWTachyonShield`, `FieldManager` (bomb fields and warhead effects: physics, looks, sounds), `TimeStop`, `ShatterFx`, `Targets` + `ModuleFFWSeeker` (FFW tracking), `ModuleFFWPhase`, `ModuleFFWRamjet`, `Darts` (`ModuleFFWDarts`, `DartManager`), `TachyonHit` + `ModuleFFWRust`, `ModuleFFWRepulsor`, `RibbonTrail` + `ModuleFFWTrail`, `Overlays` (`FfwShaders` bundle loader, `PartOverlay`: a second material drawn over a part's meshes), `HarmonyBridge`, `ForceField`, `Cloak`, `Holograms`, `Countermeasures` (scrambler, hop drive, screen glitch), `Visuals` (procedural textures/meshes/materials) |
| `configs/` | Shipped configs: `Parts/<Part>/part.cfg`, `Bullets.cfg`, `Resources.cfg` (fallback Antimatter definition), `Effects.cfg` (flash, hit-effect and trail definitions), `Rust.cfg` (ModuleFFWRust on every part) |
| `assets/` | Borrowed placeholder models, textures, sounds and their licences, laid out as in the mod folder. Made by `tools/import_assets.py`; committed |
| `package/` | Player README and CHANGELOG (shipped) |
| `shaders/` | Unity project for the plugin's shaders (`RustOverlay`, `ContourWave`, `PhaseLens`, `ShieldOverlay`, `CloakOverlay`, `Hologram`; `Editor/BuildBundle.cs`) |
| `tests/` | Console test program for `src/Core` |
| `tools/` | `build.py` (plugin), `package.py` (assemble + validate + zip, `--deploy`), `validate.py` (static checks), `make.py` (all of it), `release.py` (GitHub release), `import_assets.py` + `png.py` (borrow and tint assets), `localconfig.py` |

## Build

`config.local.json` (copy `config.example.json`) sets `kspRoot`, a KSP folder holding `KSP_x64_Data/Managed` (or set
the `KSP_ROOT` environment variable). Needs Python 3 and the .NET SDK 8+; on Windows also Unity 2019.4.18f1
(`unityEditor` in `config.local.json`) for the shader bundle (`shaders/`, `tools/shaders.py`: rust, the contour wave,
the phase lens). `make.py` runs it on Windows only; a Linux package has no bundle and the plugin falls back on plainer
effects. Releases need the bundle (`release.py` checks).

```
python3 tools/make.py              # plugin + tests + package -> release/FarFutureWeaponry-<version>.zip
python3 tools/make.py --deploy     # ...and install into kspRoot's GameData (KSP must be closed)
```

Windows: `py tools\make.py --deploy`. Single steps: `python3 tools/build.py`;
`DOTNET_ROLL_FORWARD=Major dotnet run --project tests/FarFutureWeaponry.Tests.csproj -c Release`; `python3 tools/package.py`.
`package.py` runs `validate.py` and won't zip a package with problems.

## How it hooks into BDArmory

- **No compile-time reference to BDArmory.** BDArmory Lite and BDArmory are both the assembly `BDArmory`, but their
  code differs. `Bda.cs` reads the few members it needs by reflection. If one is missing, that feature turns off and
  the log says why (`[FFW]` lines). The members were checked against BDArmory Lite 2.1.5 and BDArmory master (Aug 2026).
- **Weapons are plain BDArmory configs** (`ModuleWeapon`, `ModuleTurret`, `BULLET`). Every asset path points at
  this mod's own copy (`FarFutureWeaponry/...`), because BDArmory and BDArmory Lite keep theirs under different
  folders. Paths left at BDArmory's defaults are fine: each install's defaults point at its own folder.
- **Impact flashes**: BDArmory has no hit events. Its explosions come from pools keyed by the weapon's
  `explModelPath`, so each FFW weapon gets its own copy of the explosion model (`FX/Hits/`). `ImpactFlashes` watches
  those pools and draws a flash and a light when one goes off (`FFW_IMPACT_FLASH` in `Effects.cfg`).
- **Trails**: `BulletTrails` reads BDArmory's live bullets (`PooledBulletManager.activeBullets`) and leaves a
  fading ribbon behind the bullet types named in `FFW_BULLET_TRAIL`. Points are kept in the main body's frame, so
  floating-origin shifts don't move them.
- **Charge sparks**: `ModuleFFWChargeSparks` follows `ModuleWeapon.isCharging`.
- **Bombs**: plain BDArmory bombs (`MissileLauncher` + `BDExplosivePart`/`BDModuleNuke`) with `ModuleFFWBomb`. It tags
  the part with a VFX Reconfigured look (`EffectsApi.SetPartLook`: "none" for field bombs, whose warhead is only a
  trigger; "antimatter"; "mushroom"). It fires its field only if the warhead's `hasDetonated` is set when the part dies
  (not when an unarmed bomb is shot apart, nor on scene changes).
- **Fields** (`FieldManager`): the pull is a constant acceleration on every loaded part inside the radius. The black
  hole `explode()`s parts and demolishes buildings inside its event horizon. The tachyon freeze makes every part's
  rigidbody kinematic and stops kerbals' EVA control and animations. It then gives back the velocity each part had
  relative to the surface (Krakensbane and planet rotation accounted for). Lenses and flashes go through
  `EffectsApi.LensCore`/`Glow` each frame. Sounds are made when a bomb loads (`FieldManager.Prepare`), and lights are
  only used without VFX Reconfigured: wide lights caused hitches at detonation.
- **Antimatter** uses Far Future Technologies' units (36 GJ/unit) and the same containment model (a flat EC/s cost;
  without it, annihilation heats the part). `Resources.cfg` defines Antimatter only when the Community Resource Pack
  doesn't.

## Upstream assets

From the untracked inputs next to BDArmory Lite (`../../BDArmory Lite/`): BDArmory 1.13.1, BDArmory Extended 1.5.3,
North Kerbin Dynamics Renewed 0.1.1 (all CC BY-SA). `tools/import_assets.py` lists every borrowed file. Re-run it
only to change that list. `BDArmory-upstream/` and `FFT-upstream/` next to this repo are shallow clones of BDArmory
and Far Future Technologies, for reference.

## Licence

Code MIT; configs and assets CC BY-SA 4.0 (see `LICENSE`).
