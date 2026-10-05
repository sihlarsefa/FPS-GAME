#!/bin/zsh
# =====================================================================================
#  FPS-GAME compile/test verifier (Unity editor cannot be launched here).
#  Kullanım (önce bir kez: zsh Tools/UnityVerify/setup_deps.sh): zsh Tools/UnityVerify/verify.sh [OUTDIR] [--tests] [--player]
#    - Compiles assemblies in dependency order against the real Unity 6000.6 engine DLLs,
#      real UGUI + InputSystem (compiled from package sources) and URP signature stubs.
#    - Prints "== <Assembly>: N errors" plus each error line (paths relative to Scripts/).
#    - --tests : also compiles Core+Application+Tests/EditMode with an NUnit shim and RUNS them.
#    - --player: additionally compiles Infrastructure/Presentation WITHOUT UNITY_EDITOR/UnityEditor refs
#                (catches editor-only API used in runtime code, which would break player builds).
#  Use a private OUTDIR per agent to avoid clobbering (e.g. /tmp/.../scratchpad/out_<name>).
# =====================================================================================
T=${0:A:h}
U=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Resources
R=${T:h:h}/Assets/_Project
SC=$R/Scripts
DEPS=$T/.deps
O=${1:-$T/out}
mkdir -p $O
csc() { dotnet "$(ls -d /usr/local/share/dotnet/sdk/*/Roslyn/bincore/csc.dll | tail -1)" "$@"; }

ENGINE=(-r:$U/Scripting/NetStandard/ref/2.1.0/netstandard.dll)
for f in $U/Scripting/Managed/UnityEngine/UnityEngine*.dll; do ENGINE+=(-r:$f); done
EDITOR=()
for f in $U/Scripting/Managed/UnityEngine/UnityEditor*.dll; do EDITOR+=(-r:$f); done
PKG=(-r:$DEPS/UnityEngine.UI.dll -r:$DEPS/Unity.InputSystem.dll -r:$DEPS/Unity.RenderPipelines.Core.Runtime.dll -r:$DEPS/Unity.RenderPipelines.Universal.Runtime.dll)

BASEDEFS="DEBUG;TRACE;UNITY_ASSERTIONS;ENABLE_INPUT_SYSTEM;UNITY_STANDALONE_OSX;UNITY_STANDALONE;NET_STANDARD_2_1;NET_STANDARD;UNITY_6000;UNITY_6000_6;UNITY_2021_3_OR_NEWER;UNITY_2022_3_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_6000_6_OR_NEWER"
EDEFS="$BASEDEFS;UNITY_EDITOR;UNITY_EDITOR_OSX;UNITY_EDITOR_64"
NOWARN="-nowarn:1701,1702,0649,0169,0414,0067"

compile() { # name defs outfile files... (refs passed via global CUR_REFS)
  local name=$1 defs=$2 out=$3; shift 3
  local files=("$@")
  if [ ${#files[@]} -eq 0 ]; then echo "== $name: (no files)"; return; fi
  local res=$(csc -nologo -noconfig -nostdlib -target:library -langversion:9.0 -unsafe $NOWARN -define:"$defs" "${CUR_REFS[@]}" -out:$out "${files[@]}" 2>&1)
  local errs=$(echo "$res" | grep -E "error CS" | sed "s|$SC/||; s|$R/||" | sort -u)
  local n=$(echo -n "$errs" | grep -c "error" )
  echo "== $name: $n errors"
  [ -n "$errs" ] && echo "$errs" | head -${MAXERR:-200}
}

files_in() { find "$@" -name "*.cs" 2>/dev/null | sort; }

CUR_REFS=("${ENGINE[@]}")
compile Project.Core "$BASEDEFS" $O/Project.Core.dll $(files_in $SC/Core)
CUR_REFS=("${ENGINE[@]}" -r:$O/Project.Core.dll)
compile Project.Application "$BASEDEFS" $O/Project.Application.dll $(files_in $SC/Application)
CUR_REFS=("${ENGINE[@]}" "${EDITOR[@]}" "${PKG[@]}" -r:$O/Project.Core.dll -r:$O/Project.Application.dll)
compile Project.Infrastructure "$EDEFS" $O/Project.Infrastructure.dll $(files_in $SC/Infrastructure)
CUR_REFS+=(-r:$O/Project.Infrastructure.dll)
compile Project.Presentation "$EDEFS" $O/Project.Presentation.dll $(files_in $SC/Presentation)
CUR_REFS+=(-r:$O/Project.Presentation.dll)
compile Project.Editor "$EDEFS" $O/Project.Editor.dll $(files_in $SC/Editor)

if [[ " $* " == *" --player "* ]]; then
  mkdir -p $O/player
  CUR_REFS=("${ENGINE[@]}" "${PKG[@]}" -r:$O/Project.Core.dll -r:$O/Project.Application.dll)
  compile "Project.Infrastructure[player]" "$BASEDEFS" $O/player/Project.Infrastructure.dll $(files_in $SC/Infrastructure)
  CUR_REFS+=(-r:$O/player/Project.Infrastructure.dll)
  compile "Project.Presentation[player]" "$BASEDEFS" $O/player/Project.Presentation.dll $(files_in $SC/Presentation)
fi

if [[ " $* " == *" --tests "* ]]; then
  NR=$(ls -d /usr/local/share/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net* | tail -1)
  TREFS=(); for f in $NR/*.dll; do TREFS+=(-r:$f); done
  mkdir -p $O/tests
  res=$(csc -nologo -noconfig -nostdlib -target:exe -langversion:9.0 $NOWARN -define:"$BASEDEFS;UNITY_INCLUDE_TESTS" "${TREFS[@]}" -out:$O/tests/TestRunner.dll \
      $T/testshim/NUnitShim.cs $(files_in $SC/Core $SC/Application $R/Tests/EditMode) 2>&1)
  errs=$(echo "$res" | grep -E "error CS" | sed "s|$SC/||; s|$R/||" | sort -u)
  echo "== Tests(compile): $(echo -n "$errs" | grep -c error) errors"; [ -n "$errs" ] && echo "$errs" | head -100
  if [ -z "$errs" ]; then
    echo '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"'$(basename $(dirname $(dirname $NR)))'"}}}' > $O/tests/TestRunner.runtimeconfig.json
    dotnet $O/tests/TestRunner.dll $TESTFILTER 2>&1 | tail -60
  fi
fi
