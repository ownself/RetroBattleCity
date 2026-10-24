import subprocess, json, sys, time, datetime
from pathlib import Path
ROOT=Path('H:/Projects/Unities/BattleCity')
OUT=ROOT/'Docs/Stage2/BC-206'
def run(name,args,timeout=45):
    split=args.index('--') if '--' in args else len(args)
    argv=['unity']+args[:split]+(['--caller','plugin','--skill','unity-cli'] if args[0]=='command' else [])+['--project-path',str(ROOT),'--format','json']+args[split:]
    started=datetime.datetime.now(datetime.timezone.utc).isoformat(); t=time.monotonic()
    r=subprocess.run(argv,cwd=ROOT,capture_output=True,timeout=timeout)
    (OUT/(name+'.json')).write_bytes(r.stdout); (OUT/(name+'.stderr')).write_bytes(r.stderr)
    with (OUT/'Recheck-Commands.jsonl').open('a',encoding='utf8') as f: f.write(json.dumps({'argv':argv,'exit':r.returncode,'seconds':round(time.monotonic()-t,3),'startedUtc':started,'evidence':name})+'\n')
    d=json.loads(r.stdout.decode('utf-8-sig')); print(name, json.dumps(d,ensure_ascii=False))
    if r.returncode or not d.get('success') or (isinstance(d.get('data'),dict) and d['data'].get('success') is False): raise RuntimeError('command failed '+name)
    return d
if __name__=='__main__': run(sys.argv[1],sys.argv[2:])
