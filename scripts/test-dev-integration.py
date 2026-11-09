#!/usr/bin/env python3
"""Windows/WSL checks using a disposable source tree and development profile."""
import argparse
import importlib.util
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--nuget-cache', type=Path)
    args = parser.parse_args()
    source = Path(__file__).resolve().parents[1]
    spec = importlib.util.spec_from_file_location('shadeflow_dev', source / 'scripts/dev.py')
    dev = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(dev)
    checks = 0

    def check(condition, message):
        nonlocal checks
        if not condition: raise AssertionError(message)
        checks += 1
        print('PASS:', message, flush=True)

    def wait(predicate, timeout=60):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            value = predicate()
            if value: return value
            if process.poll() is not None: raise RuntimeError(log.read_text())
            time.sleep(.2)
        raise TimeoutError(log.read_text())

    with tempfile.TemporaryDirectory(prefix='shadeflow-dev-integration-') as temporary:
        root = Path(temporary) / 'project'
        shutil.copytree(source, root, ignore=shutil.ignore_patterns('.dev', '.git', 'bin', 'obj', 'artifacts', '__pycache__'))
        log = Path(temporary) / 'runner.log'
        arguments = [sys.executable, str(root / 'scripts/dev.py'), '--hidden', '--dotnet', str(args.dotnet.resolve())]
        if args.nuget_cache: arguments += ['--nuget-cache', str(args.nuget_cache.resolve())]
        with log.open('w') as output:
            process = subprocess.Popen(arguments, stdout=output, stderr=subprocess.STDOUT)
            try:
                session = wait(lambda: dev.read_json(root / '.dev/session.json'))
                control = Path(session['control'])
                revision = session['revision']
                pid = session['ready']['pid']

                def request(command):
                    import uuid
                    identity = uuid.uuid4().hex
                    dev.atomic_json(control / 'command.json', {'id': identity, 'command': command, 'revision': revision})
                    return wait(lambda: (reply if (reply := dev.read_json(control / 'reply.json')).get('id') == identity else None))

                status = request('status')['status']
                check(status['previewOnly'] and status['overlays'] == 0, 'Development uses preview brightness without real overlays')
                check('\\.dev\\profile\\' in status['profile'], 'Configuration is isolated from the installed application')
                colors = root / 'Styles/Colors.xaml'
                original = colors.read_text()
                colors.write_text(original.replace('x:Key="Space8">8', 'x:Key="Space8">9'))
                wait(lambda: 'Estilos atualizados sem fechar a janela.' in log.read_text())
                check(request('status')['status']['pid'] == pid, 'Watcher applies live resources without restarting')
                colors.write_text('<invalid>')
                wait(lambda: 'Estilos não aplicados:' in log.read_text())
                check(request('status')['status']['spacing'].startswith('9'), 'Invalid XAML preserves the previous working resources')
                successes = log.read_text().count('Estilos atualizados sem fechar a janela.')
                colors.write_text(original)
                wait(lambda: log.read_text().count('Estilos atualizados sem fechar a janela.') > successes)
                check(request('status')['status']['spacing'].startswith('8'), 'Corrected XAML replaces the live resource')
                check(True, 'Watcher recovers after correcting XAML')
                code = root / 'MainWindow.Visual.cs'
                code.write_text(code.read_text() + '\n// Integration restart check.\n')
                fresh = wait(lambda: (session if (session := dev.read_json(root / '.dev/session.json')).get('revision') != revision else None))
                revision = fresh['revision']
                check(fresh['ready']['pid'] != pid, 'Code edits trigger an incremental rebuild and cooperative restart')
                check((root / '.dev/profile/window-state.json').is_file(), 'Window state is saved across the automatic restart')
                check(request('stop')['ok'], 'Development session exits cooperatively')
                check(process.wait(timeout=20) == 0, 'Supervisor exits without force termination')
            finally:
                if process.poll() is None:
                    process.terminate()
                    process.wait(timeout=70)
    print(f'{checks} development integration checks passed.')


if __name__ == '__main__':
    main()
