#!/bin/zsh
# Compiles Scripts/Online (excl. Netcode + Tests) against verify.sh outputs.
# Usage: zsh verify_online.sh [OUTDIR]   (OUTDIR must already hold verify.sh output; default out_online)
T=${0:A:h}
U=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Resources
SC=${T:h:h}/Assets/_Project/Scripts
DEPS=$T/.deps
O=${1:-$T/out_online}
[ -f $O/Project.Presentation.dll ] || { echo "Önce: zsh $T/verify.sh $O"; exit 2; }
csc() { dotnet "$(ls -d /usr/local/share/dotnet/sdk/*/Roslyn/bincore/csc.dll | tail -1)" "$@"; }
REFS=(-r:$U/Scripting/NetStandard/ref/2.1.0/netstandard.dll)
for f in $U/Scripting/Managed/UnityEngine/UnityEngine*.dll; do REFS+=(-r:$f); done
REFS+=(-r:$DEPS/UnityEngine.UI.dll -r:$DEPS/Unity.InputSystem.dll)
for n in Core Application Infrastructure Presentation; do REFS+=(-r:$O/Project.$n.dll); done
DEFS="DEBUG;TRACE;UNITY_ASSERTIONS;ENABLE_INPUT_SYSTEM;UNITY_STANDALONE_OSX;UNITY_STANDALONE;NET_STANDARD_2_1;UNITY_6000;UNITY_6000_6;UNITY_2021_3_OR_NEWER"
FILES=($(find $SC/Online -name '*.cs' -not -path '*/Netcode/*' -not -path '*/Tests/*'))
res=$(csc -nologo -noconfig -nostdlib -target:library -langversion:9.0 -nowarn:1701,1702,0649,0169,0414,0067 -define:"$DEFS" "${REFS[@]}" -out:$O/Project.Online.dll "${FILES[@]}" 2>&1)
errs=$(echo "$res" | grep -E "error CS" | sed "s|$SC/||" | sort -u)
n=$(echo -n "$errs" | grep -c error)
echo "== Project.Online: $n errors"
[ -n "$errs" ] && echo "$errs" | head -100
[ "$n" -eq 0 ]
