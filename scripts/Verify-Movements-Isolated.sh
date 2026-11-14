#!/usr/bin/env bash
set -euo pipefail
# Creates and removes only its own temporary PostgreSQL cluster; never reads appsettings.json.
repo_root=$(cd "$(dirname "$0")/.." && pwd)
pg_bin=${STOCKFLOW_PG_BIN:-/Applications/Postgres.app/Contents/Versions/18/bin}
stockflow_dotnet=${STOCKFLOW_DOTNET:-dotnet}
cluster_dir=$(mktemp -d "${TMPDIR:-/tmp}/stockflow-se8.XXXXXX")
pg_port=$(python3 - <<'PY'
import socket
with socket.socket() as s:
    s.bind(('127.0.0.1',0))
    print(s.getsockname()[1])
PY
)
cleanup() {
    "$pg_bin/pg_ctl" -D "$cluster_dir/data" -m immediate stop >/dev/null 2>&1 || true
    rm -rf -- "$cluster_dir"
}
trap cleanup EXIT
"$pg_bin/initdb" -D "$cluster_dir/data" --auth=trust --encoding=UTF8 --locale=C > "$cluster_dir/initdb.log"
"$pg_bin/pg_ctl" -D "$cluster_dir/data" -l "$cluster_dir/server.log" -o "-h 127.0.0.1 -p $pg_port -k $cluster_dir" start
"$pg_bin/createdb" -h 127.0.0.1 -p "$pg_port" stockflow_isolated_se8
"$pg_bin/psql" -h 127.0.0.1 -p "$pg_port" -d stockflow_isolated_se8 -v ON_ERROR_STOP=1 -f "$repo_root/database/01_schema.sql" -f "$repo_root/database/02_seed.sql"
cd "$repo_root"
STOCKFLOW_TEST_CONNECTION="Host=127.0.0.1;Port=$pg_port;Database=stockflow_isolated_se8;Username=$(id -un);Pooling=false" \
    "$stockflow_dotnet" run --project tests/StockFlow.MovementChecks -c Release
