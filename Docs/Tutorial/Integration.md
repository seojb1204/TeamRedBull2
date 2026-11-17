# Interactive tutorial implementation status
Base main 83d9085. Feature only, no main merge or database changes.

## Runtime and integration
- New TutorialSession is isolated from DDA, rewards, saves and cloud.
- TowerGame.Tutorial uses detached new SaveData while practising; original object restored on exit/quit/completion.
- Tutorial damage is nonlethal. Threat resolution uses exact same active parry window (.05–.20 seconds), position/jump/dash collision checks; no DdaBattleSession.Prepare/Complete is called.
- Fresh/no-progress legacy profile requires training. Legacy profile with history, unlocked boss, gold, attack/health upgrades or clears is grandfathered. `tutorialSeen` alone is not evidence of completion.
- User-approved final task: activate basic R sword buff then actually hit dummy while empowered. **Integration MUST call TutorialEmpoweredHit(SwordCharged) from the actual empowered sword/boss collision after damage processing, not from R input, ChargedHits aggregate or minion hit.**
- Settings owner: add button `튜토리얼 다시 하기` -> StartTutorial(true), only when tutorialOnboardingVersion >=1 and not battle. Close settings first. Cancel/replay preserves initial assessment.
- F10 is tutorial skip only in UNITY_EDITOR/DEVELOPMENT_BUILD/EXHIBITION_BUILD. DevKeys is disabled during tutorial so normal ultimate-charge shortcut cannot run concurrently.
- SaveData fields tutorialAssessment/tutorialReplayAssessment/tutorialOnboardingVersion are added here; merge into save owner's new GameData without replacing their identity fields.

## Evidence / remaining gaps
Source and whitespace review only; no SDK in cloud, no Unity runtime verification performed yet. Tests/TutorialSession is a standalone .NET8 executable with 22 event-state assertions; NOT yet executed.

Not release-ready. Must verify/finish before merge:
1. Ultimate branch collision hook and shared charge mechanics integration.
2. Settings replay UI integration, HP/enemy/gauge RectTransform spotlight (skill slots currently spotlighted).
3. Scene presentation sprite renderers may still show normal Arakus; inspect/hide during tutorial and restore afterwards. Procedural WorldArtist replaces enemy with a dummy, but scene renderer integration is unverified.
4. Training background alignment/aspect ratio and collision readability in Unity, real keyboard input across hit-stop, healing and final hit.
5. Add per-key accepted/rejected input event accounting and richer attempt metrics; current session supports input mistakes but runtime not yet reporting them. Raw task times are collected.
6. After missed/expired R, player can recharge through real attacks and retry; validate no softlock. Verify all effects/cooldowns frozen across explanation gates and no prior combat buffs leak.
7. Tutorial telemetry is rules-based; weights NOT scientifically validated, expert requires >=3 parry and dodge opportunities; current flow with only one dodge remains normal at high score/low confidence.
8. Save failure rollback and write-result reporting depend on save owner's transactional integration. Do not claim completion persisted if write failed.
9. No full Cold Start group database integration is implemented here.

Damage reduction contract: only the short R casting interval, NOT the entire 8-second sword buff. Tutorial wording uses 시전 중; verify exact duration against ultimate owner.

## Follow-up safeguards
Guided Q..Potion wrong-control edge-triggered input batches now increment inputMistakes once per frame; reading and free practice are not penalized. This is NOT a count of all cooldown/invalid cast attempts; report that narrower operational definition.
Completion/skip saves a clone of original progression before switching. Failed writes keep tutorial frozen and original unchanged, with Enter retry or Escape exit; no completion is claimed. On exit/quit failed assessment is not persisted.
Do not add repeated mandatory dodge assessment merely to force confidence: current tutorial intentionally may return normal/low confidence. Collecting more opportunities requires a separately agreed flow; lower thresholds are not validation.
New TutorialPersistenceTests exercises isolated filesystem failure/retry/cancel. Await actual compiler/test results; source checks alone are not Unity proof.
