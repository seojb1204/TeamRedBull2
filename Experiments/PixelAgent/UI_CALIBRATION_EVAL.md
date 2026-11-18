# Offline UI calibration evaluation

`ui_calibration_eval.py` evaluates a caller-supplied trusted `TemplateRecognizer` on labelled full-frame raw RGB. It does not tune thresholds, import manifest code, capture screens, or control a PC. Existing policy/model/game source is unchanged.

- `evaluate(samples, recognizer, build_sha256=..., width=..., height=..., template_sha256=..., template_sessions=[...])`
- Declare every template or threshold-tuning source session. All must occur in calibration records, never test records. Only held-out test records are recognized/scored.
- Reject shared session IDs or exact RGB SHA256 across calibration/test, duplicated frame identity, missing provenance, wrong build/template hash/viewport, non-full-frame input, changed bytes, and mixed synthetic/real data.
- Confusion matrix includes `unknown`. Every unverified prediction becomes an abstention. Accuracy is explicitly conditional on non-abstained predictions; coverage and all-test correct fraction are reported separately. Every zero denominator yields null, never 0 or 1.
- Enter is erroneous when granted on a non-verified prediction, wrong screen, or human-labelled frame where Enter is not allowed. A lobby vs result mismatch counts as wrong even though each screen independently has an Enter binding. Pause/unknown ground truth never permits Enter. Invalid Enter contract outputs are counted rather than hidden by abstention.
- Report test frame/session counts, all decisions, build/template/RGB hashes, false Enter counts and denominators. Synthetic reports explicitly disclaim actual game accuracy.

`load_dataset(root, manifest_path='dataset.json')` accepts only JSON data with schema `tower-ui-dataset/1`, `samples` array, and Sample fields with `rgb_path` instead of `rgb`. RGB files are raw bytes, no image decompression/pickle/code import. Paths must resolve within dataset root, including symlinks. Read budget 64 MiB/file and 256 MiB/dataset, manifest 2 MiB. Hashes are verified before bytes are interpreted as RGB. Use immutable datasets to avoid file races.

No real labelled frames were available for this implementation. Unit tests are synthetic contracts only, not accuracy results. Exact hashes cannot detect resized/cropped/near-duplicate leakage; session provenance and labels remain assertions requiring review. Correlated video frames are not independent users/trials. Zero observed false Enter is not a guarantee of safety. Unseen popups and overlays require additional negative samples. Production thresholds and safety acceptance criteria must be fixed before evaluating untouched held-out sessions.

## Per-label and per-session diagnostics

Reports additionally contain `per_truth_label` (all five truth labels, including
unseen labels) and `per_test_session` (test sessions only, sorted by session ID).
Each slice reports `test_frames`, `accepted`, `abstained`,
`correct_nonabstained`, `coverage`, `accuracy_nonabstained`, `enter_grants`,
`wrong_enter_grants`, and wrong-Enter rates per grant and per frame. Calibration
frames never enter these slices. Zero denominators remain JSON null: an absent
label is not 0% accurate, and an all-abstaining session is not 100% accurate.
Unknown predictions are abstentions even when their truth label is unknown.
Malformed Enter grants still count as wrong grants even on abstained frames.

Inspect both slices before interpreting aggregate accuracy. A common lobby can
hide complete battle failure; a long successful session can hide a short failed
session. Counts reconcile with the aggregate but rates are frame-weighted within
each slice, not session-balanced estimates. These diagnostics do not compensate
for correlated frames or small samples and add no real-game performance evidence.
No thresholds are automatically tuned from test results. Synthetic fixtures remain
explicitly synthetic; real-data collection and independent labels are still needed.
