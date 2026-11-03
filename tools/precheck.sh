#!/bin/sh
# fails on duplicate x:Name, malformed Thickness values, invalid XAML and font sizes off the type scale
d=$(grep -o 'x:Name="[A-Za-z0-9_]*"' src/Main.xaml | grep -v 'x:Name="\(bd\|hover\|sel\|ring\|bar\|seg\|ic\)"\|PART_' | sort | uniq -d)
[ -n "$d" ] && { echo "DUPLICATE NAMES: $d"; exit 1; }
t=$(grep -nE '(Margin|Padding|BorderThickness|CornerRadius)="[^"{]*"' src/Main.xaml | grep -vE '="(-?[0-9.]+)(,-?[0-9.]+){0,1}"|="(-?[0-9.]+)(,-?[0-9.]+){3}"')
[ -n "$t" ] && { echo "BAD THICKNESS: $t"; exit 1; }
(cd tests && mono xc.exe ../src/Main.xaml | grep -q OK) || { echo XAML CHECK FAILED; exit 1; }
python3 tools/typescale.py check src/Main.xaml src/*.cs || { echo TYPE SCALE CHECK FAILED; exit 1; }
echo PRECHECK OK
