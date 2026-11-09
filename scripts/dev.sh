#!/usr/bin/env bash
set -euo pipefail
shadeflow_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
exec python3 "$shadeflow_root/scripts/dev.py" --root "$shadeflow_root" "$@"
