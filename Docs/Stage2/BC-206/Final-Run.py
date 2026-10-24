import subprocess, json, sys, time
from pathlib import Path
ROOT=Path('H:/Projects/Unities/BattleCity')
OUT=ROOT/'Docs/Stage2/BC-206'
def run(name,args,timeout=45):
    split=args.index('--') if '--' in args else len(args)
    argv=['unity']+args[:split]
    if args[0]=='command': argv+=['--caller','plugin','--skill','unity-cli']
    argv+=['--project-path',str(ROOT),'--format','json']+args[split:]
    t=time.monotonic()
    try:
        r=subprocess.run(argv,cwd=ROOT,capture_output=True,timeout=timeout)
    except subprocess.TimeoutExpired as e:
        (OUT/(name+'.json')).write_bytes(e.stdout or b'')
        (OUT/(name+'.stderr')).write_bytes(e.stderr or b'')
        raise
    (OUT/(name+'.json')).write_bytes(r.stdout)
    (OUT/(name+'.stderr')).write_bytes(r.stderr)
    with (OUT/'Final-Commands.jsonl').open('a',encoding='utf8') as f: f.write(json.dumps({'argv':argv,'exit':r.returncode,'seconds':round(time.monotonic()-t,3),'evidence':name})+'\n')
    d=json.loads(r.stdout.decode('utf-8-sig'))
    print(name,'exit',r.returncode,json.dumps(d,ensure_ascii=False)[:6000])
    if r.returncode or not d.get('success'): raise RuntimeError('command failed: '+name)
    if isinstance(d.get('data'),dict) and d['data'].get('success') is False: raise RuntimeError('nested command failed: '+name)
    return d
if __name__=='__main__': run(sys.argv[1],sys.argv[2:])
