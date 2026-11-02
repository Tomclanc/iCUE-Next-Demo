"""Prepare version-locked local DLL patches; never writes to Program Files."""
import argparse
import base64
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def apply_patch(data, patch):
    digest = hashlib.sha256(data).hexdigest()
    if digest == patch['after']:
        return data
    if digest != patch['before'] or len(data) != patch['size']:
        raise ValueError('Unsupported file: ' + patch['file'])
    result = bytearray(data)
    for edit in patch['edits']:
        payload = base64.b64decode(edit['data'], validate=True)
        offset = edit['offset']
        if offset < 0 or offset + len(payload) > len(result):
            raise ValueError('Patch outside file')
        result[offset:offset + len(payload)] = payload
    if hashlib.sha256(result).hexdigest() != patch['after']:
        raise ValueError('Output checksum mismatch')
    return bytes(result)


def prepare(base):
    module = base / 'imports/CueAppearance'
    required = ['AppearancePlugin.dll', 'qmldir', 'icue-app.png']
    if not all((module / name).is_file() for name in required):
        raise ValueError('Existing CueAppearance theme module is required; this is an add-on, not a full theme installer.')
    # Validate every input before producing staged files.
    prepared = []
    for patch in json.loads((ROOT / 'patches.json').read_text()):
        source = (base / 'cue_qml_plugins' / patch['file']).read_bytes()
        prepared.append((patch['file'], source, apply_patch(source, patch)))
    stage, backup = ROOT / '.prepared', ROOT / '.backup'
    stage.mkdir(exist_ok=True)
    backup.mkdir(exist_ok=True)
    for name, before, after in prepared:
        if not (backup / name).exists():
            (backup / name).write_bytes(before)
        (stage / name).write_bytes(after)
    qmldir = (module / 'qmldir').read_bytes()
    if not (backup / 'qmldir').exists():
        (backup / 'qmldir').write_bytes(qmldir)
    text = qmldir.decode('utf-8-sig')
    if not any(line.startswith('TrayTheme ') for line in text.splitlines()):
        text = text.rstrip() + '\nTrayTheme 1.0 TrayTheme.qml\n'
    (stage / 'qmldir').write_text(text, encoding='utf-8')
    print('Prepared and verified both DLLs; local originals retained in .backup.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--base', type=Path, default=Path(r'C:\Program Files\Corsair\Corsair iCUE5 Software'))
    prepare(parser.parse_args().base)
