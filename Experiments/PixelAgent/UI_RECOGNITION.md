# Screen-state recognition contract

Implemented: deterministic ROI RGB template comparison with two non-overlapping UI anchors per screen, competing-label margin, exact build SHA256 and viewport. No resizing, no fallback from missing anchors, no probability claims. Enter permission requires a separately observed prompt region; label alone never authorizes Enter.

Not implemented/validated: production screenshot capture, real game templates, button coordinates, OCR/model confidence calibration, unseen-overlay detection, Windows input. Tests use synthetic RGB fixtures only. A known pause template can identify pause but cannot guarantee recognition of arbitrary modal windows. Until real labelled calibration tests pass this module must remain disconnected from live input.

## Template collection after PC is safe
1. Use isolated network-disabled build; hash executable and assets, fix build/version, resolution, UI scale and language.
2. Capture full window incl. HUD/UI, not pixel-bridge camera-only RenderTexture. Keep frame ID and monotonic capture timestamp.
3. For lobby/battle/result/pause collect positive and hard negative frames: help/settings/cards, transitions, low FPS, white hit flashes, scene changes, popups and partially obscured windows.
4. Choose at least two static, non-overlapping semantic UI regions. Do not rely on character color or background alone. Enter prompt ROI must actually match the keyboard binding in that build.
5. Split frames by capture session, tune threshold on calibration split, evaluate held-out confusion matrix and false action authorization count. Treat uncertain labels as no-input. Set success criteria before live test; zero observed errors does not prove zero risk.
6. Record build/template hashes and all decisions. Full-frame/hardware ownership fields still require independent trusted capture/host adapters; booleans are contracts, not evidence by themselves.

## Operational sequence
Recognition → two fresh-frame flow confirmation → current-frame binding validation → OS adapter safety gate → bounded input → post-input observation. User interruption, unknown/inconsistent observations, unexpected viewport/build and stale host evidence stop the session. Never auto-focus or act on another process.

## Known modal vetoes
`blocked_cards`, `blocked_help`, `blocked_settings`, `blocked_history`, and `blocked_transition` templates have veto priority over normal screens when their anchors pass the threshold. Thus even a stronger background lobby match cannot authorize Enter through a detected modal. A blocker always yields unknown/unverified/no Enter; ambiguous ordinary labels abstain. This handles only supplied templates, not arbitrary unseen overlays. The checked-in `Docs/Proof/lobby.png` visibly shows a three-card upgrade modal, not an idle actionable lobby: filenames are not ground truth. Historical captures without build provenance cannot be promoted to current-build test evidence.
