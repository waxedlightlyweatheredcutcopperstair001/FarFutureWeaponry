#!/usr/bin/env python3
"""Build everything: the plugin, the tests, the package; optionally install into KSP.

Usage (Windows: "py tools\\make.py ..."; elsewhere "python3 tools/make.py ..."):
    make.py                 plugin + tests + package (release/FarFutureWeaponry-<version>.zip)
    make.py --deploy        ...and install into the KSP folder in config.local.json
    make.py --no-tests      skip the tests

config.local.json (next to this repository's README; not committed; copy config.example.json):
    { "kspRoot": "C:/Path/To/Kerbal Space Program" }

Needs: Python 3 and the .NET SDK (8 or newer). No Unity: there are no shaders yet.
"""
import argparse, os, shutil, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def run(args, env=None):
    print('>', ' '.join(str(a) for a in args), flush=True)
    r = subprocess.run([str(a) for a in args], cwd=ROOT, env=env)
    if r.returncode != 0:
        sys.exit(f'Step failed (exit code {r.returncode}).')


def dotnet():
    home = Path.home() / '.dotnet' / ('dotnet.exe' if os.name == 'nt' else 'dotnet')
    return str(home) if home.exists() else (shutil.which('dotnet') or sys.exit('The .NET SDK (dotnet) was not found.'))


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--deploy', action='store_true')
    p.add_argument('--no-tests', action='store_true')
    a = p.parse_args()
    py = sys.executable
    run([py, ROOT / 'tools' / 'build.py'])
    if not a.no_tests:
        env = dict(os.environ, DOTNET_ROLL_FORWARD='Major')
        run([dotnet(), 'run', '--project', ROOT / 'tests' / 'FarFutureWeaponry.Tests.csproj', '--configuration', 'Release'], env=env)
    run([py, ROOT / 'tools' / 'package.py'] + (['--deploy'] if a.deploy else []))


if __name__ == '__main__':
    main()
