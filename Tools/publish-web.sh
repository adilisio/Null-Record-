#!/bin/sh
# Publishes Builds/WebGL to the gh-pages branch; GitHub Pages serves it at
# https://adilisio.github.io/Null-Record-/ (live within a minute or two).
# Run from the repo root after `Tools/unity-batch.ps1 web`. Force-pushes gh-pages (it only holds the build).
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/Builds/WebGL"
WORK="$ROOT/Logs/gh-pages"
REMOTE="$(git -C "$ROOT" remote get-url origin)"
[ -f "$SRC/index.html" ] || { echo "No build at $SRC; run Tools/unity-batch.ps1 web first."; exit 1; }

rm -rf "$WORK"; mkdir -p "$WORK"
cp -r "$SRC"/. "$WORK"/
touch "$WORK/.nojekyll"   # stop Jekyll from touching Unity's files
cd "$WORK"
git init -q -b gh-pages
git add -A
git -c user.name="$(git -C "$ROOT" config user.name)" -c user.email="$(git -C "$ROOT" config user.email)" \
    commit -q -m "Web build of Null Record ($(git -C "$ROOT" rev-parse --short HEAD))"
git push -f "$REMOTE" gh-pages 2>&1 | tail -2
echo "Published. Check status: gh api repos/adilisio/Null-Record-/pages --jq .status"
