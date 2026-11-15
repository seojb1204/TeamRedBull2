# Combat sound candidate (not yet audition-approved)

Original deterministic synthesis, no external recording/license cost. BGM files/music switching unchanged. Existing WAVs are preserved. New generated clips combine filtered noise, low impact transients and damped inharmonic metal resonators rather than one generic beep. All common UI/combat events (including parry/cue) routed to this candidate; per-event35ms retrigger guard and96 cached-clip limit.

All5 bosses use a distinct boss/kind/pattern key at actual ExecutePattern calls. Arakus release, throw slash, landing, rush start, sweep low/high and rage have explicit cues. Metal/sacred/low-dark voice family by boss; this is procedural sound design, not professional recorded foley. Generic dispatch covers the authored general pattern executor but Step remains visually handled, delayed hazard impacts still use shared hit sound: full per-effect replacement is not complete.

Passed headless132+animation430+size957 after event changes. Waveform synthesis72 cases covers4 sample rates, deterministic finite samples, nonzero energy, <=0.7 sample peak and end fades. This does NOT prove mixed output cannot clip or perceptual quality. Actual Unity compile, simultaneous hit/parry mix, auditory review and Windows regression required. Do not claim all effects finished or deploy on this evidence alone.

VFX: existing readable parry cues, hit regions, new approved skill FX remain unchanged in this commit. Separate review required for every boss telegraph, impact, hazard and recovery; avoid coloring all impacts like parry cues, obscuring melee warnings, or showing impact before delayed hazard activation.
