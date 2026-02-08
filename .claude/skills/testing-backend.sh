#!/bin/bash

# LumiRise Backend Test Skill
# Executes dotnet test with minimal verbosity, outputting only errors
# Usage: test-backend [options]

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$PROJECT_ROOT"

# Run tests with minimal verbosity
# -v q = quiet verbosity (only errors shown)
# --logger "console;verbosity=quiet" = minimal console output
dotnet test \
  --configuration Release \
  --verbosity quiet \
  --logger "console;verbosity=quiet" \
  --no-build \
  "$@"

exit $?
