"""Bounded directed regression only; no automatic retries of failed executions."""
import json
import pathlib
import subprocess
import time

root = pathlib.Path(__file__).parent

def command(args, name):
    split = args.index('--') if '--' in args else len(args)
    argv = ['unity', 'command', *args[:split], '--caller', 'plugin', '--skill', 'unity-cli', '--format', 'json', *args[split:]]
    p = subprocess.run(argv,
                       capture_output=True, encoding='utf-8', timeout=25)
    (root / name).write_text(p.stdout, encoding='utf-8')
    if p.returncode:
        raise RuntimeError((name, p.returncode, p.stdout, p.stderr))
    result = json.loads(p.stdout)
    assert result['success'], result
    return result['data']['result']

for mode, classes in [
    ('editor', ['SessionLifecycleIntegrationTests', 'PlayerLifeIntegrationTests', 'EnemyTypesTests',
                'EnemyWaveStateTests', 'TankStateTests', 'PlayerUpgradeTests', 'Stage01Tests']),
    ('playmode', ['EnemyTypesPlayModeTests', 'EnemyTankViewTests', 'PlayerUpgradePlayModeTests',
                  'PlayerLifeIntegrationPlayModeTests', 'SessionLifecyclePlayModeTests'])]:
    for name in classes:
        command(['test', 'run', '--', '--mode', mode, '--filter', 'BattleCity.Tests.' + name + '.',
                 '--async_tests', 'true', '--timeout', '60'], name + '-Run.json')
        for attempt in range(6):
            time.sleep(1)
            r = command(['test', 'status'], name + '-Progress' + str(attempt) + '.json')
            if r.get('status') != 'running':
                break
        assert r.get('status') == 'completed', (name, r)
        summary = r.get('summary', r.get('Summary'))
        assert summary and summary['total'] > 0 and summary['failed'] == 0 and summary['passed'] == summary['total'], (name, r)
        (root / (name + '-Result.json')).write_text((root / (name + '-Progress' + str(attempt) + '.json')).read_text(encoding='utf-8'), encoding='utf-8')
        print(name, summary, flush=True)
