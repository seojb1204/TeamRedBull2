"""Fail-closed verification of an instrumented Unity battle report (not visual AI)."""
import csv
import hashlib
import math
from pathlib import Path


def validate(report_path):
    report_path = Path(report_path)
    report = {}
    for line in report_path.read_text(encoding='utf-8-sig').splitlines():
        if '\t' not in line:
            continue
        key, value = line.split('\t', 1)
        if key in report:
            raise ValueError('Duplicate report key: ' + key)
        report[key] = value
    for key, expected in {'schema': 'towerAutoRun/2', 'evidence.schema': 'towerBattleEvidence/1',
                          'evidence.track': 'logic-qa/instrumented-input',
                          'saveRootIsolated': 'true', 'result': 'AUTORUN_REPORT_COMPLETE'}.items():
        if report.get(key) != expected:
            raise ValueError('Missing/wrong report marker: ' + key)
    if report.get('evidence.droppedEvents') != '0':
        raise ValueError('Truncated event stream cannot be fully verified')
    # Never follow an arbitrary path supplied in the report; evidence is its sidecar.
    path = Path(str(report_path) + '.events.csv')
    totals = dict.fromkeys(['damage_taken', 'heal', 'damage_dealt', 'parry',
                           'input_requested', 'input_committed'], 0.0)
    previous = -1.0
    count = 0
    with path.open(encoding='utf-8-sig', newline='') as handle:
        rows = csv.DictReader(handle)
        if rows.fieldnames != ['seconds', 'kind', 'detail', 'amount']:
            raise ValueError('Wrong event columns')
        for row in rows:
            if None in row or any(v is None for v in row.values()):
                raise ValueError('Malformed event row')
            seconds, amount = float(row['seconds']), float(row['amount'])
            kind = row['kind']
            if not math.isfinite(seconds) or not math.isfinite(amount) or seconds < previous or seconds < 0 or amount < 0:
                raise ValueError('Non-finite/negative/out-of-order event')
            if kind not in totals:
                raise ValueError('Unknown event kind: ' + kind)
            if kind in ('parry', 'input_requested', 'input_committed') and amount != 0:
                raise ValueError('Counter event with nonzero amount')
            totals[kind] += 1 if kind in ('parry', 'input_requested', 'input_committed') else amount
            previous = seconds
            count += 1
            if count > 20000:
                raise ValueError('Event limit exceeded')
    mapping = {'damage_taken': 'damageTaken', 'heal': 'healing', 'damage_dealt': 'damageDealt',
               'parry': 'parries', 'input_requested': 'requestedInputs', 'input_committed': 'committedInputs'}
    for kind, key in mapping.items():
        value = float(report['evidence.' + key])
        if not math.isfinite(value) or value < 0 or not math.isclose(totals[kind], value, rel_tol=1e-5, abs_tol=.02):
            raise ValueError('Event/report mismatch: ' + key)
    if int(report['battle.parries']) != totals['parry']:
        raise ValueError('Combat/event parry mismatch')
    return {'verified': True, 'track': report['evidence.track'], 'eventCount': count,
            'eventsSha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'totals': totals}
