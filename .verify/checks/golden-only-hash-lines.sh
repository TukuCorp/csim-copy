#!/usr/bin/env bash
# Passes only if the golden differs from HEAD in exactly two "BotAction log" lines,
# each keeping its action count and changing only the sha256.
set -u
g=tests/CarbonSim.Engine.Tests/Golden/fidelity-golden.md
diff=$(git diff HEAD -U0 -- "$g" | grep -E '^[-+]' | grep -vE '^(---|\+\+\+) ')
removed=$(printf '%s\n' "$diff" | grep -E '^-' || true)
added=$(printf '%s\n' "$diff" | grep -E '^\+' || true)
nr=$(printf '%s' "$removed" | grep -c . || true)
na=$(printf '%s' "$added" | grep -c . || true)
echo "removed=$nr added=$na"
[ "$nr" = 2 ] && [ "$na" = 2 ] || { echo "GOLDEN_CHECK_FAIL: expected 2/2 changed lines"; exit 1; }
other=$(printf '%s\n%s\n' "$removed" "$added" | grep -vE '^[-+]- BotAction log: [0-9,]+ actions, sha256 [0-9a-f]{64}$' || true)
[ -z "$other" ] || { echo "GOLDEN_CHECK_FAIL: non-hash line changed:"; echo "$other"; exit 1; }
rc=$(printf '%s\n' "$removed" | sed -E 's/.*log: ([0-9,]+) actions.*/\1/')
ac=$(printf '%s\n' "$added" | sed -E 's/.*log: ([0-9,]+) actions.*/\1/')
[ "$rc" = "$ac" ] || { echo "GOLDEN_CHECK_FAIL: action counts changed"; exit 1; }
echo "GOLDEN_CHECK_OK: only BotAction sha256 lines changed, counts kept"
