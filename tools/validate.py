#!/usr/bin/env python3
"""Static checks on the assembled package (build/GameData/FarFutureWeaponry), run by package.py.

- Every config parses; no "//" hidden inside a value (KSP would cut the value there).
- Parts: unique names starting with "ffw"; the mesh exists; every module is a known BDArmory, stock or FFW module.
- Every FarFutureWeaponry/... path in a config resolves to a file in the package (model, texture or sound).
- Every texture a model names is next to it; every transform and animation a config names is in its model.
- Weapons: bullet types are defined; ammo resources are ElectricCharge or Antimatter.
- Effects: each FFW_IMPACT_FLASH watches an explosion model some weapon uses; each FFW_BULLET_TRAIL a defined bullet.

Usage: python3 -I tools/validate.py [package folder]     Exit code 1 if any problem.
"""
import re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAME = 'FarFutureWeaponry'
ASSET_EXT = ('.mu', '.png', '.dds', '.tga', '.ogg', '.wav', '.mbm')
KNOWN_MODULES = {'ModuleWeapon', 'ModuleTurret', 'ModulePartVariants', 'ModuleCASE'}
RESOURCES = {'ElectricCharge', 'Antimatter'}

problems = []


def problem(msg):
    problems.append(msg)
    print('PROBLEM: ' + msg)


class Node:
    def __init__(self, name):
        self.name, self.values, self.nodes = name, [], []

    def get(self, key, default=None):
        for k, v in self.values:
            if k == key:
                return v
        return default

    def walk(self):
        yield self
        for n in self.nodes:
            yield from n.walk()


def parse(path):
    """KSP ConfigNode text -> root Node. Reports "//" inside quoted-looking text and unbalanced braces."""
    root = Node('root')
    stack, pending = [root], None
    for no, raw in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
        line = raw.split('//', 1)[0].strip()
        key = raw.split('=', 1)[0].strip() if '=' in raw else ''
        if key in ('description', 'title') and '//' in raw:
            problem(f'{path.name}:{no}: "//" in {key}: KSP cuts the text there')
        while line:
            if line.startswith('{'):
                n = Node(pending or '')
                stack[-1].nodes.append(n)
                stack.append(n)
                pending = None
                line = line[1:].strip()
            elif line.startswith('}'):
                if len(stack) == 1:
                    problem(f'{path.name}:{no}: unbalanced "}}"')
                else:
                    stack.pop()
                line = line[1:].strip()
            elif '=' in line.split('{')[0].split('}')[0]:
                seg = re.split(r'[{}]', line, 1)[0]
                k, v = seg.split('=', 1)
                stack[-1].values.append((k.strip(), v.strip()))
                line = line[len(seg):].strip()
            else:
                seg = re.split(r'[{}]', line, 1)[0]
                pending = seg.strip() or pending
                line = line[len(seg):].strip()
    if len(stack) != 1:
        problem(f'{path.name}: {len(stack) - 1} unclosed "{{"')
    return root


class Mu:
    """A .mu file's bytes: names (transforms, animations, textures) are stored as plain strings in it."""
    def __init__(self, path):
        self.data = path.read_bytes()
        self.textures = sorted({m.decode('latin-1') for m in re.findall(rb'[\w .-]+\.(?:png|dds|tga|mbm)', self.data, re.I)})

    def __contains__(self, name):
        return name.encode('latin-1') in self.data


def resolve(pkg, value):
    """A FarFutureWeaponry/... asset path (no extension) -> file, or None."""
    rel = value[len(NAME) + 1:]
    for ext in ('',) + ASSET_EXT:
        f = pkg / (rel + ext)
        if f.is_file():
            return f
    return None


def main():
    pkg = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'build' / 'GameData' / NAME
    src = '\n'.join(p.read_text() for p in (ROOT / 'src').rglob('*.cs'))
    ffw_modules = set(re.findall(r'class (\w+)\s*:\s*PartModule', src))

    configs = {p: parse(p) for p in sorted(pkg.rglob('*.cfg'))}
    bullets = {n.get('name') for root in configs.values() for n in root.walk() if n.name == 'BULLET'}
    parts, expl_used = {}, set()

    for path, root in configs.items():
        rel = path.relative_to(pkg)
        for n in root.walk():
            for k, v in n.values:
                if v.startswith(NAME + '/') and resolve(pkg, v) is None:
                    problem(f'{rel}: {k} = {v}: no such file in the package')
                if k in ('explModelPath',):
                    expl_used.add(v)
        for part in (n for n in root.nodes if n.name == 'PART'):
            name = part.get('name')
            if not name or not name.startswith('ffw'):
                problem(f'{rel}: part name "{name}" should start with "ffw"')
            if name in parts:
                problem(f'{rel}: duplicate part name {name}')
            parts[name] = path
            mesh = part.get('mesh')
            model = path.parent / mesh if mesh else None
            if model is None or not model.is_file():
                problem(f'{rel}: mesh "{mesh}" not found next to the config')
                continue
            names = Mu(model)
            for tex in names.textures:
                tex = tex.strip()
                if not any((model.parent / (Path(tex).stem + e)).is_file() for e in ('.png', '.dds', '.tga', '.mbm')):
                    problem(f'{rel}: the model uses texture {tex}, which is not next to it')
            for m in (x for x in part.nodes if x.name == 'MODULE'):
                mname = m.get('name')
                if mname not in KNOWN_MODULES and mname not in ffw_modules:
                    problem(f'{rel}: unknown module {mname}')
                for key in ('fireTransformName', 'pitchTransformName', 'yawTransformName', 'sparkTransformName'):
                    t = m.get(key)
                    if t and t not in names:
                        problem(f'{rel}: {mname}.{key} = {t}: no such transform in {model.name}')
                for key in ('fireAnimName', 'reloadAnimName', 'deployAnimName'):
                    if m.get(key):
                        for anim in (a.strip() for a in m.get(key).split(';') if a.strip()):
                            if anim not in names:
                                problem(f'{rel}: {mname}.{key}: no animation {anim} in {model.name}')
                for variant in (x for x in m.walk() if x.name == 'GAMEOBJECTS'):
                    for go, _ in variant.values:
                        if go not in names:
                            problem(f'{rel}: variant object {go} is not in {model.name}')
                if mname == 'ModuleWeapon':
                    for b in (x.strip() for x in (m.get('bulletType') or '').split(';') if x.strip()):
                        if b not in bullets:
                            problem(f'{rel}: bulletType {b} is not defined')
                    for key in ('ammoName', 'secondaryAmmoName'):
                        if m.get(key) and m.get(key) not in RESOURCES:
                            problem(f'{rel}: {key} = {m.get(key)} is not ElectricCharge or Antimatter')
            for r in (x for x in part.nodes if x.name == 'RESOURCE'):
                if r.get('name') not in RESOURCES:
                    problem(f'{rel}: resource {r.get("name")} is not ElectricCharge or Antimatter')

    for path, root in configs.items():
        rel = path.relative_to(pkg)
        for n in root.nodes:
            if n.name == 'FFW_IMPACT_FLASH' and n.get('explosion') not in expl_used:
                problem(f'{rel}: FFW_IMPACT_FLASH {n.get("explosion")} is not any weapon\'s explModelPath')
            if n.name == 'FFW_BULLET_TRAIL' and n.get('bullet') not in bullets:
                problem(f'{rel}: FFW_BULLET_TRAIL bullet {n.get("bullet")} is not defined')

    for need in ('Plugins/FarFutureWeaponry.dll', 'LICENSE', 'README.md', 'CHANGELOG.md'):
        if not (pkg / need).is_file():
            problem(f'{need} missing from the package')

    print(f'{len(parts)} parts, {len(bullets)} bullets, {len(configs)} configs: {len(problems)} problem(s)')
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
