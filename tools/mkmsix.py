import sys,zlib,hashlib,base64,os,zipfile,time
ver=sys.argv[1]; out=sys.argv[2]; exe=sys.argv[3]
src=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','store')
B=65536
class BlockCompressor:
    """raw deflate, every 64 KB block ends with a full flush so it can be decoded on its own (needed by the MSIX block map)"""
    def __init__(self): self.c=zlib.compressobj(9,zlib.DEFLATED,-15); self.buf=b''; self.sizes=[]
    def _emit(self,b):
        p=self.c.compress(b)+self.c.flush(zlib.Z_FULL_FLUSH); self.sizes.append(len(p)); return p
    def compress(self,data):
        self.buf+=data; o=b''
        while len(self.buf)>=B: o+=self._emit(self.buf[:B]); self.buf=self.buf[B:]
        return o
    def flush(self):
        o=b''
        if self.buf or not self.sizes: o+=self._emit(self.buf); self.buf=b''
        return o+self.c.flush(zlib.Z_FINISH)
class Sink:
    def __init__(self,f): self.f=f
    def write(self,b): return self.f.write(b)
    def flush(self): pass
files=[('KEMAS.exe',open(exe,'rb').read(),True)]
for n in ['Wide310x150Logo','Square310x310Logo','Square44x44Logo','Square71x71Logo','StoreLogo','Square150x150Logo']:
    files.append(('Assets/%s.png'%n,open('%s/Assets/%s.png'%(src,n),'rb').read(),False))
files.append(('AppxManifest.xml',open(src+'/AppxManifest.template.xml',encoding='utf-8').read().replace('@VERSION@',ver).encode('utf-8'),True))
BC={}
def comp(name,data):
    bc=BlockCompressor(); bc.compress(data); bc.flush(); BC[name]=bc.sizes
bm=['<?xml version="1.0" encoding="UTF-8" standalone="no"?><BlockMap xmlns="http://schemas.microsoft.com/appx/2010/blockmap" HashMethod="http://www.w3.org/2001/04/xmlenc#sha256">']
for name,data,deflate in files:
    blocks=[data[i:i+B] for i in range(0,len(data),B)] or [b'']
    if deflate: comp(name,data)
    x='<File Name="%s" Size="%d" LfhSize="%d">'%(name.replace('/','\\'),len(data),30+len(name.encode()))
    for i,b in enumerate(blocks):
        x+='<Block Hash="%s"'%base64.b64encode(hashlib.sha256(b).digest()).decode()+(' Size="%d"'%BC[name][i] if deflate else '')+'/>'
    bm.append(x+'</File>')
bm.append('</BlockMap>')
bmx=''.join(bm).encode()
ct=b'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default ContentType="application/x-msdownload" Extension="exe"/><Default ContentType="image/png" Extension="png"/><Default ContentType="application/vnd.ms-appx.manifest+xml" Extension="xml"/><Override ContentType="application/vnd.ms-appx.blockmap+xml" PartName="/AppxBlockMap.xml"/></Types>'
allf=files+[('AppxBlockMap.xml',bmx,True),('[Content_Types].xml',ct,True)]
import struct
buf=bytearray(); cd=bytearray(); n=0
DT=(0,0)  # dos time/date fixed below
t=(0<<11)|(22<<5)|(16//2); dte=((2026-1980)<<9)|(10<<5)|18
for name,data,deflate in allf:
    nb=name.encode(); off=len(buf); crc=zlib.crc32(data)&0xffffffff
    if deflate:
        bc=BlockCompressor(); comp=bc.compress(data)+bc.flush(); method=8
    else: comp=data; method=0
    buf+=struct.pack('<IHHHHHIIIHH',0x04034b50,45,8,method,t,dte,0,0,0,len(nb),0)+nb+comp
    buf+=struct.pack('<IIQQ',0x08074b50,crc,len(comp),len(data))
    cd+=struct.pack('<IHHHHHHIIIHHHHHII',0x02014b50,45,45,8,method,t,dte,crc,len(comp),len(data),len(nb),0,0,0,0,0,off)+nb
    n+=1
cdo=len(buf); buf+=cd; z64=len(buf)
buf+=struct.pack('<IQHHIIQQQQ',0x06064b50,44,45,45,0,0,n,n,len(cd),cdo)
buf+=struct.pack('<IIQI',0x07064b50,0,z64,1)
buf+=struct.pack('<IHHHHIIH',0x06054b50,0,0,0xFFFF,0xFFFF,0xFFFFFFFF,0xFFFFFFFF,0)
open(out,'wb').write(buf); print(out,len(buf))
