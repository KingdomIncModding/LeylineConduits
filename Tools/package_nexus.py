"""Package the locally verified prototype without uploading or publishing it."""
from pathlib import Path
import hashlib
import json
import tempfile
import zipfile
import xml.etree.ElementTree as ET

PROJECT = Path(__file__).resolve().parents[1]
GAME = PROJECT.parents[1]
OUT = PROJECT / 'Releases' / 'Nexus'
VERSION = ET.parse(PROJECT / 'LeylineConduits.csproj').getroot().findtext('PropertyGroup/Version')


def digest(data):
    return hashlib.sha256(data).hexdigest()


def archive(path, entries):
    with zipfile.ZipFile(path, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as output:
        for name, data in sorted(entries.items()):
            assert not Path(name).is_absolute() and '..' not in Path(name).parts
            output.writestr(name, data)
    with zipfile.ZipFile(path) as check, tempfile.TemporaryDirectory(prefix='leyline-package-') as clean:
        assert check.testzip() is None and set(check.namelist()) == set(entries)
        check.extractall(clean)
        for name, data in entries.items():
            assert (Path(clean) / name).read_bytes() == data


def main():
    dll = PROJECT / 'bin/Release/netstandard2.1/LeylineConduits.dll'
    checks = json.loads((PROJECT / 'Verification/runtime-tests.json').read_text())
    companions = json.loads((PROJECT / 'Verification/compatibility-tests.json').read_text())
    for report in (checks, companions):
        assert report['passed'] and report['version'] == VERSION
        assert report['saveWritesBlocked'] and not report['savedKingdomLoaded']
    runtimes = list((PROJECT / 'Verification').glob('Runtime-*'))
    tested = []
    for runtime in runtimes:
        report_path = runtime / '_Mods/LeylineConduits/Verification/runtime-tests.json'
        if not report_path.exists():
            continue
        report = json.loads(report_path.read_text())
        if report['passed'] and report['testedAtUtc'] in (checks['testedAtUtc'], companions['testedAtUtc']):
            tested.append(runtime)
            assert dll.read_bytes() == (runtime / 'BepInEx/plugins/LeylineConduits/LeylineConduits.dll').read_bytes()
    assert {json.loads((p / '_Mods/LeylineConduits/Verification/runtime-tests.json').read_text())['testedAtUtc'] for p in tested} == {
        checks['testedAtUtc'], companions['testedAtUtc']}, 'Both isolated and companion runtimes must match the packaged DLL'
    installed = GAME / 'BepInEx/plugins/LeylineConduits/LeylineConduits.dll'
    assert installed.read_bytes() == dll.read_bytes()
    OUT.mkdir(parents=True, exist_ok=True)
    player = OUT / f'KingdomInc-LeylineConduits-{VERSION}.zip'
    player_entries = {'BepInEx/plugins/LeylineConduits/LeylineConduits.dll': dll.read_bytes(),
                      'Leyline Conduits requirements.txt': (PROJECT / 'Nexus/REQUIREMENTS.txt').read_bytes(),
                      'Leyline Conduits README.txt': (PROJECT / 'README.md').read_bytes()}
    archive(player, player_entries)
    source_files = sorted(PROJECT.glob('*.cs')) + [PROJECT / name for name in
        ['LeylineConduits.csproj', 'README.md', '.gitignore', 'install.ps1', 'verify-isolated.ps1']]
    source_files += [Path(__file__).resolve(), PROJECT / 'Assets/Thumbnails/leyline-conduits-thumbnail-v1.png']
    source_files += sorted((PROJECT / 'Nexus').glob('*.txt'))
    source = OUT / f'KingdomInc-LeylineConduits-{VERSION}-source.zip'
    archive(source, {'_Mods/LeylineConduits/' + p.relative_to(PROJECT).as_posix(): p.read_bytes() for p in source_files})
    report = {'name': 'Leyline Conduits', 'version': VERSION, 'publication': 'local prototype only',
              'gameVersion': checks['gameVersion'], 'unityVersion': checks['unityVersion'],
              'runtimeChecksPassed': len(checks['checks']), 'companionChecksPassed': len(companions['checks']),
              'dllSha256': digest(dll.read_bytes()), 'cleanExtraction': 'passed',
              'archives': {p.name: {'bytes': p.stat().st_size, 'sha256': digest(p.read_bytes())} for p in [player, source]}}
    (OUT / 'RELEASE_VERIFICATION.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
