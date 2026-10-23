import sys,struct,zlib,hashlib,base64,os
ver=sys.argv[1]; out=sys.argv[2]; exe=sys.argv[3]
src=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','store')
files=[('KEMAS.exe',open(exe,'rb').read(),True)]
for n in ['Wide310x150Logo','Square310x310Logo','Square44x44Logo','Square71x71Logo','StoreLogo','Square150x150Logo']:
    files.append((f'Assets/{n}.png',open(f'{src}/Assets/{n}.png','rb').read(),False))
man=open(f'{src}/AppxManifest.template.xml',encoding='utf-8').read().replace('@VERSION@',ver).encode('utf-8')
files.append(('AppxManifest.xml',man,True))
B=65536
def esc(s): return s
bm=['<?xml version="1.0" encoding="UTF-8" standalone="no"?><BlockMap xmlns="http://schemas.microsoft.com/appx/2010/blockmap" HashMethod="http://www.w3.org/2001/04/xmlenc#sha256">']
entries=[]  # name, data(raw bytes in zip), method, crc, usize
for name,data,deflate in files:
    blocks=[data[i:i+B] for i in range(0,len(data),B)] or [b'']
    attrs=[]; comp=b''
    if deflate:
        c=zlib.compressobj(9,zlib.DEFLATED,-15)
        for i,b in enumerate(blocks):
            part=c.compress(b)
            part+= c.flush(zlib.Z_FULL_FLUSH) if i<len(blocks)-1 else c.flush(zlib.Z_FINISH)
            comp+=part; attrs.append(len(part))
    else: comp=data
    lfh=30+len(name.encode())
    s=f'<File Name="{name.replace("/","\\\\")}" Size="{len(data)}" LfhSize="{lfh}">'
    for i,b in enumerate(blocks):
        h=base64.b64encode(hashlib.sha256(b).digest()).decode()
        s+=f'<Block Hash="{h}"'+(f' Size="{attrs[i]}"' if deflate else '')+'/>'
    s+='</File>'; bm.append(s)
    entries.append((name,comp,8 if deflate else 0,zlib.crc32(data)&0xffffffff,len(data)))
bm.append('</BlockMap>')
bmx=''.join(bm).encode('utf-8')
ct=b'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default ContentType="application/x-msdownload" Extension="exe"/><Default ContentType="image/png" Extension="png"/><Default ContentType="application/vnd.ms-appx.manifest+xml" Extension="xml"/><Override ContentType="application/vnd.ms-appx.blockmap+xml" PartName="/AppxBlockMap.xml"/></Types>'
for name,data in [('AppxBlockMap.xml',bmx),('[Content_Types].xml',ct)]:
    c=zlib.compressobj(9,zlib.DEFLATED,-15); comp=c.compress(data)+c.flush()
    entries.append((name,comp,8,zlib.crc32(data)&0xffffffff,len(data)))
buf=bytearray(); cd=bytearray()
for name,comp,method,crc,usize in entries:
    nb=name.encode(); off=len(buf)
    buf+=struct.pack('<IHHHHHIIIHH',0x04034b50,20,0,method,0,0x21,crc,len(comp),usize,len(nb),0)+nb+comp
    cd+=struct.pack('<IHHHHHHIIIHHHHHII',0x02014b50,20,20,0,method,0,0x21,crc,len(comp),usize,len(nb),0,0,0,0,0,off)+nb
cdo=len(buf); buf+=cd
buf+=struct.pack('<IHHHHIIH',0x06054b50,0,0,len(entries),len(entries),len(cd),cdo,0)
open(out,'wb').write(buf); print(out,len(buf))
