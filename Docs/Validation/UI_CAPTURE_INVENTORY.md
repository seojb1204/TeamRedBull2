# Historical UI capture inventory (manual visual audit)

## Scope and trust boundary

Eight static PNG/JPEG files under `Docs/Proof` were opened individually and visually inspected. Source snapshot inspected: `d86c223aff0e0ddbc9276511e5f2b05e66e32f3c`. Original files were not modified. `arakus-walk/walk_compare.gif` is an animation comparison, not part of this static UI inventory.

These are **historical reference images, not a current-build validation dataset**. Repository snapshot identity does not identify the executable that produced each capture. Capture time, executable SHA256, session ID, exact client viewport/crop and raw full-frame RGB provenance are not established for these files. The hashes below identify encoded files, **not decoded RGB**. JPEGs are lossy. No current recognition accuracy, keyboard safety, session-independent test performance or live game completion is established by this inventory.

Labels below are manual visual descriptions, not imported truth labels or permissions. All eight remain **ineligible to authorize input** until a new independently verified capture/build/viewport/session and binding contract is provided. Filename is not evidence of state.

## Observed content

Paths relative to `Docs/Proof`; dimensions are encoded image width × height.

| File | Size | Visually observed foreground | Candidate semantic label / conservative handling |
|---|---|---|---|
| `lobby.png` | 1600 × 900 | Three-card permanent-upgrade overlay; heading `영구 강화 · 세 카드 중 하나를 선택하세요`; dimmed lobby underneath; cancel/back button. | **blocked_cards, NOT lobby**. No Enter permission. Background lobby anchors must not override overlay evidence. |
| `ui/cards.jpg` | 1456 × 682 | `인장을 하나 고르세요`, three cards, selected center Q card; bottom `이번엔 넘어가기 Esc`; dimmed lobby. | blocked_cards. No Enter permission. |
| `ui/pause.jpg` | 1456 × 682 | Central `잠시 검을 내려놓고` menu over darkened battle; resume/Esc, settings, give-up/return options. | pause. No input; do not classify visible HUD as active battle. |
| `ui/battle.jpg` | 1456 × 682 | Battle HUD: player HP, timer, Arakus HP, skill bar; two characters; no visible modal. | battle candidate only. A still image cannot prove simulation is advancing or that input reaches the correct window. |
| `ui/title.jpg` | 1456 × 682 | Title `역전의 탑`; `이어서 오르기 Enter`, help and settings buttons; background characters. | title (unsupported state → unknown/no input). **Enter text alone must not authorize lobby start or result return.** |
| `ui/lobby.jpg` | 1456 × 682 | `맹세의 회랑`; boss list, Arakus detail, gold/upgrade panels; `도전하기 Enter`; no visible modal. | lobby candidate. Enter prompt visually present, but current binding/build provenance unverified. |
| `arakus.png` | 1600 × 900 | Battle with older-looking text-heavy upper HUD (`HP 100 / 100`, `아라쿠스`, `5s`) and characters; no visible modal. | battle candidate. UI differs substantially from `ui/battle.jpg`; do not pool as one viewport/build template family. |
| `card-input/outro-locked.png` | 640 × 480 | Darkened upgrade screen, heading and one remaining attack card; portions of text clipped/overlapped; character visible below. | transition / blocked_cards candidate, **not normal lobby or result**. No input throughout ambiguous exit animation. Exact transition phase cannot be inferred from this still. |

## Encoded-file integrity

```text
arakus.png                      e8f1d39758cbcd48a4d6cfa91c11bdc4e5535b54da32bee4073e74fb52bebd86
lobby.png                       0c132598142fb88d2411897064fc4b2d05ba857f165dc7884f95ba32098f1835
ui/cards.jpg                    6e399b47303e701c512e2ff81f69beee8b901a5300bd555ec9fedbd01ea08ebf
ui/pause.jpg                    747f83cd053dd5388df3e41b31b8d190e8db9f8be5692f720bafe399b06e5c45
ui/battle.jpg                   f02cab86d183d10217e31134555f5719afc6005b64e1a81051bac033fe5eb9d1
ui/title.jpg                    825c6d638df5af370396a2a1d2023b95df8d221ecf5a531e17533135ea60bc03
ui/lobby.jpg                    24ea2c4142bcdbb1ad0fc3c844d22f2ffdf6efa1c6154e78e3486686f2ef2871
card-input/outro-locked.png      d9425769e286a6aedf7233b833aba5ac74485abebe114cf37f8f3150c5c5a02b
```

## Available provenance, and what it does not prove

- [`../Proof/merge-20261105.md`](../Proof/merge-20261105.md) reports historical Unity 6000.4.0f1 tests, memory-only production scenes and controlled UI state transitions. It explicitly distinguishes these from a human keyboard/mouse complete playthrough and executable build. It is surrounding context, not a per-image capture manifest tying every `ui/*.jpg` byte to a tested binary.
- [`../Proof/card-input/README.md`](../Proof/card-input/README.md) explicitly embeds `outro-locked.png` as a card-exit input-blocking verification image. It describes camera-rendered UI, editor-injected keyboard/lifecycle signals and deterministic next-frame simulation; it does not claim an OS-keyboard playthrough.
- [`../Proof/card-input/result.json`](../Proof/card-input/result.json) records 69 checks and that editor scope. It provides no screenshot hash, full-frame raw RGB, session or build digest, so it is not sufficient to promote this screenshot into held-out live-input evidence.
- No per-image provenance manifest was found alongside the eight captures. Their labels and heterogeneous dimensions must not be inferred from names or normalized silently.

## Follow-up collection and regression cases

1. Capture independent calibration and held-out sessions from a known executable hash and exact supported viewport, including full-client RGB, timestamp and frame hash. Keep all related frames from one session in one split; exclude near-duplicates across splits as well as exact hashes.
2. Cover title, lobby, battle, result, pause, settings, help, history, three-card modal and partial-card transition. **No result/settings/help/history capture is represented here.** Modal/transition evidence must veto a stronger matching background lobby.
3. Include title Enter versus lobby Enter versus result Enter as separate contextual cases. Unknown/unsupported title must abstain even when the literal Enter prompt is visible.
4. Validate current bindings separately; no screenshot proves the effect of pressing Enter. No live OS capture, focus manipulation, PC interaction or keyboard input was performed for this audit.
5. Use the evaluator only after the required manifests and raw RGB are available. Report false Enter authorizations on blockers, unsupported states and ambiguous transitions; do not turn these historical images into performance claims.
