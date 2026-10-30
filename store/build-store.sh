#!/bin/sh
# Builds the Microsoft Store edition: same code, but the exe runs as the invoking user (no administrator, no allowElevation capability).
# usage: sh store/build-store.sh 3.4.3.0   -> out/KEMAS_<version>_x64.msix
set -e
cd "$(dirname "$0")/.."
sed 's/requireAdministrator/asInvoker/' res/app.manifest > /tmp/app.store.manifest
sed 's#"app.manifest"#"/tmp/app.store.manifest"#' res/kemas.rc > res/kemas.store.rc
mkdir -p out/store
x86_64-w64-mingw32-windres --preprocessor="$(pwd)/tools/catpp.sh" -i res/kemas.store.rc -O res -o out/kemas.store.res
rm -f res/kemas.store.rc
cp res/kemas.ico src/kemas.ico
(cd src && mcs -sdk:4.8 -langversion:7 -target:winexe -platform:anycpu -optimize+ -out:../out/store/KEMAS.exe -win32res:../out/kemas.store.res \
  -r:../ref/PresentationFramework.dll -r:../ref/PresentationCore.dll -r:../ref/WindowsBase.dll -r:../ref/System.Xaml.dll \
  -r:System.Runtime.Serialization.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Xml.dll -r:System.Core.dll -r:System.Management.dll \
  -resource:Main.xaml,Main.xaml -resource:Scene.xaml,Scene.xaml -resource:kemas.ico,kemas.ico *.cs)
rm -f src/kemas.ico
python3 tools/mkmsix.py "$1" "out/KEMAS_$1_x64.msix" out/store/KEMAS.exe
