#!/usr/bin/env python3
"""Build the Far Future Weaponry plugin.

Usage: python3 tools/build.py            (KSP's Managed folder from config.local.json "kspRoot", or KSP_ROOT)
Output: build/FarFutureWeaponry.dll
"""
import os, subprocess, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import localconfig

ROOT = Path(__file__).resolve().parents[1]
NAME = 'FarFutureWeaponry'


def ksp_managed():
    cfg = localconfig.load(required=False)
    ksp = os.environ.get('KSP_ROOT') or cfg.get('kspRoot')
    if not ksp:
        sys.exit('Set KSP_ROOT or "kspRoot" in config.local.json (a KSP 1.12.5 folder containing KSP_x64_Data/Managed).')
    managed = Path(ksp).expanduser() / 'KSP_x64_Data' / 'Managed'
    if not (managed / 'Assembly-CSharp.dll').exists():
        sys.exit(f'KSP assemblies not found in {managed}')
    return managed


def compiler():
    home = Path.home() / '.dotnet' / ('dotnet.exe' if os.name == 'nt' else 'dotnet')
    dotnet = os.environ.get('DOTNET') or (str(home) if home.exists() else 'dotnet')
    sdks = subprocess.check_output([dotnet, '--list-sdks'], text=True).strip().splitlines()
    version, base = sdks[-1].split(' ', 1)
    return dotnet, Path(base.strip('[]')) / version / 'Roslyn' / 'bincore' / 'csc.dll'


def sources():
    return [p for p in sorted((ROOT / 'src').rglob('*.cs')) if '/obj/' not in p.as_posix() and '/bin/' not in p.as_posix()]


def main():
    managed = ksp_managed()
    dotnet, csc = compiler()
    out = ROOT / 'build'
    out.mkdir(exist_ok=True)
    refs = [managed / f'{n}.dll' for n in ['mscorlib', 'System', 'System.Core', 'Assembly-CSharp', 'Assembly-CSharp-firstpass']]
    refs += sorted(managed.glob('UnityEngine*.dll'))
    args = ['/nostdlib+', '/nologo', '/langversion:9.0', '/target:library', '/optimize+',
            '/deterministic+', '/warnaserror-', '/nowarn:1591', f'/out:{out / (NAME + ".dll")}']
    args += [f'/reference:{r}' for r in refs]
    args += [str(s) for s in sources()]
    rsp = out / 'csc.rsp'
    rsp.write_text('\n'.join(f'"{a}"' for a in args) + '\n')
    result = subprocess.run([dotnet, str(csc), '/noconfig', f'@{rsp}'], cwd=ROOT, text=True, capture_output=True)
    lines = [l for l in (result.stdout + result.stderr).splitlines() if l.strip()]
    for l in lines:
        print(l.replace(str(ROOT) + os.sep, ''))
    if result.returncode != 0:
        sys.exit(f'Build failed: {sum(1 for l in lines if ": error " in l)} error(s).')
    print(f'Built build/{NAME}.dll ({len(sources())} source files).')


if __name__ == '__main__':
    main()
