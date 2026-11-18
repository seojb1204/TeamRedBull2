# DDA pattern-frequency practice policy (2026-11-14)

This change implements bounded **practice**, not a guarantee of easier combat or fun.
The existing between-battle LinUCB damage/timing levels remain unchanged. No art,
scene, input, save schema, DB configuration, or personal data is changed.

## Behavior
- Each normal pattern selection reads the completed hit/parry/dodge events of this
  battle. Less successful patterns receive at most a 1.35x authored weight boost.
- Fewer than 4 valid opportunities: neutral. Eight neutral prior observations
  smooth the rate; malformed counters are ignored. Old persisted success rates
  are deliberately not used for frequency because they counted non-threatening misses.
- HP at or below 30%: no extra practice boost. DDA OFF: base weights.
- Existing range, phase, cooldown, and immediate-repeat gates are preserved.
  Prefer alternatives when a pattern was selected twice in the last six selections;
  if all eligible candidates are capped, fall back to eligible candidates to avoid
  deadlock. This is not an absolute repetition guarantee when choices are scarce.
- The authored first attack is preserved only if it satisfies candidate gates;
  the old override could bypass distance/phase eligibility.
- Never change the attack already running or increase its damage/timing mid-pattern.

## Evidence accuracy
A miss is no longer automatically a successful dodge. Unknown misses increment
`ignored`, not opportunities. Dash invulnerability overlapping an attack and
successful parries remain direct evidence. Slash/hazard positional avoidance is
counted only if the player occupied the threat region during a visible warning
and then moved at least 0.2 world units out before the attack. This is a spatial
proxy, not proof of intent. Multi-hit warning observations reset after each hit.
Untracked projectile, sweep and other positional misses are conservatively ignored;
this can undercount genuine avoidance and bias comparisons across pattern types.

## Diagnostics
`BossPatternSystem.SelectionLog` retains the last 128 candidate rows with decision
number, time, actual selected flag, opportunities, raw success, final weight,
normalized probability, cold-start/low-HP/boost/opening/repetition reason.
The existing explicitly requested autorun report exports these rows. It does not
introduce normal-play filesystem writes or network transmission. F8 layout unchanged.

## Verification
- New frequency/evidence tests: 178 assertions (including 150 bounded-history iterations).
- Existing logic: 132; DDA robustness: 31; Phanes: 3188 assertions passed.
- Existing aggregate: logic, DDA learning, history, panel geometry, charge,
  size-animation, charge-art suites pass.
- Two legacy art-scope checks fail because they assert DDA/combat sources are
  byte-identical to an old art-only baseline. The named failures are
  `combat_DDA_DB_input_camera_scene_prefab_settings_unchanged` and
  `DDA_DB_save_player_rules_source_unchanged`. They were not deleted/weakened.
- 240 real Update-loop battles under UnityShim, same-seed replay identical;
  no production DB, real saves, or user PC used. Campaign CSV and summary:
  `dda_frequency_20261114/`. Summary Python tests: 8 pass.
- Independent read-only code review and 178-test rerun: no remaining blocker found.
- Actual Unity API compilation, Windows build, F8 rendering, natural human play
  and fun/balance improvement are NOT yet verified. Keep PR draft until Unity gate.
