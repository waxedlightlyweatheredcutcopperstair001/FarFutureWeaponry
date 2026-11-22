#!/usr/bin/env python3
"""Assemble GameData/FarFutureWeaponry from the built plugin, the borrowed assets and the configs, check it, zip it.

Usage:
    python3 tools/package.py              build/GameData/FarFutureWeaponry + release/FarFutureWeaponry-<version>.zip
    python3 tools/package.py --deploy     ...and copy it into the KSP install from config.local.json ("kspRoot")

Layout of the package:
    Plugins/FarFutureWeaponry.dll      the plugin (tools/build.py)
    Parts/<Part>/                      part.cfg (configs/) + model, textures, sounds (assets/)
    FX/, Sounds/                       borrowed explosion models, textures, sounds (assets/)
    Bullets.cfg, Resources.cfg, Effects.cfg
    Licences/                          licences of the borrowed models
    README.md, CHANGELOG.md, LICENSE
"""
import argparse, re, shutil, subprocess, sys, zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import localconfig

ROOT = Path(__file__).resolve().parents[1]
NAME = 'FarFutureWeaponry'


def version():
    m = re.search(r'AssemblyInformationalVersion\("([^"]+)"\)', (ROOT / 'src' / 'AssemblyInfo.cs').read_text())
    return m.group(1) if m else '0.0.0'


def copy(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)


def assemble(out):
    shutil.rmtree(out, ignore_errors=True)
    dll = ROOT / 'build' / (NAME + '.dll')
    if not dll.exists():
        sys.exit(f'build/{NAME}.dll not found: run tools/build.py first.')
    copy(dll, out / 'Plugins' / dll.name)
    for folder in ('assets', 'configs', 'package'):
        for f in sorted((ROOT / folder).rglob('*')):
            if f.is_file():
                dst = out / f.relative_to(ROOT / folder)
                if dst.exists():
                    sys.exit(f'{folder}/{f.relative_to(ROOT / folder)} would overwrite a file from another folder.')
                copy(f, dst)
    copy(ROOT / 'LICENSE', out / 'LICENSE')


def make_zip(out, ver):
    release = ROOT / 'release'
    release.mkdir(exist_ok=True)
    z = release / f'{NAME}-{ver}.zip'
    if z.exists():
        z.unlink()
    with zipfile.ZipFile(z, 'w', zipfile.ZIP_DEFLATED) as zf:
        for f in sorted(out.rglob('*')):
            if f.is_file():
                zf.write(f, (Path(NAME) / f.relative_to(out)).as_posix())
    return z


def deploy(out):
    cfg = localconfig.load(required=False)
    ksp = cfg.get('kspRoot')
    if not ksp or not (Path(ksp) / 'GameData').is_dir():
        sys.exit('Set "kspRoot" in config.local.json to your KSP folder (the one containing GameData) to deploy.')
    gamedata = Path(ksp) / 'GameData'
    if not ((gamedata / 'BDArmory').is_dir() or (gamedata / 'BDArmoryLite').is_dir()):
        print('WARNING: neither GameData/BDArmory nor GameData/BDArmoryLite found: this mod needs one of them.')
    if not (gamedata / 'VolumetricFXReconfigured').is_dir():
        print('WARNING: GameData/VolumetricFXReconfigured not found: this mod needs VFX Reconfigured.')
    if not (gamedata / 'PhysicsRangeExtender').is_dir():
        print('WARNING: GameData/PhysicsRangeExtender not found: this mod needs Physics Range Extender.')
    if not any(gamedata.glob('ModuleManager*.dll')):
        print('WARNING: no ModuleManager DLL in GameData: this mod needs ModuleManager.')
    target = gamedata / NAME
    try:
        if target.exists():
            shutil.rmtree(target)
        shutil.copytree(out, target)
    except OSError as e:
        sys.exit(f'Could not replace {target} ({e}). Is KSP running? Close it and deploy again.')
    print(f'Deployed to {target}')


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--deploy', action='store_true', help='copy the package into the KSP install (config.local.json kspRoot)')
    a = p.parse_args()
    out = ROOT / 'build' / 'GameData' / NAME
    assemble(out)
    r = subprocess.run([sys.executable, '-I', str(ROOT / 'tools' / 'validate.py'), str(out)])
    if r.returncode != 0:
        sys.exit('The package has problems (above): not zipped.')
    ver = version()
    z = make_zip(out, ver)
    files = sum(1 for f in out.rglob('*') if f.is_file())
    print(f'Packaged {NAME} {ver}: {files} files; {z.relative_to(ROOT)}')
    if a.deploy:
        deploy(out)


if __name__ == '__main__':
    main()
