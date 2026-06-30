#!/bin/sh
# Installs the tracked pre-push hook into .git/hooks (not version-controlled).
# Run once per clone:  sh tools/checks/install-hooks.sh
root="$(git rev-parse --show-toplevel)"
cp "$root/tools/checks/pre-push" "$root/.git/hooks/pre-push"
chmod +x "$root/.git/hooks/pre-push"
echo "Installed pre-push hook -> $root/.git/hooks/pre-push"
