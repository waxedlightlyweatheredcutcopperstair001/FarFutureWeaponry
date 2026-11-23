#!/usr/bin/env python3
"""Build the plugin's shader bundle (Windows, Unity 2019.4.18f1: KSP 1.12's version).

Usage (Windows: "py tools\\shaders.py"): uses "unityEditor" from config.local.json (or UNITY_EDITOR).
Copies shaders/ (Assets, Packages, ProjectSettings) into a scratch Unity project (build/unity-project, not committed),
runs BuildBundle.Build there and writes build/shader-bundle/:
    farfutureweaponry-windows.bundle   the bundle (the plugin loads it from Shaders/)
    unity-validation.json              what Unity checked
The Unity log is build/unity-project/unity.log.
"""
import json, os, shutil, subprocess, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import localconfig

ROOT = Path(__file__).resolve().parents[1]
BUNDLE = 'farfutureweaponry-windows'


def main():
    cfg = localconfig.load(required=False)
    editor = os.environ.get('UNITY_EDITOR') or cfg.get('unityEditor')
    if not editor or not Path(editor).is_file():
        sys.exit('Set "unityEditor" in config.local.json to Unity 2019.4.18f1\'s Unity.exe.')
    project = ROOT / 'build' / 'unity-project'
    # Assets, plus a minimal package list and the editor version (nothing to download).
    for folder in ('Assets', 'Packages', 'ProjectSettings'):
        if folder != 'ProjectSettings':
            shutil.rmtree(project / folder, ignore_errors=True)
        shutil.copytree(ROOT / 'shaders' / folder, project / folder, dirs_exist_ok=True)
    output = ROOT / 'build' / 'shader-bundle'
    shutil.rmtree(output, ignore_errors=True)
    output.mkdir(parents=True)
    log = project / 'unity.log'
    env = dict(os.environ, FFW_BUNDLE_OUTPUT=str(output))
    args = [editor, '-batchmode', '-projectPath', str(project), '-executeMethod', 'BuildBundle.Build', '-logFile', str(log)]
    if os.name == 'nt':
        args.append('-force-d3d11')   # (not -nographics: Unity can drop shader variants without a device)
    print('> Unity: building the shader bundle (a minute or two; log: build/unity-project/unity.log)', flush=True)
    code = subprocess.run(args, env=env, cwd=ROOT).returncode
    validation = output / 'unity-validation.json'
    if code != 0 or not validation.exists():
        errors = []
        if log.exists():
            errors = [l for l in log.read_text(errors='replace').splitlines() if '[FFW]' in l or 'error' in l.lower()][-15:]
        sys.exit('Unity did not build the bundle (exit code %d). From the log:\n  ' % code + '\n  '.join(errors or ['(no log)']))
    built = output / BUNDLE
    if not built.exists() or built.read_bytes()[:7] != b'UnityFS':
        sys.exit('Unity reported success but wrote no valid bundle.')
    final = output / (BUNDLE + '.bundle')
    built.replace(final)
    for extra in output.glob(BUNDLE + '.manifest'):
        extra.unlink()
    result = json.loads(validation.read_text())
    print(f'Built {final.relative_to(ROOT)} ({final.stat().st_size // 1024} KB, {result.get("shaders")} shaders)')


if __name__ == '__main__':
    main()
