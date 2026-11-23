#!/usr/bin/env python3
"""Build a part model with Blender: models/<Part>/build.py -> assets/Parts/<Part>/ (model.mu and its textures).

Usage: python3 tools/model.py <Part> [--preview]     (previews go to build/previews/<Part>/)
Needs Blender 4.5 (on PATH or ~/.local/bin/blender) with the io_object_mu add-on installed.
"""
import argparse, shutil, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    p = argparse.ArgumentParser()
    p.add_argument('part')
    p.add_argument('--preview', action='store_true')
    a = p.parse_args()
    script = ROOT / 'models' / a.part / 'build.py'
    if not script.is_file():
        sys.exit(f'No {script}')
    blender = shutil.which('blender') or str(Path.home() / '.local' / 'bin' / 'blender')
    out = ROOT / 'assets' / 'Parts' / a.part
    shutil.rmtree(out, ignore_errors=True)
    cmd = [blender, '-b', '-noaudio', '--python-exit-code', '1', '-P', str(script), '--', '--out', str(out)]
    if a.preview:
        cmd += ['--preview', str(ROOT / 'build' / 'previews' / a.part)]
    r = subprocess.run(cmd, cwd=ROOT)
    if r.returncode != 0:
        sys.exit(f'Blender failed (exit code {r.returncode}).')
    print('Built', out)


if __name__ == '__main__':
    main()
