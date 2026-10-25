import struct,zlib,collections,pathlib

def load(p):
 b=pathlib.Path(p).read_bytes(); pos=8; data=b'';pal=None
 while pos<len(b):
  n=struct.unpack('>I',b[pos:pos+4])[0];t=b[pos+4:pos+8];d=b[pos+8:pos+8+n];pos+=n+12
  if t==b'IHDR':w,h,depth,kind,_,_,inter=struct.unpack('>IIBBBBB',d)
  if t==b'PLTE':pal=[tuple(d[i:i+3]) for i in range(0,len(d),3)]
  if t==b'IDAT':data+=d
 assert depth==8 and inter==0,(depth,kind,inter)
 channels={2:3,3:1,6:4}[kind];stride=w*channels;raw=zlib.decompress(data);rows=[];prev=bytearray(stride);off=0
 for y in range(h):
  f=raw[off];off+=1;r=bytearray(raw[off:off+stride]);off+=stride
  for i in range(stride):
   a=r[i-channels] if i>=channels else 0;c=prev[i-channels] if i>=channels else 0;v=prev[i]
   if f==1:r[i]=(r[i]+a)&255
   if f==2:r[i]=(r[i]+v)&255
   if f==3:r[i]=(r[i]+(a+v)//2)&255
   if f==4:
    q=a+v-c;aa=abs(q-a);bb=abs(q-v);cc=abs(q-c);r[i]=(r[i]+(a if aa<=bb and aa<=cc else v if bb<=cc else c))&255
  rows.append([pal[r[x]] if kind==3 else tuple(r[x*channels:x*channels+3]) for x in range(w)]);prev=r
 return rows
if __name__=='__main__':
 for p in sorted(pathlib.Path('Assets/Sprites/Stages').glob('*.png'))[:5]:
  rows=load(p);print(p.name,collections.Counter(c for row in rows for c in row))
