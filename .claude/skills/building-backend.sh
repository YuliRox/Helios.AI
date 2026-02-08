#!/bin/bash

# LumiRise Backend Build Skill
# Executes dotnet build with minimal verbosity, outputting only errors
# Usage: building-backend [options]

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$PROJECT_ROOT"

# Run build with minimal verbosity
# -v q = quiet verbosity (only errors shown)
# --logger "console;verbosity=quiet" = minimal console output
dotnet build \
  --configuration Release \
  --verbosity quiet \
  --logger "console;verbosity=quiet" \
  "$@"

exit $?
