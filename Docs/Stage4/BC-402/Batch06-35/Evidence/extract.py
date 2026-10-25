import sys
sys.path.insert(0,'Docs/Stage4/BC-402/Batch01-05/Evidence')
from inspect_png import load
from pathlib import Path
from collections import Counter
import hashlib,json
out=[]
for stage in range(6,36):
 p=Path(f'Assets/Sprites/Stages/NES - Battle City (JPN) - Backgrounds - Stage {stage:02}.png');im=load(p); rows=[];patterns={}
 for y in range(26):
  row=''
  for x in range(26):
   pix=[im[y*8+j][x*8+i] for j in range(8) for i in range(8)];colors=set(pix)
   if 12<=x<=13 and 24<=y<=25:k='H'
   elif (137,217,0) in colors:k='G'
   elif (66,64,254) in colors:k='W'
   elif (153,79,0) in colors:k='B'
   elif colors=={(102,102,102),(174,174,174),(254,254,254)} and pix.count((174,174,174))==37:k='I'
   elif (174,174,174) in colors:k='S'
   elif colors=={(0,0,0)}:k='.'
   else:raise Exception((stage,x,y,colors))
   row+=k;patterns.setdefault(k,set()).add(tuple(pix))
  rows.append(row)
 text='\n'.join(rows)+'\n'
 if stage==1:
  assert text==Path('Assets/BattleCity/Editor/Stage01Layout.txt').read_text(), 'Stage01 differs'
 else:Path(f'Assets/BattleCity/Editor/Stage{stage:02}Layout.txt').write_text(text)
 out.append({'stage':stage,'source':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'layoutSha256':hashlib.sha256(text.encode()).hexdigest(),'counts':dict(Counter(''.join(rows))),'patterns':{k:len(v) for k,v in patterns.items()}})
print(json.dumps(out,indent=2));Path('Temp/BC402-Rest/Extraction.json').write_text(json.dumps(out,indent=2))
