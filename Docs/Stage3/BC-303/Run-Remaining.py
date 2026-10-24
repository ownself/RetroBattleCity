"""Resume only failed Sprite assertions and classes not reached; never replay prior green classes."""
import json
import pathlib
import subprocess
import time
root = pathlib.Path(__file__).parent

def command(args, name):
    split = args.index('--') if '--' in args else len(args)
    argv = ['unity', 'command', *args[:split], '--caller', 'plugin', '--skill', 'unity-cli', '--format', 'json', *args[split:]]
    p = subprocess.run(argv, capture_output=True, encoding='utf-8', timeout=25)
    (root / name).write_text(p.stdout, encoding='utf-8')
    assert p.returncode == 0, (name, p.stdout, p.stderr)
    return json.loads(p.stdout)['data']['result']

compile_result = json.loads((root / 'Compile-ViewAssertion-Result.json').read_text(encoding='utf-8'))['data']['result']
assert compile_result['status'] == 'completed' and not compile_result['failed']
old = json.loads((root / 'EnemyTankViewTests-Progress0.json').read_text(encoding='utf-8'))['data']['result']
filters = list(dict.fromkeys(r['FullName'].split('(')[0] for r in old['results'] if r['Status'] != 'Passed'))
targets = [(name, 'View-Retest-' + str(i)) for i, name in enumerate(filters)]
targets += [('BattleCity.Tests.' + name + '.', name) for name in ['PlayerUpgradePlayModeTests', 'PlayerLifeIntegrationPlayModeTests', 'SessionLifecyclePlayModeTests']]
for name, file in targets:
    command(['test', 'run', '--', '--mode', 'playmode', '--filter', name, '--async_tests', 'true', '--timeout', '60'], file + '-Run.json')
    for attempt in range(6):
        time.sleep(1)
        r = command(['test', 'status'], file + '-Progress' + str(attempt) + '.json')
        if r.get('status') != 'running': break
    assert r.get('status') == 'completed', (name, r)
    summary = r['summary']
    assert summary['total'] > 0 and summary['passed'] == summary['total'], (name, r)
    (root / (file + '-Result.json')).write_text((root / (file + '-Progress' + str(attempt) + '.json')).read_text(encoding='utf-8'), encoding='utf-8')
    print(name, summary, flush=True)
