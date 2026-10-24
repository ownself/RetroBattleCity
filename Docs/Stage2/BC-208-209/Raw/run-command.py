import datetime, json, pathlib, subprocess, sys, time
root = pathlib.Path('Docs/Stage2/BC-208-209/Raw')
name, *args = sys.argv[1:]
command = ['unity', *args]
start = datetime.datetime.now().astimezone().isoformat()
t = time.monotonic()
p = subprocess.run(command, capture_output=True, timeout=90)
(root / (name + '.json')).write_bytes(p.stdout)
(root / (name + '.stderr')).write_bytes(p.stderr)
with (root / 'commands.jsonl').open('a', encoding='utf-8') as f:
    f.write(json.dumps(dict(start=start, end=datetime.datetime.now().astimezone().isoformat(), argv=command, exit=p.returncode, seconds=time.monotonic()-t))+'\n')
print(p.stdout.decode('utf-8', errors='replace'))
sys.exit(p.returncode)
