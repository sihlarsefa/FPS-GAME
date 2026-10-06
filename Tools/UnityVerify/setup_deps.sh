#!/bin/zsh
# Bir kez çalıştır: UGUI + Input System (paket kaynaklarından) ve URP imza stub'larını .deps/ altına derler.
T=${0:A:h}
U=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Resources
P=$U/PackageManager
D=$T/.deps
mkdir -p $D/pkgs
csc() { dotnet "$(ls -d /usr/local/share/dotnet/sdk/*/Roslyn/bincore/csc.dll | tail -1)" "$@"; }
REFS=(-r:$U/Scripting/NetStandard/ref/2.1.0/netstandard.dll)
for f in $U/Scripting/Managed/UnityEngine/*.dll; do REFS+=(-r:$f); done
DEFS="DEBUG;TRACE;UNITY_STANDALONE_OSX;UNITY_STANDALONE;ENABLE_INPUT_SYSTEM;ENABLE_MONO;NET_STANDARD_2_1;NET_STANDARD;UNITY_6000;UNITY_6000_6"
for v in 5_3 5_4 5_5 5_6 2017_1 2017_2 2017_3 2017_4 2018_1 2018_2 2018_3 2018_4 2019_1 2019_2 2019_3 2019_4 2020_1 2020_2 2020_3 2021_1 2021_2 2021_3 2022_1 2022_2 2022_3 2023_1 2023_2 2023_3 6000_0 6000_1 6000_2 6000_3 6000_4 6000_5 6000_6; do DEFS="$DEFS;UNITY_${v}_OR_NEWER"; done
echo "UGUI..."
csc -nologo -noconfig -nostdlib -target:library -langversion:9.0 -nowarn:1701,1702,0618,0649,0169,0414,0067 -define:"$DEFS;PACKAGE_PHYSICS;PACKAGE_PHYSICS2D;PACKAGE_ANIMATION" \
  "${REFS[@]}" -out:$D/UnityEngine.UI.dll $(find $P/BuiltInPackages/com.unity.ugui/Runtime/UGUI -name "*.cs") 2>&1 | grep "error CS" | head
echo "Input System..."
rm -rf $D/pkgs/inputsystem; (cd $D/pkgs && tar xzf $P/Editor/com.unity.inputsystem-1.20.0.tgz && mv package inputsystem)
csc -nologo -noconfig -nostdlib -target:library -langversion:9.0 -unsafe -nowarn:1701,1702,0618,0649,0169,0414,0067 \
  -define:"$DEFS;UNITY_INPUT_SYSTEM_ENABLE_UI;UNITY_INPUT_SYSTEM_ENABLE_PHYSICS;UNITY_INPUT_SYSTEM_ENABLE_PHYSICS2D;UNITY_INPUT_SYSTEM_PLATFORM_SCROLL_DELTA;UNITY_INPUT_SYSTEM_INPUT_MODULE_SCROLL_DELTA;UNITY_INPUT_SYSTEM_INPUT_MODULE_NAVIGATION_DEVICE_TYPE;UNITY_INPUT_SYSTEM_SENDPOINTERHOVERTOPARENT;UNITY_INPUT_SYSTEM_PLATFORM_POLLING_FREQUENCY;UNITY_INPUTSYSTEM_SUPPORTS_MOUSE_SCRIPT_EVENTS;UNITY_INPUTSYSTEM_SUPPORTS_FOCUS_EVENTS" \
  "${REFS[@]}" -r:$D/UnityEngine.UI.dll -out:$D/Unity.InputSystem.dll $(find $D/pkgs/inputsystem/InputSystem -name "*.cs" | grep -v "/Plugins/InputForUI/" | grep -v "/InputSystem/Editor/") 2>&1 | grep "error CS" | head
echo "URP stubs..."
csc -nologo -noconfig -nostdlib -target:library -langversion:9.0 "${REFS[@]}" -out:$D/Unity.RenderPipelines.Core.Runtime.dll $T/stubs/UrpCoreStub.cs 2>&1 | grep "error CS" | head
csc -nologo -noconfig -nostdlib -target:library -langversion:9.0 "${REFS[@]}" -r:$D/Unity.RenderPipelines.Core.Runtime.dll -out:$D/Unity.RenderPipelines.Universal.Runtime.dll $T/stubs/UrpStub.cs $T/stubs/UrpRenderGraphStub.cs 2>&1 | grep "error CS" | head
ls -la $D/*.dll
