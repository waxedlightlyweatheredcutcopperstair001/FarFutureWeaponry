#!/usr/bin/env python3
"""Copy the borrowed placeholder models, textures and sounds into assets/ (committed), tinting some textures.

The placeholder parts reuse models from BDArmory 1.13.1 (CC-BY-SA 2.0), BDArmory Extended 1.5.3 (CC-BY-SA 4.0) and
North Kerbin Dynamics Renewed 0.1.1 (CC-BY-SA 4.0). They are copied, not referenced, because BDArmory keeps them under
GameData/BDArmory/ and BDArmory Lite under GameData/BDArmoryLite/: a copy works with both.

assets/ mirrors the packaged mod folder (GameData/FarFutureWeaponry/...). Re-run this script only to change what is
borrowed (edit MANIFEST); the results are committed so the package builds anywhere without the upstream folders.

Usage: python3 -I tools/import_assets.py [--inputs "<folder holding BDArmory-1.13.1, BDArmory-Extended-1.5.3 and
       North_Kerbin_Dynamic_Renewed-0.1.1>"]   (default: ../../BDArmory Lite, next to this repository's parent)
"""
import argparse, shutil, sys, wave
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import png

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'assets'

SOURCES = {
    'BDA': Path('BDArmory-1.13.1/BDArmory/Distribution/GameData/BDArmory'),
    'BDAX': Path('BDArmory-Extended-1.5.3/BDArmoryExtended'),
    'NKDR': Path('North_Kerbin_Dynamic_Renewed-0.1.1/gameData/NKDR'),
}

# Tints (placeholder look): each weapon's paint pulled towards its beam colour.
MAGENTA, BLUE, CYAN, VIOLET = '#c8408e', '#3f6fd8', '#3a9cff', '#7a46d8'
GREEN, PERIWINKLE, NAVY = '#3fbf5a', '#6c6ee0', '#2a3a70'
OLIVE, RED, ORANGE, TEAL, DARKVIOLET = '#7f8a3a', '#c83a3a', '#d8782a', '#2aa6b8', '#4a2a8a'

# (source, path in source, path in assets/, tint or None)
MANIFEST = [
    # Antimatter-ial rifle: BDArmory Extended's QF 6-pounder antitank gun (5-round magazine, reload animation).
    ('BDAX', 'Parts/FixedGuns/AntitankGun/Model.mu', 'Parts/AntimatterRifle/Model.mu', None),
    ('BDAX', 'Parts/FixedGuns/AntitankGun/Mollins.png', 'Parts/AntimatterRifle/Mollins.png', MAGENTA),
    ('BDAX', 'Parts/FixedGuns/AntitankGun/muzzle.png', 'Parts/AntimatterRifle/muzzle.png', None),
    ('BDAX', 'Parts/FixedGuns/AntitankGun/smoke.png', 'Parts/AntimatterRifle/smoke.png', None),
    ('BDAX', 'Parts/FixedGuns/AntitankGun/57mmShot.ogg', 'Parts/AntimatterRifle/shot.ogg', None),
    ('BDAX', 'Parts/FixedGuns/AntitankGun/reload.ogg', 'Parts/AntimatterRifle/reload.ogg', None),
    # Neutron gun: BDArmory Extended's medium combat laser.
    ('BDAX', 'Parts/Lasers/medLaser/Model.mu', 'Parts/NeutronGun/Model.mu', None),
    ('BDAX', 'Parts/Lasers/medLaser/laser_lg_laser_md_Diffuse.png', 'Parts/NeutronGun/laser_lg_laser_md_Diffuse.png', CYAN),
    ('BDAX', 'Parts/Lasers/medLaser/laser_lg_laser_md_Normal.png', 'Parts/NeutronGun/laser_lg_laser_md_Normal.png', None),
    ('BDAX', 'Parts/Lasers/medLaser/hflare.png', 'Parts/NeutronGun/hflare.png', None),
    ('BDAX', 'Parts/Lasers/medLaser/laserPulse.wav', 'Parts/NeutronGun/pulse.wav', None),
    # Gauss cannon: BDArmory Extended's railgun (Bare / Hidden variants).
    ('BDAX', 'Parts/FixedGuns/Railgun/Model.mu', 'Parts/GaussCannon/Model.mu', None),
    ('BDAX', 'Parts/FixedGuns/Railgun/railgun.png', 'Parts/GaussCannon/railgun.png', BLUE),
    ('BDAX', 'Parts/FixedGuns/Railgun/railgunCasing.png', 'Parts/GaussCannon/railgunCasing.png', BLUE),
    ('BDAX', 'Parts/FixedGuns/Railgun/muzzle.png', 'Parts/GaussCannon/muzzle.png', None),
    ('BDAX', 'Parts/FixedGuns/Railgun/smoke.png', 'Parts/GaussCannon/smoke.png', None),
    ('BDAX', 'Parts/FixedGuns/Railgun/spark.png', 'Parts/GaussCannon/spark.png', None),
    ('BDAX', 'Parts/FixedGuns/Railgun/bullet.png', 'Parts/GaussCannon/bullet.png', None),
    ('BDAX', 'Parts/FixedGuns/Railgun/railfire.ogg', 'Parts/GaussCannon/fire.ogg', None),
    # Gauss turret: NKDR's prototype railgun turret.
    ('NKDR', 'parts/Railgun/model.mu', 'Parts/GaussTurret/model.mu', None),
    ('NKDR', 'parts/Railgun/Railgun.png', 'Parts/GaussTurret/Railgun.png', BLUE),
    ('NKDR', 'parts/Railgun/Railgun Base.png', 'Parts/GaussTurret/Railgun Base.png', BLUE),
    ('NKDR', 'parts/Railgun/ExpTex2.png', 'Parts/GaussTurret/ExpTex2.png', None),
    # Point-defense laser array: BDArmory's airborne laser turret.
    ('BDA', 'Parts/ABL/model.mu', 'Parts/PointDefenseLaser/model.mu', None),
    ('BDA', 'Parts/ABL/texture.png', 'Parts/PointDefenseLaser/texture.png', MAGENTA),
    ('BDA', 'Parts/ABL/glasstest.png', 'Parts/PointDefenseLaser/glasstest.png', None),
    ('BDA', 'Parts/ABL/sounds/laser.ogg', 'Parts/PointDefenseLaser/laser.ogg', None),
    ('BDA', 'Parts/ABL/sounds/charge.ogg', 'Parts/PointDefenseLaser/charge.ogg', None),
    ('BDA', 'Parts/ABL/sounds/drain.ogg', 'Parts/PointDefenseLaser/drain.ogg', None),
    # Antimatter containment trap: BDArmory's ammo box.
    ('BDA', 'Parts/AmmoBox/model.mu', 'Parts/AntimatterTrap/model.mu', None),
    ('BDA', 'Parts/AmmoBox/texture.png', 'Parts/AntimatterTrap/texture.png', VIOLET),
    # Gravity bomb and tachyon bomb: BDArmory's Mk82.
    ('BDA', 'Parts/mk82Bomb/model.mu', 'Parts/GravityBomb/model.mu', None),
    ('BDA', 'Parts/mk82Bomb/texture.png', 'Parts/GravityBomb/texture.png', VIOLET),
    ('BDA', 'Parts/mk82Bomb/model.mu', 'Parts/TachyonBomb/model.mu', None),
    ('BDA', 'Parts/mk82Bomb/texture.png', 'Parts/TachyonBomb/texture.png', GREEN),
    # Warp bomb: NKDR's B-83 (until its own sphere model).
    ('NKDR', 'parts/B-83 Nuclear/model.mu', 'Parts/WarpBomb/model.mu', None),
    ('NKDR', 'parts/B-83 Nuclear/B-83.png', 'Parts/WarpBomb/B-83.png', NAVY),
    # Antimatter bomb: NKDR's Castle Bravo.
    ('NKDR', 'parts/Castle Bravo/model.mu', 'Parts/AntimatterBomb/model.mu', None),
    ('NKDR', 'parts/Castle Bravo/COLOR Castle Bravo.png', 'Parts/AntimatterBomb/COLOR Castle Bravo.png', MAGENTA),
    ('NKDR', 'parts/Castle Bravo/tobump Castle Bravo_NRM.png', 'Parts/AntimatterBomb/tobump Castle Bravo_NRM.png', None),
    # Mushroom bomb: NKDR's Tsar Bomba (round and fat, like the reference).
    ('NKDR', 'parts/Tsar Bomba/model.mu', 'Parts/MushroomBomb/model.mu', None),
    ('NKDR', 'parts/Tsar Bomba/Tsar Bomba.png', 'Parts/MushroomBomb/Tsar Bomba.png', PERIWINKLE),
    # Tachyon-be-gone: BDArmory's ECM jammer pod.
    ('BDA', 'Parts/ecmJammer/model.mu', 'Parts/TachyonShield/model.mu', None),
    ('BDA', 'Parts/ecmJammer/tex_ecmj131.png', 'Parts/TachyonShield/tex_ecmj131.png', GREEN),
    # Phase 3 missiles.
    ('NKDR', 'parts/Tomohawk Conventional/model.mu', 'Parts/CruiseMissile/model.mu', None),
    ('NKDR', 'parts/Tomohawk Conventional/Tomahawk.png', 'Parts/CruiseMissile/Tomahawk.png', OLIVE),
    ('NKDR', 'parts/Tomohawk Conventional/smokepuff1.png', 'Parts/CruiseMissile/smokepuff1.png', None),
    ('NKDR', 'parts/Tomohawk Conventional/srbsmoke.png', 'Parts/CruiseMissile/srbsmoke.png', None),
    ('BDA', 'Parts/aim-120/model.mu', 'Parts/PhaseMissile/model.mu', None),
    ('BDA', 'Parts/aim-120/texture.png', 'Parts/PhaseMissile/texture.png', CYAN),
    ('BDA', 'Parts/aim-120/model.mu', 'Parts/KrakenMissile/model.mu', None),
    ('BDA', 'Parts/aim-120/texture.png', 'Parts/KrakenMissile/texture.png', RED),
    ('BDA', 'Parts/harm/model.mu', 'Parts/KesslerPod/model.mu', None),
    ('BDA', 'Parts/harm/tex_harm.png', 'Parts/KesslerPod/tex_harm.png', ORANGE),
    ('NKDR', 'parts/AGM 158/model.mu', 'Parts/LeechMissile/model.mu', None),
    ('NKDR', 'parts/AGM 158/AGM 158 NEW.png', 'Parts/LeechMissile/AGM 158 NEW.png', TEAL),
    ('NKDR', 'parts/AGM 158/smokepuff1.png', 'Parts/LeechMissile/smokepuff1.png', None),
    ('NKDR', 'parts/AGM 158/srbsmoke.png', 'Parts/LeechMissile/srbsmoke.png', None),
    ('BDA', 'Parts/sidewinder/model.mu', 'Parts/ChronosDart/model.mu', None),
    ('BDA', 'Parts/sidewinder/texture.png', 'Parts/ChronosDart/texture.png', GREEN),
    # Phase 3 guns.
    ('BDAX', 'Parts/Lasers/PulseLaser/Model.mu', 'Parts/TachyonGun/Model.mu', None),
    ('BDAX', 'Parts/Lasers/PulseLaser/pulselaser.png', 'Parts/TachyonGun/pulselaser.png', GREEN),
    ('BDAX', 'Parts/Lasers/PulseLaser/pulseLaser_Emis.png', 'Parts/TachyonGun/pulseLaser_Emis.png', GREEN),
    ('BDAX', 'Parts/Lasers/PulseLaser/laserflare.png', 'Parts/TachyonGun/laserflare.png', None),
    ('BDAX', 'Parts/Lasers/PulseLaser/pulse.ogg', 'Parts/TachyonGun/pulse.ogg', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/Model.mu', 'Parts/RepulsorBeam/Model.mu', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/GravCannon.png', 'Parts/RepulsorBeam/GravCannon.png', DARKVIOLET),
    ('BDAX', 'Parts/Lasers/TorsionCannon/GravCannon_n.png', 'Parts/RepulsorBeam/GravCannon_n.png', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/hflare.png', 'Parts/RepulsorBeam/hflare.png', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/Beam1.png', 'Parts/RepulsorBeam/Beam1.png', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/Beam2.png', 'Parts/RepulsorBeam/Beam2.png', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/Beam3.png', 'Parts/RepulsorBeam/Beam3.png', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/Beam4.png', 'Parts/RepulsorBeam/Beam4.png', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/TorsionFire.ogg', 'Parts/RepulsorBeam/fire.ogg', None),
    ('BDAX', 'Parts/Lasers/TorsionCannon/BeamEnd.ogg', 'Parts/RepulsorBeam/end.ogg', None),
    # Missile exhaust and engine sounds (BDArmory's: their paths differ between BDArmory and BDArmory Lite).
    ('BDA', 'Models/exhaust/smallExhaust.mu', 'FX/Exhaust/smallExhaust.mu', None),
    ('BDA', 'Models/exhaust/mediumExhaust.mu', 'FX/Exhaust/mediumExhaust.mu', None),
    ('BDA', 'Models/exhaust/jetExhaustPrefab.mu', 'FX/Exhaust/jetExhaustPrefab.mu', None),
    ('BDA', 'Models/exhaust/tex_smokeExhaust.dds', 'FX/Exhaust/tex_smokeExhaust.dds', None),
    ('BDA', 'Models/exhaust/tex_sphere.dds', 'FX/Exhaust/tex_sphere.dds', None),
    ('BDA', 'Sounds/rocketLoop.ogg', 'Sounds/rocketLoop.ogg', None),
    ('BDA', 'Sounds/jet.ogg', 'Sounds/jet.ogg', None),
    # Impact explosions. Each weapon's explModelPath is its own copy, so its explosions land in their own BDArmory
    # pool, which the plugin watches to add the weapon's flash. The copies share one folder (and its textures).
    ('BDA', 'Models/explosion/explosion.mu', 'FX/Hits/antimatterHit.mu', None),
    ('BDA', 'Models/explosion/30mmExplosion.mu', 'FX/Hits/neutronHit.mu', None),
    ('BDA', 'Models/explosion/explosion.mu', 'FX/Hits/gravityBlast.mu', None),
    ('BDA', 'Models/explosion/30mmExplosion.mu', 'FX/Hits/tachyonHit.mu', None),
    ('BDA', 'Models/explosion/ExpTex2.png', 'FX/Hits/ExpTex2.png', None),
    ('BDA', 'Models/explosion/ExpTex4.png', 'FX/Hits/ExpTex4.png', None),
    ('BDA', 'Models/explosion/smokeCm.png', 'FX/Hits/smokeCm.png', None),
    ('BDA', 'Models/explosion/spark.png', 'FX/Hits/spark.png', None),
    ('BDA', 'Models/explosion/bulletHit.png', 'FX/Hits/bulletHit.png', None),
    ('BDA', 'Models/explosion/smokeTex.png', 'FX/Hits/smokeTex.png', None),
    # Beam and tracer textures, the laser cone, shared sounds.
    ('BDA', 'Textures/laser.png', 'FX/laser.png', None),
    ('BDA', 'Textures/bullet.png', 'FX/bullet.png', None),
    ('BDA', 'Models/laser/laserCone.mu', 'FX/LaserCone/laserCone.mu', None),
    ('BDA', 'Models/laser/laserTex.png', 'FX/LaserCone/laserTex.png', None),
    ('BDA', 'Sounds/explode1.ogg', 'Sounds/explode1.ogg', None),
    ('BDA', 'Sounds/subExplode.ogg', 'Sounds/subExplode.ogg', None),
    ('BDA', 'Parts/50CalTurret/sounds/turretOverheat.ogg', 'Sounds/turretOverheat.ogg', None),
    # Licences of everything borrowed.
    ('BDA', 'License.txt', 'Licences/BDArmory-License.txt', None),
    ('BDA', 'Credits.txt', 'Licences/BDArmory-Credits.txt', None),
    ('BDAX', 'License.txt', 'Licences/BDArmoryExtended-License.txt', None),
    ('NKDR', 'Licences.txt', 'Licences/NKDR-Licence.txt', None),
]


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--inputs', default=str(ROOT.parents[1] / 'BDArmory Lite'))
    a = p.parse_args()
    inputs = Path(a.inputs)
    missing = [f'{s}/{src}' for s, src, _, _ in MANIFEST if not (inputs / SOURCES[s] / src).is_file()]
    if missing:
        sys.exit('Missing inputs (under ' + str(inputs) + '):\n  ' + '\n  '.join(missing))
    shutil.rmtree(ASSETS, ignore_errors=True)
    for s, src, dst, colour in MANIFEST:
        data = (inputs / SOURCES[s] / src).read_bytes()
        if colour:
            data = png.tint(data, colour)
        out = ASSETS / dst
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(data)
        print(f'{s}/{src} -> assets/{dst}{" (tinted " + colour + ")" if colour else ""}', flush=True)
    # A silent clip for BDArmory's chargeSoundPath on weapons whose charge sound the plugin makes (ModuleFFWChargeSparks):
    # BDArmory plays its clip once, and an empty path logs an error.
    with wave.open(str(ASSETS / 'Sounds' / 'silence.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(44100)
        w.writeframes(b'\x00\x00' * 2205)
    print(f'Imported {len(MANIFEST)} files; wrote Sounds/silence.wav.')


if __name__ == '__main__':
    main()
