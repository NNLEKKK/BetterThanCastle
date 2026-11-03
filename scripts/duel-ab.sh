#!/bin/sh
# usage: duel-ab.sh <featmask> [matches=10] [units=8]  -> runs current bot (feature mask) vs frozen bot v2, prints tally
M=${1:-63}; N=${2:-10}; U=${3:-8}
cd /c/Users/LKFM/Desktop/BTC || exit 1
f=tests/scenarios/duel-abx.txt
{ echo "prof start"; echo "duelunits 4 $U $U"; echo "duelskip 1"; echo "duelheal 0"; echo "duelplayerbot 1 1"; echo "duelfeat $M"; echo "duel"; echo "waitduel 240";
  i=1; while [ $i -lt $N ]; do echo "waitduelover 120"; echo "duelrematch"; echo "wait 4"; i=$((i+1)); done
  echo "waitduelover 120"; echo "wait 1"; echo "dump basic"; echo "quit"; } > $f
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/scenario.ps1 -Name duel-abx > /dev/null 2>&1
echo "feat=$M units=$U"; sh scripts/duel-tally.sh $N | tail -1
