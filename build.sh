#!/bin/sh
# Build KEMAS.exe on Linux with Mono.
# Needs: mono-mcs, binutils-mingw-w64-x86-64, and Windows WPF reference DLLs in ./ref
# (copy PresentationCore, PresentationFramework, WindowsBase, System.Xaml .dll from
#  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF on a Windows PC - not committed, they are Microsoft files)
set -e
cd "$(dirname "$0")"
mkdir -p out
x86_64-w64-mingw32-windres --preprocessor="$(pwd)/tools/catpp.sh" -i res/kemas.rc -O res -o out/kemas.res
cp res/kemas.ico src/kemas.ico
cd src
mcs -sdk:4.8 -langversion:7 -target:winexe -platform:anycpu -optimize+ -out:../out/KEMAS.exe -win32res:../out/kemas.res \
  -r:../ref/PresentationFramework.dll -r:../ref/PresentationCore.dll -r:../ref/WindowsBase.dll -r:../ref/System.Xaml.dll \
  -r:System.Runtime.Serialization.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Xml.dll -r:System.Core.dll \
  -resource:Main.xaml,Main.xaml -resource:Scene.xaml,Scene.xaml -resource:kemas.ico,kemas.ico *.cs
rm -f kemas.ico
cd ..
sha256sum out/KEMAS.exe | cut -d' ' -f1 > out/KEMAS.exe.sha256
echo "Built out/KEMAS.exe  sha256=$(cat out/KEMAS.exe.sha256)"
