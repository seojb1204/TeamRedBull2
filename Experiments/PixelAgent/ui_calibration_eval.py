"""Offline held-out evaluation. No capture, native input or threshold tuning.
Records and provenance declarations require a trusted human/data pipeline; hashes
check integrity/duplicates, not truthfulness. Synthetic fixtures are NOT gameplay.
"""
from dataclasses import dataclass
import hashlib
import math

LABELS=('lobby','battle','result','pause','unknown')

def digest(data):return hashlib.sha256(data).hexdigest()
def sha_ok(value):return isinstance(value,str) and len(value)==64 and all(c in '0123456789abcdef' for c in value)
def ratio(n,d):return n/d if d else None


def summarize_decisions(decisions):
    """Frame-weighted diagnostic slice; empty ratios stay null, never zero accuracy."""
    total=len(decisions)
    accepted=sum(not d['abstained'] for d in decisions)
    correct=sum(not d['abstained'] and d['prediction']==d['truth'] for d in decisions)
    grants=sum(d['enter_granted'] for d in decisions)
    wrong=sum(d['wrong_enter'] for d in decisions)
    return {'test_frames':total,'accepted':accepted,'abstained':total-accepted,
            'correct_nonabstained':correct,'coverage':ratio(accepted,total),
            'accuracy_nonabstained':ratio(correct,accepted),
            'enter_grants':grants,'wrong_enter_grants':wrong,
            'wrong_enter_rate_per_grant':ratio(wrong,grants),
            'wrong_enter_rate_all_test':ratio(wrong,total)}

@dataclass(frozen=True)
class Sample:
    session_id: str
    frame_id: str
    split: str
    rgb: bytes
    rgb_sha256: str
    width: int
    height: int
    full_frame: bool
    build_sha256: str
    truth: str
    enter_allowed: bool
    source: str  # synthetic or real-labelled


def evaluate(samples, recognizer, *, build_sha256, width, height,
             template_sha256, template_sessions):
    """recognizer matches TemplateRecognizer API; only test frames are recognized.
    All sessions that contributed templates/threshold tuning must be listed in
    template_sessions and represented in calibration records. No automatic tuning.
    """
    rows=tuple(samples);sources=frozenset(template_sessions)
    if not sha_ok(build_sha256) or not sha_ok(template_sha256):raise ValueError('invalid expected hash')
    if type(width) is not int or type(height) is not int or width<=0 or height<=0:raise ValueError('invalid viewport')
    if getattr(recognizer,'template_sha256',None)!=template_sha256:raise ValueError('template hash mismatch')
    if any(not isinstance(s,str) or not s for s in sources):raise ValueError('invalid template session')
    session_splits={};hash_splits={};frame_ids=set();kinds=set()
    for r in rows:
        if not isinstance(r.session_id,str) or not r.session_id or not isinstance(r.frame_id,str) or not r.frame_id:raise ValueError('missing frame/session identity')
        if r.split not in {'calibration','test'}:raise ValueError('invalid split')
        if r.source not in {'synthetic','real-labelled'}:raise ValueError('unknown data provenance')
        kinds.add(r.source)
        if r.truth not in LABELS or type(r.enter_allowed) is not bool:raise ValueError('invalid ground truth')
        if r.enter_allowed and r.truth not in {'lobby','result'}:raise ValueError('invalid Enter ground truth')
        if type(r.width) is not int or type(r.height) is not int:raise ValueError('invalid frame viewport')
        if r.full_frame is not True:raise ValueError('full frame required')
        if (r.build_sha256,r.width,r.height)!=(build_sha256,width,height):raise ValueError('build/viewport mismatch')
        if type(r.rgb) is not bytes or len(r.rgb)!=width*height*3 or not sha_ok(r.rgb_sha256) or digest(r.rgb)!=r.rgb_sha256:raise ValueError('RGB integrity mismatch')
        identity=(r.session_id,r.frame_id)
        if identity in frame_ids:raise ValueError('duplicate frame identity')
        frame_ids.add(identity)
        if r.session_id in session_splits and session_splits[r.session_id]!=r.split:raise ValueError('session leakage')
        session_splits[r.session_id]=r.split
        if r.rgb_sha256 in hash_splits and hash_splits[r.rgb_sha256]!=r.split:raise ValueError('duplicate RGB leakage')
        hash_splits[r.rgb_sha256]=r.split
    if len(kinds)>1:raise ValueError('do not blend synthetic and real-labelled performance')
    if sources-set(session_splits):raise ValueError('missing template source session records')
    if any(session_splits[s]!='calibration' for s in sources):raise ValueError('template or tuning session leaked into test')
    if rows and not sources:raise ValueError('template/tuning provenance required')
    matrix={truth:{prediction:0 for prediction in LABELS} for truth in LABELS}
    total=accepted=correct=abstain=enter=wrong_enter=invalid_enter_contract=0
    decisions=[]
    for r in rows:
        if r.split!='test':continue
        prediction=recognizer.recognize(r.rgb,width=width,height=height,build_sha256=build_sha256,full_frame=True)
        if prediction.screen not in LABELS or type(prediction.verified) is not bool or type(prediction.enter_verified) is not bool:raise ValueError('invalid prediction')
        if not all(math.isfinite(v) for v in (prediction.distance,prediction.margin)):raise ValueError('non-finite prediction scores')
        label=prediction.screen if prediction.verified else 'unknown'
        is_abstain=label=='unknown'
        total+=1;abstain+=is_abstain;accepted+=not is_abstain
        correct+=not is_abstain and label==r.truth
        matrix[r.truth][label]+=1
        granted=prediction.enter_verified
        erroneous=granted and (not prediction.verified or prediction.screen!=r.truth or not r.enter_allowed)
        enter+=granted;wrong_enter+=erroneous
        invalid_enter_contract+=granted and (not prediction.verified or prediction.screen not in {'lobby','result'})
        decisions.append({'session':r.session_id,'frame':r.frame_id,'sha256':r.rgb_sha256,'truth':r.truth,'prediction':label,'abstained':is_abstain,'enter_granted':granted,'wrong_enter':erroneous,'distance':prediction.distance,'margin':prediction.margin})
    provenance=next(iter(kinds),'no-data')
    return {'schema':'tower-ui-heldout/1','data_kind':provenance,
        'scope':'synthetic contract tests only; no real game accuracy claim' if provenance!='real-labelled' else 'held-out labelled frames only; not live-input safety proof',
        'build_sha256':build_sha256,'template_sha256':template_sha256,'viewport':[width,height],
        'template_sessions':sorted(sources),'calibration_frames':sum(r.split=='calibration' for r in rows),
        'test_frames':total,'test_sessions':sum(v=='test' for v in session_splits.values()),
        'confusion_matrix':matrix,'abstained':abstain,'accepted':accepted,'correct_nonabstained':correct,
        'coverage':ratio(accepted,total),'accuracy_nonabstained':ratio(correct,accepted),
        'correct_nonabstained_fraction_all_test':ratio(correct,total),
        'abstain_rate':ratio(abstain,total),'enter_grants':enter,'wrong_enter_grants':wrong_enter,
        'wrong_enter_rate_per_grant':ratio(wrong_enter,enter),'wrong_enter_rate_all_test':ratio(wrong_enter,total),
        'enter_contract_violations':invalid_enter_contract,'decisions':decisions,
        'per_truth_label':{label:summarize_decisions([d for d in decisions if d['truth']==label]) for label in LABELS},
        'per_test_session':{session:summarize_decisions([d for d in decisions if d['session']==session])
                            for session in sorted(s for s,split in session_splits.items() if split=='test')},
        'limitations':['provenance/labels are asserted, not independently verified','exact hashes do not detect cropped or near-duplicate leakage','all template and threshold-tuning sessions must be declared','zero observed wrong Enter does not prove zero risk','correlated frames are not independent player trials']}


def load_dataset(root, manifest_path='dataset.json', *, max_total_bytes=256*1024*1024):
    """Read JSON data and raw RGB files only. No import/eval, image decoder or pickle.
    Relative paths resolve inside root (including symlinks). RGB is hashed before
    any size/shape interpretation. Use an immutable snapshot to avoid file races.
    """
    if type(max_total_bytes) is not int or max_total_bytes<0:raise ValueError('non-negative integer byte budget required')
    import json
    from pathlib import Path
    root=Path(root).resolve(strict=True)
    def inside(relative):
        if not isinstance(relative,str) or not relative or Path(relative).is_absolute():raise ValueError('relative dataset path required')
        path=(root/relative).resolve(strict=True)
        if not path.is_relative_to(root) or not path.is_file():raise ValueError('dataset path escapes root or is not a file')
        return path
    manifest=inside(manifest_path)
    if manifest.stat().st_size>2*1024*1024:raise ValueError('manifest too large')
    doc=json.loads(manifest.read_text(encoding='utf-8'))
    if not isinstance(doc,dict) or set(doc)!={'schema','samples'} or doc['schema']!='tower-ui-dataset/1' or not isinstance(doc['samples'],list) or len(doc['samples'])>10000:raise ValueError('invalid dataset manifest')
    fields=set(Sample.__dataclass_fields__)-{'rgb'}
    records=[];total=0
    for entry in doc['samples']:
        if not isinstance(entry,dict) or set(entry)!=fields|{'rgb_path'}:raise ValueError('invalid sample fields')
        path=inside(entry['rgb_path']);size=path.stat().st_size
        if size>64*1024*1024 or total+size>max_total_bytes:raise ValueError('dataset byte limit')
        with path.open('rb') as stream:raw=stream.read(min(64*1024*1024,max_total_bytes-total)+1)
        total+=len(raw)
        if len(raw)>64*1024*1024 or total>max_total_bytes:raise ValueError('dataset byte limit')
        if not sha_ok(entry['rgb_sha256']) or digest(raw)!=entry['rgb_sha256']:raise ValueError('RGB integrity mismatch')
        records.append(Sample(rgb=raw,**{k:entry[k] for k in fields}))
    return records
