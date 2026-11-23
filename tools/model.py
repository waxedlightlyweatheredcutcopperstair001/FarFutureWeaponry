#!/usr/bin/env python3
"""Build a part model with Blender: models/<Part>/build.py -> assets/Parts/<Part>/ (model.mu and its textures).

Usage (Windows: "py tools\\model.py ..."; elsewhere "python3 tools/model.py ..."):
    model.py <Part> [--preview]     (previews go to build/previews/<Part>/)

Only needed to change a model: the built models are committed in assets/, and make.py / package.py use those.
Needs Blender 4.5 with the io_object_mu add-on installed and enabled. Blender is found from "blender" in
config.local.json, else on PATH, else in its usual install folders.
"""
import argparse, glob, os, shutil, subprocess, sys, tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import localconfig

ROOT = Path(__file__).resolve().parents[1]


def find_blender():
    cfg = localconfig.load(required=False).get('blender')
    if cfg:
        if Path(cfg).is_file():
            return cfg
        sys.exit(f'config.local.json: "blender" = {cfg}: no such file.')
    found = shutil.which('blender')
    if found:
        return found
    candidates = [str(Path.home() / '.local' / 'bin' / 'blender')]
    if os.name == 'nt':
        for base in (os.environ.get('ProgramFiles', r'C:\Program Files'), os.environ.get('ProgramFiles(x86)', '')):
            if base:
                candidates += sorted(glob.glob(os.path.join(base, 'Blender Foundation', 'Blender*', 'blender.exe')), reverse=True)
        candidates += sorted(glob.glob(os.path.join(os.environ.get('ProgramFiles', r'C:\Program Files'),
                                                    'Steam', 'steamapps', 'common', 'Blender', 'blender.exe')))
    elif sys.platform == 'darwin':
        candidates.append('/Applications/Blender.app/Contents/MacOS/Blender')
    for c in candidates:
        if Path(c).is_file():
            return c
    sys.exit('Blender was not found. Install Blender 4.5, or add "blender": "<path to blender.exe>" to config.local.json.')


def main():
    p = argparse.ArgumentParser()
    p.add_argument('part')
    p.add_argument('--preview', action='store_true')
    a = p.parse_args()
    script = ROOT / 'models' / a.part / 'build.py'
    if not script.is_file():
        sys.exit(f'No {script}')
    blender = find_blender()
    out = ROOT / 'assets' / 'Parts' / a.part
    # Build into a scratch folder; the committed model is only replaced if Blender succeeds.
    with tempfile.TemporaryDirectory(prefix='ffw-model-') as tmp:
        cmd = [blender, '-b', '-noaudio', '--python-exit-code', '1', '-P', str(script), '--', '--out', tmp]
        if a.preview:
            cmd += ['--preview', str(ROOT / 'build' / 'previews' / a.part)]
        print('>', ' '.join(f'"{c}"' if ' ' in c else c for c in cmd), flush=True)
        r = subprocess.run(cmd, cwd=ROOT)
        if r.returncode != 0 or not (Path(tmp) / 'model.mu').is_file():
            sys.exit(f'Blender failed (exit code {r.returncode}); {out} is unchanged.')
        shutil.rmtree(out, ignore_errors=True)
        shutil.copytree(tmp, out)
    print('Built', out)


if __name__ == '__main__':
    main()
