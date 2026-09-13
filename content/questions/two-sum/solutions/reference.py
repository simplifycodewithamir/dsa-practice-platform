import sys
d=sys.stdin.read().split()
n,t=int(d[0]),int(d[1]); a=list(map(int,d[2:2+n]))
seen={}
for i,x in enumerate(a):
    if t-x in seen: print(seen[t-x], i); break
    seen.setdefault(x,i)
