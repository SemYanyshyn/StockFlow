#!/usr/bin/env python3
"""Create a fresh disposable PostgreSQL cluster, run explicit checks, always stop it.

Never connects to the user's configured database. Requires local PostgreSQL
binaries and .NET8. Does not run in the normal Solution or unit-test CI.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shlex
import socket
import subprocess
import tempfile
import uuid

parser=argparse.ArgumentParser()
parser.add_argument('--pg-bin',type=Path,required=True)
parser.add_argument('--dotnet',default='dotnet')
parser.add_argument('--schema-ref',default='5e1a9101d4b89b18e9d64667a15d856b743d1f45')
args=parser.parse_args()
repo=Path(__file__).resolve().parents[2]
tools={name:args.pg_bin/name for name in ('initdb','pg_ctl','createdb','psql','postgres')}
for path in tools.values():
    if not path.is_file(): raise SystemExit('Missing PostgreSQL executable: '+str(path))
version=subprocess.check_output([str(tools['postgres']),'--version'],text=True).strip()
# PostgreSQL Unix socket paths have a short length limit; macOS TMPDIR is too long.
base='/private/tmp' if Path('/private/tmp').is_dir() else None
workspace=Path(tempfile.mkdtemp(prefix='stockflow-se6-verify-',dir=base))
data=workspace/'data'
socket_dir=workspace/'socket'
socket_dir.mkdir()
suffix=uuid.uuid4().hex[:10]
databases=['stockflow_se6_test_'+suffix,'stockflow_se6_concurrency_'+suffix]
with socket.socket() as available:
    available.bind(('127.0.0.1',0))
    port=available.getsockname()[1]
snapshot=workspace/'schema'
snapshot.mkdir()
hashes={}
for name in ('01_schema.sql','02_seed.sql'):
    contents=subprocess.check_output(['git','show',args.schema_ref+':database/'+name],cwd=repo)
    (snapshot/name).write_bytes(contents)
    hashes[name]=hashlib.sha256(contents).hexdigest()
metadata={'bin':str(args.pg_bin),'host':'127.0.0.1','port':port,'username':'stockflow_test',
          'source_commit':args.schema_ref,'sha256':hashes,'postgres_version':version}
started=False
try:
    subprocess.run([str(tools['initdb']),'-D',str(data),'-A','trust','-U','stockflow_test',
                    '--encoding=UTF8','--no-locale'],check=True,stdout=subprocess.DEVNULL)
    options='-h 127.0.0.1 -p '+str(port)+' -k '+shlex.quote(str(socket_dir))
    subprocess.run([str(tools['pg_ctl']),'-D',str(data),'-l',str(workspace/'server.log'),
                    '-o',options,'-w','start'],check=True)
    started=True
    common=['-h','127.0.0.1','-p',str(port),'-U','stockflow_test']
    for database in databases:
        subprocess.run([str(tools['createdb']),*common,database],check=True)
        for name in ('01_schema.sql','02_seed.sql'):
            subprocess.run([str(tools['psql']),'-X','-q','-v','ON_ERROR_STOP=1',*common,
                            '-d',database,'-f',str(snapshot/name)],check=True)
    integration_metadata={**metadata,'database':databases[0]}
    concurrency_metadata={**metadata,'database':databases[1]}
    (workspace/'integration-metadata.json').write_text(json.dumps(integration_metadata,indent=2))
    (workspace/'concurrency-metadata.json').write_text(json.dumps(concurrency_metadata,indent=2))
    environment=os.environ.copy()
    environment['STOCKFLOW_INTEGRATION_CONNECTION_STRING']=(
        'Host=127.0.0.1;Port='+str(port)+';Database='+databases[0]+';Username=stockflow_test;Pooling=false')
    integration=subprocess.run([args.dotnet,'run','--project',str(Path(__file__).parent/'StockFlow.Invoices.Integration.csproj'),
                                '-c','Release'],cwd=repo,env=environment,text=True,
                               stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    (workspace/'integration.log').write_text(integration.stdout)
    print(integration.stdout,end='')
    if integration.returncode: raise SystemExit(integration.returncode)
    subprocess.run(['python3',str(Path(__file__).parent/'serializable-probe.py'),
                    '--metadata',str(workspace/'concurrency-metadata.json'),
                    '--output',str(workspace/'concurrency')],check=True)
    result={'postgres_version':version,'source_commit':args.schema_ref,'schema_sha256':hashes,
            'integration_groups_passed':7,'concurrency_scenarios_passed':2,
            'scope':'Real movement classes; TEST-ONLY stock SQL adapter; SQL mechanism probe, not completed application SE-9'}
    (workspace/'summary.json').write_text(json.dumps(result,indent=2))
    print('PASS: 7 SQL integration groups and 2 overlapping-session mechanism scenarios.')
finally:
    if started:
        subprocess.run([str(tools['pg_ctl']),'-D',str(data),'-m','fast','-w','stop'],check=True)
    print('Scratch evidence retained at:',workspace)
