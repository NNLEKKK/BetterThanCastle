#!/bin/sh
# usage: duel-analyze.sh <dir under duel-logs, e.g. par0|.> ...   per-side metrics over all matches in the dirs
cd /c/Users/LKFM/Desktop/BTC/game/csmod/BetterThanCastle/duel-logs || exit 1
for d in "$@"; do
 for f in $d/duel-*.txt; do
  awk -F'|' '
   $2=="UNIT" { split($3,a," "); s[a[1]]=substr(a[2],2) }
   $2=="HIT" { split($3,a," "); split(a[1],u,">"); sd=s[u[1]]; hits[sd]++; for(i=2;i<=length(a);i++){ if(a[i]~/^dmg=/) dmg[sd]+=substr(a[i],5); if(a[i]~/^gap=/){g=substr(a[i],5)+0; if(g>0){gs[sd]+=g; gn[sd]++}} if(a[i]~/^dist=/){ds[sd]+=substr(a[i],6)} } }
   $2=="KILL" && !fb { fb=1; split($3,a," "); fbv=a[1]; fbs=substr(a[2],2); fbt=$1 }
   $2=="KILL" { split($3,a," "); kills[substr(a[2],2)]++ }
   $2=="SNAP" && $1>11 && (fb==0 || $1<fbt) { n=split($3,p," "); for(i=1;i<=n;i++){ split(p[i],q,":"); split(q[3],xy,","); id="u"q[1]; if(id in lx){ dx=xy[1]-lx[id]; dz=xy[2]-lz[id]; if(dx*dx+dz*dz<0.01){ idle[s[id]]+=0.2 } } lx[id]=xy[1]; lz[id]=xy[2] } }
   $2=="END" { res=$3 }
   END { printf "%s fb_side=%s fb_t=%s | hits0=%d hits1=%d avgGap0=%d avgGap1=%d avgDist0=%.2f avgDist1=%.2f | idleSec0=%.1f idleSec1=%.1f | %s\n", FILENAME, fbs, fbt, hits[0], hits[1], gn[0]?gs[0]/gn[0]:0, gn[1]?gs[1]/gn[1]:0, hits[0]?ds[0]/hits[0]:0, hits[1]?ds[1]/hits[1]:0, idle[0], idle[1], substr(res,1,60) }
  ' $f
 done
done
