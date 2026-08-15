import json,os,re,zipfile,sys
BASE=[p for p in __import__("glob").glob("/sessions/*/mnt/Revit26_Plugin")][0]
M=json.loads(open("/tmp/renmap.json").read())
log=[]
for o in M:
    zp=os.path.join(BASE,o["zip"].replace("/",os.sep))
    if not os.path.exists(zp):
        log.append(("MISSING",o["zip"])); continue
    dest=os.path.dirname(zp)
    try:
        with zipfile.ZipFile(zp) as z: z.extractall(dest)
    except Exception as e:
        log.append(("ERR",o["zip"],str(e))); continue
    root=os.path.join(dest,o["root"])
    ren=0
    toks=o["tokens"]; tool=o["tool"]; ov=o["oldver"]; nv=o["newver"]
    for dp,dn,fn in os.walk(root,topdown=False):
        for name in fn+dn:
            new=name
            for t in toks: new=re.sub(re.escape(t),tool,new)
            new=re.sub(r"(?i)"+re.escape(ov)+r"(?![0-9])",nv,new)
            if new!=name:
                s=os.path.join(dp,name); d=os.path.join(dp,new)
                if not os.path.exists(d):
                    os.rename(s,d); ren+=1; log.append(("REN",o["root"],name,new))
    log.append(("OK",o["root"],ren))
print(json.dumps(log))
