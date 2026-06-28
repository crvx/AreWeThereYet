#!/bin/bash
set -e
VERSION=$(git describe --tags --always 2>/dev/null || echo "dev")
OUTPUT="AreWeThereYet-${VERSION}.zip"
TEMP=$(mktemp -d)

msbuild AreWeThereYet/AreWeThereYet.csproj /p:Configuration=Release /t:Build /nologo

mkdir -p "${TEMP}/GameData/AreWeThereYet/Plugins"
mkdir -p "${TEMP}/GameData/AreWeThereYet/Textures"

cp AreWeThereYet/bin/Release/AreWeThereYet.dll "${TEMP}/GameData/AreWeThereYet/Plugins/"
cp GameData/AreWeThereYet/Textures/*.png "${TEMP}/GameData/AreWeThereYet/Textures/" 2>/dev/null || true
cp GameData/AreWeThereYet/Colors.cfg "${TEMP}/GameData/AreWeThereYet/"
cp README.md LICENSE "${TEMP}/GameData/AreWeThereYet/"

cd "${TEMP}"
zip -r "${OLDPWD}/${OUTPUT}" GameData/
cd "${OLDPWD}"
rm -rf "${TEMP}"
echo "Created ${OUTPUT}"
