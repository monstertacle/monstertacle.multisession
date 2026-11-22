#!/usr/bin/env bash
set -euo pipefail

fail() {
    printf '%s\n' "$1" >&2
    exit 2
}

root=$(git rev-parse --show-toplevel 2>/dev/null) || fail 'Run inside a Git working tree.'
cd -- "$root" || fail 'Cannot enter the Git working tree.'

if ! command -v gitleaks >/dev/null || [[ $(gitleaks version 2>/dev/null) != 8.30.1 ]]; then
    fail 'Install official Gitleaks 8.30.1 and add it to PATH. See docs/secret-handling.md.'
fi

# Gitleaks loads this file from the repository even with an explicit configuration.
if [[ -e .gitleaksignore || -L .gitleaksignore ]]; then
    fail 'Remove .gitleaksignore before running the required staged scan.'
fi

# Check the index independently of the scanner's Git subprocess.
git diff --cached --no-ext-diff --no-textconv -- . >/dev/null 2>&1 ||
    fail 'Cannot read the staged diff.'

unset GITLEAKS_CONFIG
export GITLEAKS_CONFIG_TOML=$'[extend]\nuseDefault = true'
status=0
# Emit only fixed messages so diagnostics cannot expose sensitive content.
gitleaks git --staged --pre-commit --redact=100 --ignore-gitleaks-allow --exit-code 3 . \
    >/dev/null 2>&1 || status=$?

case "$status" in
    0) printf '%s\n' 'Staged secret scan passed.' ;;
    3) printf '%s\n' 'Potential secret detected in staged changes (details redacted).' >&2; exit 3 ;;
    *) fail 'Gitleaks could not complete the staged scan.' ;;
esac
