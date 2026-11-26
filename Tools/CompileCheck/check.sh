#!/usr/bin/env sh
# Compiles Game.Runtime and Game.Editor outside Unity as a quick error check.
cd "$(dirname "$0")" && dotnet build Editor.csproj -nologo -v q -clp:NoSummary "$@"
