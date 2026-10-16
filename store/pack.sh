#!/bin/sh
# Builds the Microsoft Store package (unsigned - the Store signs it) from out/KEMAS.exe.
# Needs makemsix from https://github.com/microsoft/msix-packaging (built with --pack).
set -e
cd "$(dirname "$0")"
MAKEMSIX="${MAKEMSIX:-makemsix}"
VER=$(grep -o 'Version = "[0-9.]*"' ../src/Core.cs | grep -o '[0-9.]*')
rm -rf layout && mkdir -p layout/Assets ../out
cp ../out/KEMAS.exe layout/
cp Assets/*.png layout/Assets/
sed "s/@VERSION@/$VER.0/" AppxManifest.template.xml > layout/AppxManifest.xml
rm -f "../out/KEMAS_$VER.0_x64.msix"
"$MAKEMSIX" pack -d layout -p "../out/KEMAS_$VER.0_x64.msix"
echo "Built out/KEMAS_$VER.0_x64.msix"
