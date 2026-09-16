#!/usr/bin/env bash
# Compiles (C#) or parses (Python) every starter under content/questions/*/starters/ using the
# runner images and the compile command the Judge itself is configured with, read straight out of
# DsaPractice.Judge/appsettings.json -- so this check cannot drift from what the sandbox does.
#
# A starter that doesn't build is worse than no starter: the first thing a solver sees is an error
# they didn't cause. Run this after editing one.
#
#   tools/check-starters.sh            # every question
#   tools/check-starters.sh two-sum    # just one
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
settings="$repo_root/source/DsaPractice.Judge/appsettings.json"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

runner() { python3 -c "
import json, sys
runners = json.load(open('$settings'))['Judge']['Sandbox']['Runners']
print(runners['$1'].get('$2', ''))
"; }

csharp_compile_image="$(runner csharp CompileImage)"
csharp_compile_script="$(python3 -c "
import json
runners = json.load(open('$settings'))['Judge']['Sandbox']['Runners']
# The command is ['sh', '-c', '<script>']; the script is what we hand to the container.
print(runners['csharp']['CompileCommand'][-1])
")"
python_image="$(runner python Image)"

failed=0
for directory in "$repo_root"/content/questions/${1:-*}/starters; do
  [ -d "$directory" ] || continue
  slug="$(basename "$(dirname "$directory")")"

  for starter in "$directory"/*; do
    language="$(basename "${starter%.*}")"
    label="$slug/$(basename "$starter")"

    case "$language" in
      csharp)
        rm -rf "$work/in" "$work/out"
        mkdir -p "$work/in" "$work/out"
        cp "$starter" "$work/in/main.cs"
        if output="$(docker run --rm -v "$work/in:/work" -v "$work/out:/out" -w /work \
            "$csharp_compile_image" sh -c "$csharp_compile_script" 2>&1)"; then
          echo "ok    $label"
        else
          echo "FAIL  $label"
          echo "$output" | sed 's/^/        /'
          failed=1
        fi
        ;;
      python)
        if output="$(docker run --rm -i "$python_image" \
            python -c 'import ast, sys; ast.parse(sys.stdin.read())' < "$starter" 2>&1)"; then
          echo "ok    $label"
        else
          echo "FAIL  $label"
          echo "$output" | sed 's/^/        /'
          failed=1
        fi
        ;;
      *)
        echo "skip  $label (no check for '$language')"
        ;;
    esac
  done
done

exit "$failed"
