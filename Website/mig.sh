#! /bin/bash

#!/usr/bin/env bash
set -e

if [ -z "$1" ]; then
  echo "Usage: $0 <MigrationName>"
  exit 1
fi

MIGRATION_NAME="$1"

dotnet ef migrations add "$MIGRATION_NAME" \
  -o ./Data/Migrations/

