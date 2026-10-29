#!/usr/bin/env python3
"""Probe proposed SQL isolation only, not the application's StockRepository/SE-9.

Requires an explicitly prepared disposable database and metadata from the scratch
cluster setup. Never reads application configuration or touches a default database.
"""
import argparse
import atexit
import json
import re
import subprocess
import threading
import time
import uuid
from pathlib import Path
from queue import Queue, Empty


parser = argparse.ArgumentParser()
parser.add_argument('--metadata', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
metadata = json.loads(args.metadata.read_text())
if metadata['host'] != '127.0.0.1' or not re.fullmatch(r'stockflow_se6_concurrency_[a-z0-9]+', metadata['database']):
    raise SystemExit('Refusing a host/database other than the explicitly isolated concurrency scratch DB.')
args.output.mkdir(parents=True, exist_ok=False)
psql = [str(Path(metadata['bin'])/'psql'), '-X', '-qAt', '-h', metadata['host'],
        '-p', str(metadata['port']), '-U', metadata['username'], '-d', metadata['database'],
        '-v', 'ON_ERROR_STOP=1', '-v', 'VERBOSITY=verbose']
run = uuid.uuid4().hex[:8]


def query(sql):
    return subprocess.check_output([*psql, '-c', sql], text=True).strip()


sessions = []

def close_sessions():
    for session in sessions:
        if session.process.poll() is None:
            session.process.terminate()
            try: session.process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                session.process.kill()
                session.process.wait()

atexit.register(close_sessions)

class Session:
    def __init__(self, name):
        self.name = name
        self.lines = []
        self.queries = []
        self.queue = Queue()
        self.process = subprocess.Popen([*psql, '-a'], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                        stderr=subprocess.STDOUT, text=True, bufsize=1)
        sessions.append(self)
        self.reader = threading.Thread(target=self._read, daemon=True)
        self.reader.start()

    def _read(self):
        for line in self.process.stdout:
            self.lines.append(line)
            self.queue.put(line.strip())

    def send(self, sql):
        self.queries.append(sql)
        self.process.stdin.write(sql + '\n')
        self.process.stdin.flush()

    def marker(self, marker, timeout=15):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                if self.queue.get(timeout=0.1) == marker:
                    return
            except Empty:
                if self.process.poll() is not None:
                    break
        raise AssertionError(f'{self.name}: missing barrier {marker}: {"".join(self.lines)}')

    def finish(self):
        self.process.stdin.close()
        try:
            code = self.process.wait(timeout=20)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()
            raise AssertionError(f'{self.name}: timed out; see session log')
        self.reader.join(timeout=2)
        log = ''.join(self.lines)
        (args.output/(self.name+'.sql')).write_text('\n'.join(self.queries))
        (args.output/(self.name+'.log')).write_text(log)
        states = re.findall(r'ERROR:\s+([0-9A-Z]{5}):', log)
        return {'exit_code': code, 'sqlstates': states, 'sql': self.name+'.sql', 'log': self.name+'.log'}


def movement(number, kind, product, warehouse, amount):
    supplier = '1' if kind == 'receipt' else 'NULL'
    return f"""INSERT INTO stock_movements
    (number,type,product_id,warehouse_id,supplier_id,user_id,quantity,movement_date,note)
    VALUES ('{number}','{kind}',{product},{warehouse},{supplier},2,{amount},
            TIMESTAMP '2026-10-30 12:00:00','isolated Serializable mechanism probe');"""


def save(product, warehouse, quantity):
    return f"""INSERT INTO stock(product_id,warehouse_id,quantity,min_threshold)
    VALUES ({product},{warehouse},{quantity},0)
    ON CONFLICT (product_id,warehouse_id) DO UPDATE SET quantity=EXCLUDED.quantity;"""


def state(product, warehouse, prefix):
    return json.loads(query(f"""SELECT json_build_object(
      'quantity',(SELECT quantity FROM stock WHERE product_id={product} AND warehouse_id={warehouse}),
      'min_threshold',(SELECT min_threshold FROM stock WHERE product_id={product} AND warehouse_id={warehouse}),
      'movement_count',(SELECT count(*) FROM stock_movements WHERE number IN ('{prefix}-A','{prefix}-B')),
      'movement_quantity',(SELECT coalesce(sum(quantity),0) FROM stock_movements WHERE number IN ('{prefix}-A','{prefix}-B')),
      'movement_numbers',(SELECT array_agg(number ORDER BY number) FROM stock_movements WHERE number IN ('{prefix}-A','{prefix}-B'))
    );"""))


def begin(name):
    return f"SET application_name='{name}';\nBEGIN ISOLATION LEVEL SERIALIZABLE;"


# Existing-row case: A holds the row; B has an old snapshot and demonstrably waits
# on A's row lock. This is real overlap, not two sequential Register substitutes.
query('UPDATE stock SET quantity=10,min_threshold=10 WHERE product_id=1 AND warehouse_id=1;')
prefix = f'PROBE-{run}-WRITE'
a = Session('writeoff-A')
b = Session('writeoff-B')
a.send(begin(f'sf_{run}_write_a') + '\nSELECT quantity FROM stock WHERE product_id=1 AND warehouse_id=1 FOR UPDATE;\n\\echo A_ROW_LOCKED')
a.marker('A_ROW_LOCKED')
b.send(begin(f'sf_{run}_write_b') + '\nSELECT quantity FROM stock WHERE product_id=1 AND warehouse_id=1;\n\\echo B_SNAPSHOT_READY')
b.marker('B_SNAPSHOT_READY')
b.send('SELECT quantity FROM stock WHERE product_id=1 AND warehouse_id=1 FOR UPDATE;\n' +
       movement(prefix+'-B','write_off',1,1,8) + '\n' + save(1,1,2) + '\nCOMMIT;\n\\q')
waiting = False
for _ in range(100):
    if query(f"SELECT coalesce(wait_event_type,'') FROM pg_stat_activity WHERE application_name='sf_{run}_write_b';") == 'Lock':
        waiting = True
        break
    time.sleep(0.05)
if not waiting:
    raise AssertionError('B never demonstrably waited for A row lock; cannot claim an overlapping probe.')
a.send(movement(prefix+'-A','write_off',1,1,8) + '\n' + save(1,1,2) + '\nCOMMIT;\n\\q')
write_a, write_b = a.finish(), b.finish()
write_final = state(1,1,prefix)
assert write_a['exit_code'] == 0 and write_b['sqlstates'] == ['40001'], (write_a,write_b)
assert write_final['quantity'] == 2 and write_final['min_threshold'] == 10, write_final
assert write_final['movement_count'] == 1 and write_final['movement_quantity'] == 8, write_final


# Missing-row case: both sessions read absence before either writes. Releasing
# both barriers preserves overlapping Serializable snapshots; one must abort.
query('DELETE FROM stock WHERE product_id=10 AND warehouse_id=2;')
prefix = f'PROBE-{run}-RECEIPT'
a = Session('receipt-A')
b = Session('receipt-B')
for session, label in [(a,'A'),(b,'B')]:
    session.send(begin(f'sf_{run}_receipt_{label.lower()}') + '\nSELECT quantity FROM stock WHERE product_id=10 AND warehouse_id=2 FOR UPDATE;\n\\echo '+label+'_ABSENCE_READ')
for session, label in [(a,'A'),(b,'B')]:
    session.marker(label+'_ABSENCE_READ')
a.send(movement(prefix+'-A','receipt',10,2,5) + '\n' + save(10,2,5) + '\nCOMMIT;\n\\q')
b.send(movement(prefix+'-B','receipt',10,2,7) + '\n' + save(10,2,7) + '\nCOMMIT;\n\\q')
receipt_a, receipt_b = a.finish(), b.finish()
receipt_final = state(10,2,prefix)
codes = sorted([receipt_a['exit_code'],receipt_b['exit_code']])
states = receipt_a['sqlstates'] + receipt_b['sqlstates']
assert codes[0] == 0 and codes[1] != 0 and states == ['40001'], (receipt_a,receipt_b)
assert receipt_final['quantity'] in (5,7) and receipt_final['min_threshold'] == 0, receipt_final
assert receipt_final['movement_count'] == 1 and receipt_final['movement_quantity'] == receipt_final['quantity'], receipt_final

result = {
    'scope': 'Proposed raw SQL Serializable mechanism only; NOT application/StockRepository SE-9 completion',
    'postgres_version': query('SHOW server_version;'),
    'schema_commit': metadata['source_commit'], 'schema_sha256':metadata['sha256'],
    'database':metadata['database'],
    'writeoff':{'before':{'quantity':10,'min_threshold':10},'amounts':[8,8],
                'overlap':'B old snapshot then pg_stat_activity wait_event_type=Lock before A commit',
                'A':write_a,'B':write_b,'final':write_final},
    'receipt':{'before':'row absent','amounts':[5,7],
               'overlap':'both ABSENCE_READ barriers reached before either write',
               'A':receipt_a,'B':receipt_b,'final':receipt_final}
}
(args.output/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
print(json.dumps(result,ensure_ascii=False,indent=2))
