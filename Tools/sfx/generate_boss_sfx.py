"""Boss combat sound effects for Reversal Tower.

Every effect is built from real recordings: CC0 Freesound recordings in
Tools/sfx/sources (see the README there) and the project's own CC0 StarNinjas
sword recordings. They are cut, re-pitched (tape-style speed change), reversed,
filtered, layered and given a hall reverb. No oscillator tones are used, so
nothing sounds like a synthesizer beep.

Output: Assets/StreamingAssets/Audio/sfx_<name>.wav (44.1 kHz, mono, 16-bit PCM,
the only format ExternalAssets.Wave reads) plus a Unity .meta with a stable GUID.

Usage:  python3 Tools/sfx/generate_boss_sfx.py            (writes all files)
        python3 Tools/sfx/generate_boss_sfx.py --out DIR  (writes elsewhere, for previews)
Needs numpy, scipy and the ffmpeg command (to decode the FLAC sources).

The output is deterministic: the same script always writes the same bytes.
"""
import argparse, functools, hashlib, subprocess, wave, zlib
from pathlib import Path
import numpy as np
from scipy.signal import fftconvolve, butter, sosfilt

SR = 44100
ROOT = Path(__file__).resolve().parents[2]
SOURCES = Path(__file__).resolve().parent / 'sources'
GAME_AUDIO = ROOT / 'Assets/StreamingAssets/Audio'


# ───────────────────────────── building blocks ─────────────────────────────

def rng(name):
    return np.random.default_rng(zlib.crc32(name.encode()))

def fit(x, n):
    return x[:n] if len(x) >= n else np.concatenate([x, np.zeros(n - len(x))])

def place(dst, src, at, gain=1.0):
    i = int(at * SR)
    if i >= len(dst):
        return dst
    m = min(len(src), len(dst) - i)
    dst[i:i + m] += src[:m] * gain
    return dst

def canvas(dur):
    return np.zeros(int(SR * dur))

def env_exp(n, decay, attack=0.002):
    t = np.arange(n) / SR
    return np.clip(t / attack, 0, 1) * np.exp(-t / decay)

def lowpass(x, fc, order=2):
    return sosfilt(butter(order, min(fc, SR * 0.45), 'low', fs=SR, output='sos'), x)

def highpass(x, fc, order=2):
    return sosfilt(butter(order, fc, 'high', fs=SR, output='sos'), x)

def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, min(hi, SR * 0.45)], 'band', fs=SR, output='sos'), x)

def drive(x, amount):
    return np.tanh(x * amount) / np.tanh(amount)

def reverb(x, size=1.6, mix=0.3, damp=4500, predelay=0.012, seed=1):
    """Hall reverb: convolution with a decaying-noise impulse response."""
    r = np.random.default_rng(seed)
    n = int(SR * size)
    t = np.arange(n) / SR
    ir = r.standard_normal(n) * np.exp(-t * 6.9 / size)
    for d, g in ((0.011, .55), (0.019, .45), (0.027, .38), (0.041, .3), (0.053, .24)):
        ir[int(d * SR)] += g * 3
    ir = lowpass(ir, damp, 1)
    ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    wet = fftconvolve(np.concatenate([x, np.zeros(len(ir))]), ir)[:len(x) + len(ir)]
    dry = np.concatenate([x, np.zeros(len(ir))])
    y = dry * (1 - mix) + wet * mix * 0.9
    tail = np.nonzero(np.abs(y) > 1e-4)[0]
    return y[:tail[-1] + 1] if len(tail) else y

def finish(x, peak=0.85):
    """Remove DC and sub rumble, lift bass harmonics for small speakers, normalize, trim tail."""
    x = x - np.mean(x)
    x = highpass(x, 35, 2)
    low = lowpass(x, 160, 2)
    lm = np.max(np.abs(low))
    if lm > 1e-6:
        x = x + highpass(drive(low / lm * 2.5, 3.0), 180, 2) * lm * 0.45
    fi = int(0.002 * SR)
    x[:fi] *= np.linspace(0, 1, fi)
    m = np.max(np.abs(x))
    if m <= 0:
        return x
    x = x * (peak / m)
    loud = np.nonzero(np.abs(x) > peak * 0.0032)[0]
    end = min(len(x), loud[-1] + int(0.04 * SR)) if len(loud) else len(x)
    x = x[:end]
    fo = min(int(0.06 * SR), len(x) // 4)
    x[-fo:] *= np.linspace(1, 0, fo) ** 2
    return x


# ───────────────────────────── recordings ─────────────────────────────

@functools.lru_cache(maxsize=None)
def _load(name):
    flac = SOURCES / f'{name}.flac'
    if flac.exists():
        raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(flac), '-f', 's16le', '-ac', '1', '-ar', str(SR), '-'],
                             check=True, capture_output=True).stdout
        x = np.frombuffer(raw, '<i2').astype(float) / 32768
    else:
        with wave.open(str(GAME_AUDIO / f'{name}.wav')) as w:
            x = np.frombuffer(w.readframes(w.getnframes()), '<i2').astype(float) / 32768
            x = x.reshape(-1, w.getnchannels()).mean(1)
            rate = w.getframerate()
        if rate != SR:
            x = np.interp(np.arange(int(len(x) * SR / rate)) * rate / SR, np.arange(len(x)), x)
    x = x - np.mean(x)
    return x / (np.max(np.abs(x)) + 1e-9)

def rec(name, speed=1.0, start=0.0, dur=None, rev=False, end_speed=None):
    """A recording, optionally cut, reversed and played faster/slower (pitch moves with speed,
    like a tape). end_speed glides the speed across the clip."""
    x = _load(name)
    x = x[int(start * SR):]
    if end_speed is None:
        if speed != 1.0:
            x = np.interp(np.arange(0, len(x) - 1, speed), np.arange(len(x)), x)
    else:
        n_out = int(len(x) / ((speed + end_speed) / 2))
        pos = np.cumsum(np.linspace(speed, end_speed, n_out))
        pos = pos[pos < len(x) - 1]
        x = np.interp(pos, np.arange(len(x)), x)
    if dur is not None:
        x = fit(x, int(dur * SR))
        f = min(int(0.03 * SR), len(x) // 4)
        x[-f:] *= np.linspace(1, 0, f)
    if rev:
        x = x[::-1].copy()
    return x

def swell_into(x, at_end):
    """Reverse-style build: align x so its loudest point lands at `at_end` seconds."""
    k = int(np.argmax(np.abs(x)))
    out = canvas(at_end + (len(x) - k) / SR)
    place(out, x, max(0.0, at_end - k / SR))
    return out

def grains(dur, count, seed, sources=('perc_impact1', 'perc_impact2', 'perc_door'), decay=0.4, speed=(1.2, 2.4)):
    """Falling debris: many tiny pieces of real impact recordings, scattered in time."""
    r = rng(seed)
    out = canvas(dur)
    for i in range(count):
        at = r.exponential(decay)
        if at >= dur:
            continue
        src = rec(sources[i % len(sources)], speed=r.uniform(*speed))
        piece = src[:int(SR * r.uniform(0.03, 0.09))].copy()
        piece *= np.linspace(1, 0, len(piece)) ** 2
        place(out, highpass(piece, 300), at, r.uniform(0.15, 0.6) * np.exp(-at / decay))
    return out


# ─────────────────────────────── sounds ───────────────────────────────

S = {}
TELL = {}
def sfx(name, tell=None):
    """tell: the pattern's wind-up in seconds (GameData.Catalog, DDA level 0).
    A wind-up sound swells toward that moment and then stops, so the strike that
    follows lands on near-silence instead of being masked by the wind-up tail."""
    def deco(fn):
        S[name] = fn
        if tell:
            TELL[name] = tell
        return fn
    return deco

def tell_shape(x, tell):
    end = min(len(x), int((tell + 0.06) * SR))
    x = fit(x, end).copy()
    x *= 0.12 + 0.88 * np.linspace(0, 1, end) ** 2.0
    fo = int(0.09 * SR)
    x[-fo:] *= np.linspace(1, 0, fo) ** 2
    return x

# Strikes are soft-clipped so their first 300 ms carry more energy than the wind-up before them.
PUNCH = {n: 2.2 for n in ('swing_heavy', 'impact_shield', 'kick', 'charge_go', 'charge_crash', 'landing', 'rush',
                          'sweep_low', 'sweep_high', 'rage_roar', 'erupt_holy', 'erupt_fall', 'thunder', 'wave',
                          'barrier_blast', 'judgement', 'player_hurt')}
PUNCH.update({n: 1.5 for n in ('swing', 'barrier_break', 'phase_change', 'boss_death', 'summon', 'zap')})

# Danger signature shared by every attack that cannot be parried: the cinematic boom played
# backwards, so the room seems to inhale right before the hit.
# misc_cineboom is a rumble that rises for 1.6 s and then sustains: its rise is the inhale,
# its body (cut short with a decay) is the ground-shaking tail under big hits.
def danger_inhale(at_end, length=1.3, speed=0.9):
    x = rec('misc_cineboom', speed=speed)[:int(1.62 / speed * SR)]
    return swell_into(x[-int(length * SR):], at_end)

def rumble(dur, decay=0.8):
    x = rec('misc_cineboom', start=1.55, dur=dur)
    return x * env_exp(len(x), decay, 0.004)

def stomp(gain=1.0):
    n = int(SR * 0.9)
    x = fit(rec('drum_tom_lo_hard', speed=0.62), n) * 0.9 + fit(rec('bd_boom', speed=0.9), n) * 0.6
    return lowpass(x, 2500) * gain

# --- Arakus: wind-ups (telegraphs) ---

@sfx('tell_heavy', tell=.85)          # 거검 휘두르기·횡베기: 무게를 싣는 발 + 칼을 뒤로 끄는 바람
def _():
    x = canvas(1.0)
    place(x, stomp(.55), 0)
    place(x, swell_into(rec('ambi_swoosh', speed=0.55, rev=True), .82), 0, 1.0)
    place(x, swell_into(rec('attack', speed=0.45, rev=True), .84), 0, .5)
    return reverb(x, 1.2, .22)

@sfx('tell_shield', tell=.85)         # 방패 내려찍기: 방패를 치켜드는 쇳소리
def _():
    x = canvas(1.0)
    place(x, stomp(.45), 0)
    place(x, swell_into(highpass(rec('parry', speed=0.5, rev=True), 300), .8), 0, .45)
    place(x, swell_into(rec('perc_swoosh', speed=0.5, rev=True), .84), 0, .9)
    return reverb(x, 1.1, .22)

@sfx('tell_quick', tell=.3)           # 발차기·퀵스텝: 짧은 옷자락
def _():
    return reverb(rec('perc_swash', speed=0.85), .7, .15)

@sfx('tell_charge', tell=1.1)         # 철벽 돌진 준비: 발 구르기 두 번 + 들이마시는 굉음
def _():
    x = canvas(1.25)
    place(x, stomp(1.0), 0)
    place(x, stomp(1.0), .38)
    place(x, danger_inhale(1.08, 1.3), 0, .9)
    place(x, swell_into(lowpass(rec('drum_roll', dur=1.2), 1800), 1.05), 0, .5)
    return reverb(x, 1.4, .22)

@sfx('tell_throw', tell=.6)           # 방패 투척 준비: 몸을 뒤로 젖힘
def _():
    x = canvas(.7)
    place(x, swell_into(rec('perc_swoosh', speed=0.65, rev=True), .58), 0)
    return reverb(x, .9, .2)

@sfx('tell_rush', tell=.5)            # 방패 돌진 준비: 쿵 + 짧게 들이마심
def _():
    x = canvas(.6)
    place(x, stomp(.9), 0)
    place(x, swell_into(rec('perc_swash', speed=0.6, rev=True), .48), 0, .8)
    return reverb(x, 1.0, .22)

@sfx('tell_sweep', tell=1.0)          # 일대 휩쓸기 준비: 들이마시는 굉음 + 칼날에 빛이 차오름 (패링 불가)
def _():
    x = canvas(1.2)
    place(x, danger_inhale(.98, 1.4), 0, 1.0)
    place(x, swell_into(highpass(rec('perc_bell', speed=1.5, dur=1.2, rev=True), 900), .97), 0, .5)
    place(x, swell_into(rec('ambi_swoosh', speed=0.6, rev=True), .97), 0, .6)
    return reverb(x, 1.6, .28)

@sfx('tell_rage', tell=2.5)           # 아라쿠스의 분노 시전: 빨라지는 북 + 낮게 깔리는 합창
def _():
    x = canvas(2.6)
    at, gap, k = 0.0, .55, 0
    while at < 2.4:
        place(x, rec('drum_tom_lo_hard', speed=0.7), at, .5 + .5 * at / 2.5)
        at += gap; gap = max(.12, gap * .8); k += 1
    place(x, lowpass(rec('drum_roll', start=.5, dur=2.5), 2500), 0, .45)
    place(x, rec('ambi_choir', speed=0.45, dur=2.6), 0, .55)
    place(x, danger_inhale(2.48, 1.6), 0, .9)
    return reverb(x, 1.8, .28)

# --- Arakus: strikes (the hit lands on the first frames) ---

@sfx('swing_heavy')                   # 거검 휘두르기·횡베기
def _():
    x = canvas(1.0)
    place(x, rec('attack', speed=0.55), 0, 1.0)
    place(x, rec('ambi_swoosh', speed=1.4, start=.8), 0, .7)
    place(x, rec('drum_tom_lo_hard', speed=0.8), .02, .6)
    return reverb(x, 1.1, .2)

@sfx('swing')                         # 가벼운 베기 (낫·연격·발도·잔상)
def _():
    x = canvas(.6)
    place(x, rec('attack', speed=0.8), 0, 1.0)
    place(x, rec('perc_swoosh', speed=1.2), 0, .6)
    return reverb(x, .9, .18)

@sfx('impact_shield')                 # 방패 내려찍기: 쇠와 나무가 바닥을 침
def _():
    x = canvas(1.4)
    place(x, rec('perc_impact1', speed=0.6), 0, 1.0)
    place(x, rec('hit', speed=0.55), 0, .6)
    place(x, rec('drum_bass_hard', speed=0.8), 0, .8)
    place(x, rec('perc_door', speed=0.7, start=.05), .01, .4)
    place(x, grains(1.2, 40, 'is-g', decay=.25), .03, .6)
    return reverb(x, 1.3, .24)

@sfx('kick')                          # 발차기: 둔탁한 몸통 타격
def _():
    x = canvas(.7)
    place(x, rec('drum_heavy_kick', speed=0.8), 0, 1.0)
    place(x, rec('perc_impact2', speed=0.85), 0, .7)
    place(x, rec('perc_swash', speed=1.1), 0, .4)
    return reverb(x, .8, .15)

@sfx('charge_go')                     # 철벽 돌진 출발: 바닥을 박차는 굉음 + 질주
def _():
    x = canvas(1.6)
    place(x, rec('bd_boom', speed=0.85), 0, 1.0)
    place(x, rec('perc_impact1', speed=0.55), 0, .8)
    place(x, rec('ambi_swoosh', speed=0.8), .05, .8)
    for k in range(5):
        place(x, stomp(.35), .2 + k * .2)
    return reverb(x, 1.3, .22)

@sfx('charge_crash')                  # 기둥 충돌: 돌이 깨지는 굉음 + 무너지는 잔해
def _():
    x = canvas(3.0)
    place(x, rumble(3.0, 0.90), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('perc_impact1', speed=0.45), 0, .9)
    place(x, rec('perc_door', speed=0.55), 0, .6)
    place(x, rec('drum_splash_hard', speed=0.5), .01, .35)
    place(x, grains(2.6, 140, 'cc-g', decay=.7), .04, .9)
    return reverb(x, 2.0, .28)

@sfx('shield_throw')                  # 방패 투척: 회전하며 날아가는 바람
def _():
    x = canvas(1.0)
    for k in range(5):
        place(x, rec('perc_swoosh', speed=1.5), k * .14, 1 - k * .15)
    return reverb(x, .9, .2)

@sfx('landing')                       # 도약 내려찍기 착지: 지면 강타
def _():
    x = canvas(2.4)
    place(x, rumble(2.4, 0.72), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('drum_tom_lo_hard', speed=0.55), 0, .8)
    place(x, rec('perc_impact2', speed=0.6), 0, .7)
    place(x, grains(2.0, 100, 'ld-g', decay=.5), .03, .8)
    return reverb(x, 1.8, .28)

@sfx('rush')                          # 방패 돌진: 짓밟으며 밀고 오는 소리
def _():
    x = canvas(1.6)
    for k in range(6):
        place(x, stomp(.8), k * .22)
        place(x, rec('perc_impact2', speed=0.9 + 0.05 * k), k * .22, .35)
    place(x, rec('ambi_swoosh', speed=0.7), 0, .5)
    return reverb(x, 1.2, .22)

@sfx('sweep_low')                     # 일대 휩쓸기 1타 (낮은 횡베기): 넓게 깔리는 칼바람
def _():
    x = canvas(1.5)
    place(x, rec('ambi_swoosh', speed=1.3, start=.8), 0, 1.0)
    place(x, rec('attack', speed=0.5), 0, .9)
    place(x, rec('perc_impact2', speed=0.7), 0, .8)
    place(x, highpass(rec('perc_bell', speed=1.5, dur=.8), 900), 0, .25)
    place(x, rec('drum_tom_lo_hard', speed=0.6), 0, .7)
    return reverb(x, 1.6, .28)

@sfx('sweep_high')                    # 일대 휩쓸기 2타 (종베기): 내리꽂히는 굉음
def _():
    x = canvas(3.2)
    place(x, rumble(3.2, 0.96), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('perc_impact1', speed=0.45), 0, .9)
    place(x, rec('hit', speed=0.5), 0, .5)
    place(x, rec('drum_splash_hard', speed=0.55), .01, .35)
    place(x, grains(2.6, 120, 'sh-g', decay=.6), .04, .9)
    return reverb(x, 2.2, .3)

@sfx('rage_roar')                     # 분노 완성: 폭발 + 낮은 합창이 터짐
def _():
    x = canvas(3.4)
    place(x, rumble(3.4, 1.02), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('bd_boom', speed=0.7), 0, .9)
    place(x, drive(rec('ambi_choir', speed=0.42, dur=3.0), 1.8), .02, .8)
    place(x, rec('drum_splash_hard', speed=0.6), 0, .4)
    return reverb(x, 2.2, .3)

# --- Phanes / Aethelos (light) ---

@sfx('tell_spell', tell=.9)           # 광탄·파편·창·파동 준비: 맑은 종이 차오름
def _():
    x = canvas(1.0)
    for k, sp in enumerate((1.0, 1.19, 1.5, 1.78)):
        place(x, highpass(rec('perc_bell2', speed=sp * 1.4, dur=.6), 600), k * .18, .35 + .15 * k)
    place(x, swell_into(rec('ambi_glass_rub', speed=1.2, dur=1.0), .88), 0, .7)
    return reverb(x, 1.6, .35)

@sfx('tell_holy', tell=1.05)          # 빛 기둥·낙하·벼락·창조 준비: 합창이 차오름
def _():
    x = canvas(1.2)
    place(x, rec('ambi_choir', speed=0.8, dur=1.2), 0, 1.0)
    place(x, swell_into(highpass(rec('perc_bell', speed=1.2, dur=1.2, rev=True), 500), 1.02), 0, .5)
    return reverb(x, 1.8, .38)

@sfx('tell_barrier')                  # 결계·저울·일식·하늘의 분노·심연의 파동 준비: 거꾸로 울리는 종 + 들이마시는 굉음
def _():
    x = canvas(1.45)
    place(x, swell_into(rec('perc_bell', speed=0.5, dur=2.5, rev=True), 1.38), 0, 1.0)
    place(x, danger_inhale(1.38, 1.5), 0, .8)
    place(x, rec('ambi_choir', speed=0.5, dur=1.45), 0, .45)
    return x

# --- Adicus / Kharos (dark) ---

@sfx('tell_dark', tell=.8)            # 낫·잔상·발도 준비: 빨려 들어가는 어두운 바람
def _():
    x = canvas(.9)
    place(x, swell_into(rec('ambi_dark_woosh', speed=1.3, rev=True), .78), 0, 1.0)
    place(x, rec('ambi_haunted_hum', speed=0.8, dur=.9), 0, .35)
    place(x, swell_into(highpass(rec('parry', speed=0.6, rev=True), 400), .78), 0, .25)
    return reverb(x, 1.2, .3)

@sfx('tell_summon', tell=.95)         # 죄수·망자 소환 준비: 낮은 웅웅거림 + 거꾸로 도는 바람
def _():
    x = canvas(1.1)
    place(x, rec('ambi_haunted_hum', speed=0.7, dur=1.1), 0, .8)
    place(x, swell_into(rec('ambi_lunar_land', speed=1.6, dur=2.0, rev=True), .93), 0, .8)
    return reverb(x, 1.6, .35)

# --- projectiles / hazards / gimmicks ---

@sfx('zap')                           # 광탄·창·사슬 발사
def _():
    x = canvas(.8)
    place(x, rec('perc_swoosh', speed=1.7), 0, 1.0)
    place(x, highpass(rec('perc_bell2', speed=2.0, dur=.4), 1200), 0, .35)
    return reverb(x, 1.1, .3)

@sfx('hazard_warn')                   # 장판 경고: 유리를 문지르는 떨림
def _():
    x = rec('ambi_glass_rub', speed=1.5, start=.3, dur=.6)
    x *= np.linspace(.4, 1, len(x))
    return reverb(x, .8, .2)

@sfx('erupt_holy')                    # 빛 기둥 폭발
def _():
    x = canvas(1.8)
    place(x, rec('bd_boom', speed=0.9), 0, .9)
    place(x, rec('perc_impact2', speed=0.75), 0, .7)
    place(x, rec('drum_splash_hard', speed=1.1), 0, .45)
    place(x, rec('ambi_choir', speed=1.0, dur=.9), 0, .5)
    return reverb(x, 1.8, .33)

@sfx('erupt_fall')                    # 낙하 공격 착탄 (별·낫·검)
def _():
    x = canvas(1.3)
    place(x, rec('perc_impact1', speed=0.7), 0, 1.0)
    place(x, rec('drum_tom_lo_hard', speed=0.75), 0, .6)
    place(x, grains(1.0, 40, 'ef-g', decay=.25), .02, .6)
    return reverb(x, 1.3, .26)

@sfx('thunder')                       # 벼락 꽂기
def _():
    x = canvas(2.6)
    place(x, rec('drum_snare_hard', speed=0.7), 0, .8)
    place(x, rec('drum_cymbal_hard', speed=0.55), 0, .5)
    place(x, rumble(2.6, 0.78), .02, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, lowpass(rec('ambi_lunar_land', speed=0.7, start=1.0, dur=2.4), 700), .1, .6)
    return reverb(x, 2.0, .3)

@sfx('wave')                          # 파동: 바닥을 따라 밀려오는 충격파
def _():
    x = canvas(1.8)
    place(x, rec('bd_boom', speed=0.8), 0, 1.0)
    place(x, rec('ambi_dark_woosh', speed=1.6), 0, .9)
    return reverb(x, 1.4, .3)

@sfx('barrier_rise')                  # 결계벽이 솟아오름: 큰 종 + 쿵
def _():
    x = canvas(2.4)
    place(x, rec('perc_bell', speed=0.55, dur=2.4), 0, .9)
    place(x, rec('bd_boom', speed=0.8), 0, .8)
    place(x, rec('ambi_choir', speed=0.7, dur=1.6), .05, .4)
    return reverb(x, 2.0, .33)

@sfx('barrier_break')                 # 결계 파괴: 깨지는 쇳소리 + 흩어지는 조각
def _():
    x = canvas(2.0)
    place(x, rec('drum_splash_hard', speed=1.25), 0, 1.0)
    place(x, rec('drum_cymbal_hard', speed=1.5), 0, .6)
    place(x, rec('bd_boom', speed=0.9), 0, .6)
    place(x, grains(1.6, 60, 'bb-g', sources=('perc_bell2', 'perc_till', 'parry'), decay=.35, speed=(1.4, 2.6)), .02, .9)
    return reverb(x, 1.8, .33)

@sfx('barrier_blast')                 # 결계 파괴 실패: 맵 전체 폭발
def _():
    x = canvas(3.6)
    place(x, rumble(3.6, 1.08), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('perc_impact1', speed=0.4), 0, .9)
    place(x, rec('drum_splash_hard', speed=0.5), 0, .4)
    place(x, grains(3.0, 140, 'bx-g', decay=.8), .04, .8)
    return reverb(x, 2.4, .33)

@sfx('summon')                        # 소환 순간: 땅에서 끌려 나오는 소리
def _():
    x = canvas(1.8)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('ambi_dark_woosh', speed=1.1, dur=1.6), 0, .8)
    place(x, rec('ambi_haunted_hum', speed=0.6, dur=1.6), 0, .4)
    return reverb(x, 1.6, .33)

@sfx('judgement')                     # 심판·일식 판정: 큰 종 + 굉음
def _():
    x = canvas(2.8)
    place(x, rec('perc_bell', speed=0.45, dur=2.8), 0, 1.0)
    place(x, rumble(2.8, 0.84), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('drum_tom_lo_hard', speed=0.5), 0, .6)
    return reverb(x, 2.2, .33)

# --- battle events ---

@sfx('phase_change')                  # 페이즈 전환: 굉음 + 큰 종 + 낮은 합창
def _():
    x = canvas(3.6)
    place(x, rumble(3.6, 1.08), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('perc_bell', speed=0.5, dur=3.4), 0, .6)
    place(x, rec('ambi_choir', speed=0.5, dur=3.0), .05, .7)
    place(x, rec('drum_splash_hard', speed=0.6), 0, .3)
    return reverb(x, 2.4, .33)

@sfx('boss_death')                    # 보스 쓰러짐: 꺼져 가는 합창 + 무너지는 굉음
def _():
    x = canvas(5.5)
    place(x, rumble(5.0, 1.50), 0, .55)
    place(x, rec('bd_boom', speed=0.75), 0, .8)
    place(x, rec('ambi_choir', speed=0.55, end_speed=0.3, dur=4.5), .05, .8)
    place(x, rec('perc_impact1', speed=0.4), .4, .6)
    place(x, grains(4.0, 120, 'bd-g', decay=1.2), .5, .7)
    return reverb(x, 3.0, .38)

@sfx('player_hurt')                   # 플레이어 피격: 묵직하고 아픈 타격
def _():
    x = canvas(.8)
    place(x, rec('drum_heavy_kick', speed=0.9), 0, .9)
    place(x, rec('perc_impact2', speed=1.0), 0, 1.0)
    place(x, rec('perc_swash', speed=1.2), 0, .35)
    return reverb(x, .8, .15)

@sfx('heartbeat')                     # 저체력 심장박동: 쿵-쿵
def _():
    x = canvas(.75)
    place(x, lowpass(rec('drum_bass_soft', speed=0.75), 1400), 0, 1.0)
    place(x, lowpass(rec('drum_bass_soft', speed=0.8), 1400), .24, .7)
    return x


# Big impacts are mostly low end, which ears and laptop speakers hear as quiet: lift 200 Hz-5 kHz.
PRESENCE = {'barrier_blast': 2.0, 'sweep_high': 1.5, 'judgement': 1.0, 'landing': 1.2, 'phase_change': 1.2,
            'charge_crash': 1.6, 'boss_death': 1.6, 'thunder': 0.6, 'wave': 1.0, 'rage_roar': 1.2, 'rush': 1.0,
            'impact_shield': 1.0, 'charge_go': 1.0, 'barrier_break': 0.6, 'summon': 0.6}


# ─────────────────────────────── output ───────────────────────────────

def write_wav(path, x):
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype('<i2')
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())

def write_meta(path):
    guid = hashlib.md5(('TeamRedBull2/sfx/' + path.name).encode()).hexdigest()
    path.with_name(path.name + '.meta').write_text(
        'fileFormatVersion: 2\nguid: ' + guid + '\nDefaultImporter:\n  externalObjects: {}\n'
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')

def render(name):
    x = np.asarray(S[name](), dtype=float)
    if name in TELL:
        x = tell_shape(x, TELL[name])
    elif name in PUNCH:
        x = drive(x / np.max(np.abs(x)), PUNCH[name])
    if name in PRESENCE:
        x = x + bandpass(x, 200, 5000) * PRESENCE[name]
    return finish(x)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=str(GAME_AUDIO))
    ap.add_argument('--only', nargs='*')
    args = ap.parse_args()
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for name in S:
        if args.only and name not in args.only:
            continue
        x = render(name)
        p = out / f'sfx_{name}.wav'
        write_wav(p, x)
        write_meta(p)
        print(f'{p.name:28s} {len(x) / SR:5.2f}s')

if __name__ == '__main__':
    main()
