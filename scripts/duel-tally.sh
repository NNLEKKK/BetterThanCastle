#!/bin/sh
# usage: duel-tally.sh N  -> summary of the last N duel logs (bot = side 1 = winner "bot")
cd /c/Users/LKFM/Desktop/BTC/game/csmod/BetterThanCastle/duel-logs || exit 1
b=0; p=0; o=0
for f in $(ls -tr | tail -$1); do
  r=$(grep -a "|END|" $f | grep -v deactivate | head -1)
  case "$r" in *winner=bot*) b=$((b+1));; *winner=player*) p=$((p+1));; *) o=$((o+1));; esac
  echo "$f $(echo $r | cut -c1-150)"
done
echo "TALLY bot=$b player(side0)=$p other=$o"
