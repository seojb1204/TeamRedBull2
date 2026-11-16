"""Boss combat sound effects for Reversal Tower.

Every sound is synthesized here from oscillators, filtered noise and a synthetic
reverb. No recordings or third-party samples are used, so the output can ship
without attribution beyond CREDITS.md.

Output: Assets/StreamingAssets/Audio/sfx_<name>.wav (44.1 kHz, mono, 16-bit PCM,
the only format ExternalAssets.Wave reads) plus a Unity .meta with a stable GUID.

Usage:  python3 Tools/sfx/generate_boss_sfx.py            (writes all files)
        python3 Tools/sfx/generate_boss_sfx.py --out DIR  (writes elsewhere, for previews)

The output is deterministic: the same script always writes the same bytes.
"""
import argparse, hashlib, math, wave, zlib
from pathlib import Path
import numpy as np
from scipy.signal import fftconvolve, butter, sosfilt

SR = 44100
ROOT = Path(__file__).resolve().parents[2]


# ───────────────────────────── building blocks ─────────────────────────────

def rng(name):
    return np.random.default_rng(zlib.crc32(name.encode()))

def tt(dur):
    return np.arange(int(SR * dur)) / SR

def fit(x, n):
    """Pad or cut x to exactly n samples."""
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])

def place(dst, src, at, gain=1.0):
    """Mix src into dst starting at time `at` (seconds)."""
    i = int(at * SR)
    if i >= len(dst):
        return dst
    m = min(len(src), len(dst) - i)
    dst[i:i + m] += src[:m] * gain
    return dst

def env_ar(n, attack, release, curve=2.0):
    """Attack-release envelope over n samples (attack/release in seconds)."""
    t = np.arange(n) / SR
    dur = n / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    r = np.clip((dur - t) / max(release, 1e-4), 0, 1)
    return (a ** curve) * (r ** curve)

def env_exp(n, decay, attack=0.002):
    t = np.arange(n) / SR
    return np.clip(t / attack, 0, 1) * np.exp(-t / decay)

def osc_phase(freq):
    return 2 * np.pi * np.cumsum(freq) / SR

def sine(freq):
    return np.sin(osc_phase(freq))

def saw(freq):
    ph = np.cumsum(freq) / SR
    return 2 * (ph - np.floor(ph + 0.5))

def glide(n, f0, f1, curve=1.0):
    u = np.linspace(0, 1, n) ** curve
    return f0 * (f1 / f0) ** u

def lin(n, a, b):
    return np.linspace(a, b, n)

def noise(n, r):
    return r.standard_normal(n) * 0.33

def lowpass(x, fc, order=2):
    return sosfilt(butter(order, min(fc, SR * 0.45), 'low', fs=SR, output='sos'), x)

def highpass(x, fc, order=2):
    return sosfilt(butter(order, fc, 'high', fs=SR, output='sos'), x)

def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, min(hi, SR * 0.45)], 'band', fs=SR, output='sos'), x)

def svf_bp(x, fc, q):
    """Time-varying band-pass (TPT state-variable filter). fc may be an array."""
    fc = np.broadcast_to(np.asarray(fc, dtype=float), x.shape)
    out = np.empty_like(x)
    ic1 = ic2 = 0.0
    k = 1.0 / q
    for i in range(len(x)):
        g = math.tan(math.pi * min(fc[i], SR * 0.45) / SR)
        a1 = 1.0 / (1.0 + g * (g + k))
        v3 = x[i] - ic2
        v1 = a1 * ic1 + g * a1 * v3
        v2 = ic2 + g * v1
        ic1 = 2 * v1 - ic1
        ic2 = 2 * v2 - ic2
        out[i] = v1
    return out

def formant(src, vowel):
    table = {
        'a': [(700, 1.0, 8), (1150, 0.6, 10), (2600, 0.25, 14)],
        'o': [(450, 1.0, 8), (850, 0.55, 10), (2500, 0.2, 14)],
        'u': [(330, 1.0, 8), (750, 0.4, 10), (2400, 0.15, 14)],
    }[vowel]
    out = np.zeros_like(src)
    for f, g, q in table:
        out += svf_bp(src, f, q) * g
    return out

def drive(x, amount):
    return np.tanh(x * amount) / np.tanh(amount)

def reverb(x, size=1.6, mix=0.3, damp=4500, predelay=0.012, seed=1):
    """Hall reverb: convolution with a synthetic decaying-noise impulse response."""
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

def finish(x, peak=0.85, fade_in=0.002, fade_out=0.03):
    """Remove DC, fade edges, normalize to the given peak."""
    x = x - np.mean(x)
    # Sub below ~35 Hz only eats headroom. Then a bass exciter: saturate the low band and
    # add back only the harmonics above 180 Hz, so booms still read on small speakers.
    x = highpass(x, 35, 2)
    low = lowpass(x, 160, 2)
    lm = np.max(np.abs(low))
    if lm > 1e-6:
        x = x + highpass(drive(low / lm * 2.5, 3.0), 180, 2) * lm * 0.55
    n = len(x)
    fi, fo = int(fade_in * SR), int(fade_out * SR)
    if fi:
        x[:fi] *= np.linspace(0, 1, fi)
    if fo:
        x[-fo:] *= np.linspace(1, 0, fo) ** 2
    m = np.max(np.abs(x))
    if m <= 0:
        return x
    x = x * (peak / m)
    # Cut the reverb tail once it falls below about -50 dB, so clips stay small in memory.
    loud = np.nonzero(np.abs(x) > peak * 0.0032)[0]
    end = min(len(x), loud[-1] + int(0.04 * SR)) if len(loud) else len(x)
    x = x[:end]
    fo = min(int(0.06 * SR), len(x) // 4)
    x[-fo:] *= np.linspace(1, 0, fo) ** 2
    return x


# ─────────────────────────────── voices ───────────────────────────────

def growl(dur, f0, f1, vowel='a', drive_amt=3.0, seed='growl', vib=6.0, rough=0.35):
    """Throaty growl/roar: jittered sawtooth through vocal formants."""
    r = rng(seed)
    n = int(SR * dur)
    jitter = lowpass(r.standard_normal(n), 30) * rough * 4
    f = glide(n, f0, f1) * (1 + 0.03 * np.sin(2 * np.pi * vib * tt(dur)) + 0.04 * jitter)
    src = saw(f) + 0.6 * saw(f * 0.5) + 0.25 * noise(n, r) * (1 + jitter)
    v = formant(src, vowel)
    return drive(v * 2.5, drive_amt)

def whoosh(dur, f0, f1, q=1.5, seed='whoosh', curve=1.0, shape=(0.45, 0.4)):
    r = rng(seed)
    n = int(SR * dur)
    x = svf_bp(noise(n, r), glide(n, f0, f1, curve), q)
    return x * env_ar(n, shape[0] * dur, shape[1] * dur, 1.5)

def boom(dur, f0=80, f1=32, click=0.5, body=1.0, seed='boom', noise_lp=900):
    r = rng(seed)
    n = int(SR * dur)
    f = glide(n, f0, f1, 0.5)
    tone = sine(f) * env_exp(n, dur * 0.35, 0.001) * body
    nz = lowpass(noise(n, r), noise_lp) * env_exp(n, dur * 0.18, 0.001) * 2.2
    ck = highpass(noise(n, r), 1500) * env_exp(n, 0.012, 0.0005) * click * 3
    # Mid-range crunch: laptop and TV speakers cannot play below ~150 Hz, so the hit
    # must also live at 200-1600 Hz or it vanishes on the machine used for the demo.
    mid = bandpass(noise(n, r), 180, 1600) * env_exp(n, min(dur * 0.12, 0.12), 0.001) * 2.0 * body
    harm = highpass(drive(tone * 3.0, 4.0), 160) * 0.55
    return drive(tone + nz + ck + mid + harm, 1.6)

def rubble(dur, density=60, seed='rubble', lo=400, hi=4000, decay=None):
    r = rng(seed)
    n = int(SR * dur)
    out = np.zeros(n)
    count = int(density * dur)
    decay = decay or dur * 0.4
    for _ in range(count):
        at = r.exponential(decay)
        if at >= dur:
            continue
        g_len = int(SR * r.uniform(0.004, 0.03))
        grain = bandpass(r.standard_normal(g_len), lo * r.uniform(.7, 1.3), hi * r.uniform(.6, 1.0), 1)
        grain *= np.exp(-np.arange(g_len) / (g_len * 0.3)) * r.uniform(0.3, 1.0) * math.exp(-at / decay)
        place(out, grain, at)
    return out

def ring(dur, partials, decay, seed='ring', bend=0.0):
    n = int(SR * dur)
    t = tt(dur)
    out = np.zeros(n)
    for i, (f, g) in enumerate(partials):
        fr = f * (1 + bend * np.exp(-t * 8))
        out += sine(np.full(n, 1.0) * fr) * g * np.exp(-t / (decay / (1 + i * 0.35)))
    return out * np.clip(t / 0.001, 0, 1)

def braam(dur, root=55.0, seed='braam'):
    """Low brass-like danger hit: detuned saws, filter opens then closes."""
    r = rng(seed)
    n = int(SR * dur)
    t = tt(dur)
    src = np.zeros(n)
    for mult, g in ((1, 1.0), (1.5, 0.55), (2, 0.6), (3, 0.25)):
        for det in (-0.6, 0.0, 0.7):
            src += saw(np.full(n, root * mult * (1 + det / 100))) * g
    cutoff = 180 + 1700 * np.exp(-t / 0.35) * np.clip(t / 0.05, 0, 1)
    y = np.zeros(n)
    blk = 256
    for s in range(0, n, blk):  # piecewise-constant sweep
        seg = src[max(0, s - 512):s + blk]
        y[s:s + blk] = lowpass(seg, cutoff[s], 2)[-len(src[s:s + blk]):]
    y = drive(y * 0.5, 2.0) * env_ar(n, 0.02, dur * 0.6, 1.2)
    return y + lowpass(noise(n, r), 300) * env_exp(n, 0.15) * 0.4

def shimmer(dur, notes, seed='shimmer', rate=7.0):
    n = int(SR * dur)
    t = tt(dur)
    out = np.zeros(n)
    for i, f in enumerate(notes):
        trem = 0.65 + 0.35 * np.sin(2 * np.pi * (rate + i * 0.7) * t + i)
        out += (sine(np.full(n, f)) + 0.3 * sine(np.full(n, f * 2.01))) * trem
    return out / len(notes)

def choir(dur, notes, vowel='a', seed='choir'):
    r = rng(seed)
    n = int(SR * dur)
    t = tt(dur)
    src = np.zeros(n)
    for f in notes:
        for det in (-0.4, 0.4):
            vib = 1 + 0.006 * np.sin(2 * np.pi * (5.1 + det) * t + r.uniform(0, 6))
            src += saw(np.full(n, f * (1 + det / 100)) * vib)
    return formant(src / (2 * len(notes)), vowel)

def gong(dur, f0, seed='gong'):
    ratios = [(1.0, 1.0), (1.47, .7), (2.09, .5), (2.56, .45), (3.39, .3), (4.11, .22), (5.43, .12)]
    return ring(dur, [(f0 * a, g) for a, g in ratios], dur * 0.5, seed, bend=0.02)

def riser(dur, f0, f1, seed='riser'):
    r = rng(seed)
    n = int(SR * dur)
    u = np.linspace(0, 1, n)
    nz = svf_bp(noise(n, r), glide(n, f0, f1, 1.6), 2.5)
    tone = sine(glide(n, f0 * .5, f1 * .5, 1.6)) * 0.3
    return (nz + tone) * (u ** 2.2)

def drum(at_list, dur, f0=70, seed='drum'):
    out = np.zeros(int(SR * dur))
    for k, (at, g) in enumerate(at_list):
        place(out, boom(0.35, f0 * 1.2, f0 * 0.6, click=.3, seed=f'{seed}{k}', noise_lp=500), at, g)
    return out


# ─────────────────────────────── sounds ───────────────────────────────
# Each returns raw samples; finish() normalizes them.

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

PUNCH = {n: 2.6 for n in ('swing_heavy', 'impact_shield', 'kick', 'charge_go', 'charge_crash', 'landing', 'rush',
                          'sweep_low', 'sweep_high', 'rage_roar', 'erupt_holy', 'erupt_fall', 'thunder', 'wave',
                          'barrier_blast', 'judgement', 'player_hurt')}
PUNCH.update({n: 1.8 for n in ('swing', 'barrier_break', 'phase_change', 'boss_death', 'summon', 'zap')})

PRESENCE = {'barrier_blast': 3.5, 'sweep_high': 3.0, 'judgement': 4.5, 'landing': 2.0, 'phase_change': 2.0,
            'charge_crash': 1.5, 'boss_death': 1.3, 'barrier_rise': 2.0, 'wave': 1.5, 'impact_shield': 1.5,
            'thunder': 1.3, 'sweep_low': 1.0, 'erupt_fall': .8, 'player_hurt': .8, 'charge_go': .6}

def tell_shape(x, tell):
    end = min(len(x), int((tell + 0.06) * SR))
    x = x[:end].copy()
    ramp = 0.12 + 0.88 * np.linspace(0, 1, end) ** 2.0
    x *= ramp
    fo = int(0.09 * SR)
    x[-fo:] *= np.linspace(1, 0, fo) ** 2
    return x

# --- Arakus: wind-ups (telegraphs) ---

@sfx('tell_heavy', tell=0.85)            # 거검 휘두르기 / 횡베기: 낮게 으르렁거리며 힘을 모음
def _():
    d = 0.95
    x = growl(d, 72, 88, 'a', 3.5, 'th') * env_ar(int(SR * d), .35, .2, 1.6)
    x += lowpass(whoosh(d, 900, 2600, 6, 'th-scrape', 1.4, (.6, .3)), 5000) * 0.6
    x += sine(glide(int(SR * d), 42, 55)) * env_ar(int(SR * d), .5, .2) * 0.5
    return reverb(x, 1.2, .25)

@sfx('tell_shield', tell=0.85)           # 방패 내려찍기: 방패를 치켜들며 짧게 기합
def _():
    d = 0.75
    x = growl(.55, 90, 105, 'o', 3, 'ts') * env_ar(int(SR * .55), .15, .2)
    x = fit(x, int(SR * d))
    x += whoosh(d, 300, 1100, 1.8, 'ts-w', 1.2, (.7, .2)) * 0.9
    x += sine(glide(int(SR * d), 50, 60)) * env_ar(int(SR * d), .4, .2) * .4
    return reverb(x, 1.0, .22)

@sfx('tell_quick', tell=0.3)            # 발차기·퀵스텝: 짧은 숨 + 옷자락
def _():
    d = 0.32
    r = rng('tq')
    n = int(SR * d)
    breath = bandpass(noise(n, r), 900, 3200) * env_ar(n, .05, .2)
    return breath + whoosh(d, 500, 1800, 1.5, 'tq-w') * .7

@sfx('tell_charge', tell=1.1)           # 철벽 돌진 준비: 발 구르기 두 번 + 커지는 포효 + 위험 경고음
def _():
    d = 1.25
    n = int(SR * d)
    x = np.zeros(n)
    place(x, boom(.4, 90, 40, .6, seed='tc1'), 0.0, 1.0)
    place(x, boom(.4, 90, 40, .6, seed='tc2'), 0.38, 1.0)
    place(x, braam(1.1, 49, 'tc-b'), 0.0, 0.7)
    g = growl(.9, 80, 125, 'a', 4, 'tc-g') * env_ar(int(SR * .9), .6, .1, 1.3)
    place(x, g, .3, .9)
    x += riser(d, 120, 900, 'tc-r') * .5
    return reverb(x, 1.4, .25)

@sfx('tell_throw', tell=0.6)            # 방패 투척 준비: 몸을 뒤로 젖히며 바람을 가름
def _():
    d = 0.6
    x = whoosh(d, 1600, 400, 2, 'tt-w', 1, (.7, .15)) * 1.0
    x += fit(growl(.45, 95, 85, 'o', 2.5, 'tt-g') * env_ar(int(SR * .45), .1, .25), int(SR * d)) * .6
    return reverb(x, .9, .2)

@sfx('tell_rush', tell=0.5)             # 방패 돌진 준비: 쿵 + 짧은 함성
def _():
    d = 0.55
    x = fit(boom(.4, 85, 40, .5, seed='tr'), int(SR * d))
    place(x, growl(.4, 100, 120, 'a', 3.5, 'tr-g') * env_ar(int(SR * .4), .1, .15), .1, .8)
    return reverb(x, 1.0, .22)

@sfx('tell_sweep', tell=1.0)            # 일대 휩쓸기 준비: 위험 경고음 + 검에 빛이 차오르는 소리 (회피 불가)
def _():
    d = 1.15
    n = int(SR * d)
    x = braam(d, 55, 'sw-b') * 1.1
    x += shimmer(d, [880, 1320, 1760, 2640], 'sw-s', 11) * (np.linspace(0, 1, n) ** 2.5) * .55
    x += riser(d, 200, 4200, 'sw-r') * .7
    x += sine(glide(n, 35, 46)) * np.linspace(.2, 1, n) * .45
    return reverb(x, 1.6, .3)

@sfx('tell_rage', tell=2.5)             # 아라쿠스의 분노 시전 (2.5초): 빨라지는 북 + 깊어지는 저음
def _():
    d = 2.5
    n = int(SR * d)
    hits, at, gap = [], 0.0, .55
    while at < d - .1:
        hits.append((at, .55 + .45 * at / d))
        at += gap
        gap = max(.13, gap * .8)
    x = drum(hits, d, 62, 'tg')
    x += braam(d, 41.2, 'tg-b') * .45 * np.linspace(.3, 1, n)
    x += riser(d, 80, 1500, 'tg-r') * .45
    x += growl(d, 60, 80, 'u', 2.5, 'tg-g') * np.linspace(0, 1, n) ** 2 * .45
    return reverb(x, 1.6, .25)

# --- Arakus: strikes ---

@sfx('swing_heavy')           # 거검 휘두르기·횡베기
def _():
    d = 0.75
    x = whoosh(d, 1300, 220, 1.4, 'sh-w', .7, (.06, .8)) * 1.4
    x += ring(d, [(820, .5), (1907, .3), (3140, .15)], .25, 'sh-r') * .35
    place(x, boom(.35, 70, 40, .2, .8, 'sh-b'), .02, .8)
    return reverb(x, 1.1, .2)

@sfx('swing')                 # 가벼운 베기 (낫·연격·발도)
def _():
    d = 0.45
    x = whoosh(d, 2600, 500, 1.6, 'sw-l', .8, (.05, .85)) * 1.2
    x += ring(d, [(1240, .4), (2890, .2)], .18, 'sw-lr') * .3
    return reverb(x, .9, .18)

@sfx('impact_shield')         # 방패 내려찍기: 나무+쇠 둔탁음 + 바닥 파편
def _():
    d = 0.9
    r = rng('is')
    n = int(SR * d)
    x = boom(d, 95, 38, .8, 1.1, 'is-b')
    x += bandpass(noise(n, r), 250, 900) * env_exp(n, .06) * 1.2
    x += ring(d, [(310, .5), (742, .3), (1290, .2)], .2, 'is-r') * .5
    x += rubble(d, 70, 'is-ru', 500, 5000, .2) * .8
    return reverb(x, 1.3, .25)

@sfx('kick')                  # 발차기: 둔탁한 몸통 타격
def _():
    d = 0.4
    x = boom(d, 140, 55, .5, 1.0, 'k-b')
    x += whoosh(d, 600, 1500, 1.4, 'k-w', 1, (.2, .5)) * .5
    return reverb(x, .8, .15)

@sfx('charge_go')             # 철벽 돌진 출발: 폭발적 도약 + 질주 바람
def _():
    d = 1.3
    x = fit(boom(.7, 75, 30, 1.0, 1.2, 'cg-b'), int(SR * d))
    x += fit(highpass(noise(int(SR * .3), rng('cg-c')), 1200) * env_exp(int(SR * .3), .05), int(SR * d)) * 1.2
    x += rubble(d, 90, 'cg-ru', 300, 4000, .25) * .9
    x += whoosh(d, 150, 900, 1.2, 'cg-w', .7, (.15, .6)) * 1.1
    for k in range(5):
        place(x, boom(.25, 70, 45, .3, .6, f'cg-s{k}'), .15 + k * .2, .5)
    return reverb(x, 1.3, .22)

@sfx('charge_crash')          # 기둥 충돌: 돌이 깨지는 굉음 + 무너지는 잔해
def _():
    d = 2.0
    r = rng('cc')
    n = int(SR * d)
    x = boom(d, 70, 26, 1.0, 1.4, 'cc-b')
    x += highpass(noise(n, r), 1200) * env_exp(n, .08) * 1.6
    x += rubble(d, 140, 'cc-ru', 300, 6000, .6) * 1.1
    x += ring(d, [(196, .5), (417, .3), (689, .2)], .5, 'cc-r') * .35
    return reverb(x, 2.0, .3)

@sfx('shield_throw')          # 방패 투척: 회전하며 날아가는 휘파람 소리
def _():
    d = 0.8
    n = int(SR * d)
    t = tt(d)
    w = whoosh(d, 700, 1400, 3, 'st-w', 1, (.1, .4)) * 1.4
    am = .5 + .5 * np.sin(2 * np.pi * glide(n, 16, 22).cumsum() / SR * 1.0)
    return reverb(w * am + ring(d, [(1650, .2)], .15, 'st-r') * .3, .9, .2)

@sfx('landing')               # 방패 던지고 내려찍기 착지: 지면 강타
def _():
    d = 1.5
    r = rng('ld')
    n = int(SR * d)
    x = boom(d, 85, 28, 1.0, 1.4, 'ld-b')
    x += highpass(noise(n, r), 900) * env_exp(n, .05) * 1.2
    x += rubble(d, 110, 'ld-ru', 300, 5000, .45)
    x += ring(d, [(280, .5), (660, .3)], .25, 'ld-r') * .35
    return reverb(x, 1.8, .3)

@sfx('rush')                  # 방패 돌진: 짓밟으며 밀고 오는 소리
def _():
    d = 1.4
    n = int(SR * d)
    x = np.zeros(n)
    for k in range(6):
        place(x, boom(.28, 90, 45, .4, .8, f'ru{k}'), k * .22, .8)
    x += growl(d, 95, 110, 'a', 4, 'ru-g') * env_ar(n, .1, .5) * .7
    x += whoosh(d, 200, 700, 1.2, 'ru-w', 1, (.2, .5)) * .7
    return reverb(x, 1.2, .22)

@sfx('sweep_low')             # 일대 휩쓸기 1타 (낮은 횡베기): 넓게 깔리는 칼바람 + 빛
def _():
    d = 1.0
    x = whoosh(d, 2200, 160, 1.1, 'sl-w', .7, (.04, .85)) * 1.6
    x += ring(d, [(1046, .4), (1568, .3), (2093, .25), (3136, .15)], .4, 'sl-r', .03) * .45
    place(x, boom(.5, 60, 35, .3, .9, 'sl-b'), .01, .9)
    cr = highpass(noise(int(SR * .3), rng('sl-c')), 1400) * env_exp(int(SR * .3), .05)
    place(x, cr, 0, 1.3)
    return reverb(x, 1.6, .3)

@sfx('sweep_high')            # 일대 휩쓸기 2타 (종베기): 내리꽂히는 굉음
def _():
    d = 2.2
    r = rng('shh')
    n = int(SR * d)
    x = np.zeros(n)
    # The hit lands on the first sample: the warning came from tell_sweep and sweep_low.
    place(x, whoosh(.3, 3000, 300, 1.4, 'shh-w', .8, (.03, .9)) * 1.2, 0)
    place(x, boom(1.8, 70, 24, 1.0, 1.5, 'shh-b'), .01, 1.1)
    place(x, highpass(noise(int(SR * .5), r), 1500) * env_exp(int(SR * .5), .06) * 1.4, .01)
    place(x, rubble(1.6, 120, 'shh-ru', 300, 5000, .5), .03, 1.0)
    place(x, ring(1.6, [(1046, .4), (2093, .3), (3136, .2)], .6, 'shh-r') * .4, .01)
    return reverb(x, 2.4, .32)

@sfx('rage_roar')             # 분노 완성: 짐승 같은 포효 + 충격파
def _():
    d = 2.0
    n = int(SR * d)
    x = np.zeros(n)
    g = growl(1.7, 105, 82, 'a', 5, 'rr-g', vib=5, rough=.6) * env_ar(int(SR * 1.7), .08, .6, 1.2)
    g += growl(1.7, 210, 160, 'o', 3, 'rr-g2', vib=5.5, rough=.5) * env_ar(int(SR * 1.7), .1, .6, 1.2) * .35
    place(x, g, .05, 1.0)
    place(x, boom(1.2, 65, 26, .8, 1.3, 'rr-b'), 0, .9)
    return reverb(x, 2.2, .3)

# --- Phanes / Aethelos (light) ---

@sfx('tell_spell', tell=0.9)            # 광탄·파편·파동 준비: 위로 올라가는 반짝임
def _():
    d = 0.9
    n = int(SR * d)
    notes = [659, 880, 1047, 1319, 1760]
    x = np.zeros(n)
    for k, f in enumerate(notes):
        tone = (sine(np.full(int(SR * .5), f)) + .3 * sine(np.full(int(SR * .5), f * 2))) * env_exp(int(SR * .5), .18)
        place(x, tone, k * .14, .5)
    x += riser(d, 600, 5000, 'tsp-r') * .5
    return reverb(x, 1.6, .4)

@sfx('tell_holy', tell=1.05)             # 빛 기둥·낙하·벼락·창조 준비: 성가 같은 울림이 차오름
def _():
    d = 1.1
    n = int(SR * d)
    x = choir(d, [220, 277.2, 330, 440], 'a', 'th-c') * env_ar(n, .7, .15, 1.3) * 1.2
    x += riser(d, 400, 6000, 'th-r') * .45
    x += ring(d, [(1760, .3), (2637, .2)], .4, 'th-bell') * np.linspace(0, 1, n) * .3
    return reverb(x, 1.8, .4)

@sfx('tell_barrier')          # 결계·심판의 저울·일식·하늘의 분노 준비: 거꾸로 울리는 징 + 위험 경고음
def _():
    d = 1.4
    n = int(SR * d)
    g = reverb(gong(1.4, 82, 'tb-g'), 1.4, .4)[:n][::-1]
    x = fit(g, n) * 1.2
    x += braam(d, 41.2, 'tb-b') * .6 * np.linspace(.2, 1, n)
    x += choir(d, [110, 164.8], 'u', 'tb-c') * np.linspace(0, 1, n) ** 2 * .7
    return x

# --- Adicus / Kharos (dark) ---

@sfx('tell_dark', tell=0.8)             # 낫·잔상·발도 준비: 빨려 들어가는 어두운 기운 + 사슬
def _():
    d = 0.85
    n = int(SR * d)
    w = reverb(whoosh(.6, 300, 1600, 2, 'td-w', 1, (.2, .5)), 1.0, .6)
    x = fit(w[::-1], n) * 1.3
    x += growl(d, 55, 62, 'u', 2.5, 'td-g') * env_ar(n, .5, .1) * .5
    r = rng('td-chain')
    for k in range(7):
        at = .1 + k * .09 + r.uniform(-.02, .02)
        place(x, ring(.12, [(2300 * r.uniform(.9, 1.1), .5), (3900, .3)], .03, f'td{k}') * .25, at)
    return x

@sfx('tell_summon', tell=0.95)           # 죄수·망자 소환 준비: 속삭임 + 낮은 웅웅거림
def _():
    d = 1.0
    r = rng('ts')
    n = int(SR * d)
    whisper = formant(noise(n, r) * 2, 'u') * (0.6 + 0.4 * np.sin(2 * np.pi * 3.3 * tt(d))) * env_ar(n, .3, .2)
    drone = (saw(np.full(n, 55)) + saw(np.full(n, 55.4))) * .5
    x = whisper * 1.3 + lowpass(drone, 400) * env_ar(n, .4, .2) * .5
    return reverb(x, 1.6, .35)

# --- projectiles / hazards / gimmicks ---

@sfx('zap')                   # 광탄·창·사슬 발사
def _():
    d = 0.5
    r = rng('zp')
    n = int(SR * d)
    mod = sine(np.full(n, 330)) * 600 * np.exp(-tt(d) / .1)
    car = np.sin(osc_phase(glide(n, 1800, 500, .6) + mod))
    x = car * env_exp(n, .12) + highpass(noise(n, r), 3000) * env_exp(n, .04) * .5
    return reverb(x, 1.1, .3)

@sfx('hazard_warn')           # 장판 경고 (기둥·낙하): 짧게 끓어오르는 소리
def _():
    d = 0.6
    r = rng('hw')
    n = int(SR * d)
    hum = sine(glide(n, 220, 440)) * (.6 + .4 * np.sin(2 * np.pi * 18 * tt(d)))
    sizzle = bandpass(noise(n, r), 2500, 7000) * (.4 + .6 * np.linspace(0, 1, n))
    return (hum * .6 + sizzle * .5) * env_ar(n, .15, .1)

@sfx('erupt_holy')            # 빛 기둥 폭발
def _():
    d = 1.3
    r = rng('eh')
    n = int(SR * d)
    x = boom(d, 90, 40, .6, .9, 'eh-b')
    x += bandpass(noise(n, r), 1800, 7000) * env_exp(n, .12) * 1.4
    x += shimmer(d, [1047, 1319, 1568, 2093], 'eh-s', 13) * env_exp(n, .4) * .6
    return reverb(x, 1.8, .35)

@sfx('erupt_fall')            # 낙하 공격 착탄 (별·낫·검)
def _():
    d = 0.9
    x = boom(d, 100, 40, .9, 1.0, 'ef-b')
    x += rubble(d, 80, 'ef-ru', 500, 5000, .2) * .8
    return reverb(x, 1.3, .28)

@sfx('thunder')               # 벼락 꽂기
def _():
    d = 1.8
    r = rng('tn')
    n = int(SR * d)
    crack = highpass(noise(n, r), 1500) * env_exp(n, .05) * 2.0
    rumble = lowpass(noise(n, r), 160) * env_ar(n, .05, 1.2, 1) * 2.4
    x = crack + rumble + boom(d, 70, 30, .3, .8, 'tn-b')
    return reverb(x, 2.0, .3)

@sfx('wave')                  # 파동: 바닥을 따라 밀려오는 충격파
def _():
    d = 1.2
    n = int(SR * d)
    x = boom(d, 60, 30, .3, 1.0, 'wv-b')
    x += whoosh(d, 150, 600, 1.0, 'wv-w', 1, (.1, .6)) * 1.2
    return reverb(x, 1.4, .3)

@sfx('barrier_rise')          # 결계벽이 솟아오름
def _():
    d = 1.6
    n = int(SR * d)
    x = gong(d, 98, 'br-g') * .8
    x += boom(d, 70, 35, .4, .8, 'br-b')
    x += riser(d, 300, 3000, 'br-r')[::-1] * .4
    x += shimmer(d, [392, 587, 784], 'br-s', 5) * env_ar(n, .4, .8) * .4
    return reverb(x, 2.0, .35)

@sfx('barrier_break')         # 결계 파괴 성공: 유리 깨짐 + 내려가는 종소리
def _():
    d = 1.4
    r = rng('bb')
    n = int(SR * d)
    x = highpass(noise(n, r), 2500) * env_exp(n, .09) * 1.4
    for k in range(40):
        at = r.exponential(.18)
        if at < d - .2:
            f = r.uniform(2500, 7000)
            place(x, ring(.2, [(f, .5), (f * 1.53, .3)], .05, f'bb{k}') * r.uniform(.2, .6), at)
    for k, f in enumerate([1568, 1319, 1047, 784]):
        place(x, ring(.8, [(f, .5), (f * 2.01, .2)], .3, f'bbc{k}') * .5, .15 + k * .1)
    x += boom(d, 80, 40, .4, .6, 'bb-b')
    return reverb(x, 1.8, .35)

@sfx('barrier_blast')         # 결계 파괴 실패: 맵 전체 폭발
def _():
    d = 2.6
    r = rng('bx')
    n = int(SR * d)
    x = boom(d, 60, 22, 1.0, 1.6, 'bx-b')
    x += highpass(noise(n, r), 600) * env_exp(n, .25) * 1.8
    x += rubble(d, 120, 'bx-ru', 200, 4000, .8) * .9
    x += braam(d, 36.7, 'bx-br') * .5
    return reverb(x, 2.6, .35)

@sfx('summon')                # 소환 순간: 소용돌이 + 낮은 웅웅거림
def _():
    d = 1.1
    r = rng('sm')
    n = int(SR * d)
    swirl = svf_bp(noise(n, r), 600 + 400 * np.sin(2 * np.pi * 4 * tt(d)), 4) * env_ar(n, .1, .5) * 1.6
    x = swirl + boom(d, 70, 35, .2, .7, 'sm-b')
    return reverb(x, 1.6, .35)

@sfx('judgement')             # 심판·일식 판정: 징 + 충격
def _():
    d = 1.8
    x = gong(d, 73, 'jd-g') * 1.1 + boom(d, 75, 28, .7, 1.2, 'jd-b')
    return reverb(x, 2.2, .35)

# --- battle events ---

@sfx('phase_change')          # 페이즈 전환: 충격 + 위험 경고음 + 성가가 솟음
def _():
    d = 2.6
    n = int(SR * d)
    x = boom(d, 70, 24, 1.0, 1.4, 'pc-b')
    x += braam(d, 41.2, 'pc-br') * .8
    x += choir(d, [110, 130.8, 164.8, 220], 'a', 'pc-c') * env_ar(n, 1.0, .8, 1.2) * 1.0
    return reverb(x, 2.4, .35)

@sfx('boss_death')            # 보스 쓰러짐: 꺼져 가는 포효 + 무너지는 굉음
def _():
    d = 3.4
    n = int(SR * d)
    x = boom(d, 60, 20, 1.0, 1.6, 'bd-b')
    g = growl(2.2, 120, 45, 'o', 3, 'bd-g', vib=4, rough=.5) * env_ar(int(SR * 2.2), .05, 1.4, 1)
    place(x, g, .05, .8)
    place(x, rubble(2.6, 100, 'bd-ru', 200, 4000, 1.0), .2, .9)
    x += choir(d, [55, 82.4, 110], 'u', 'bd-c') * env_ar(n, .8, 2.0, 1) * .5
    return reverb(x, 3.0, .4)

@sfx('player_hurt')           # 플레이어 피격: 묵직하고 아픈 타격
def _():
    d = 0.5
    r = rng('ph')
    n = int(SR * d)
    x = boom(d, 120, 50, .9, 1.0, 'ph-b')
    x += drive(bandpass(noise(n, r), 900, 3500) * env_exp(n, .05) * 3, 2.5) * .6
    return reverb(x, .8, .15)

@sfx('heartbeat')             # 저체력 심장박동
def _():
    d = 0.75
    n = int(SR * d)
    x = np.zeros(n)
    place(x, lowpass(boom(.3, 62, 38, .05, 1.0, 'hb1', 400), 500), 0.0, 1.0)
    place(x, lowpass(boom(.3, 55, 35, .05, 1.0, 'hb2', 400), 500), 0.24, .7)
    return x


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

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=str(ROOT / 'Assets/StreamingAssets/Audio'))
    ap.add_argument('--only', nargs='*')
    args = ap.parse_args()
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for name, fn in S.items():
        if args.only and name not in args.only:
            continue
        x = np.asarray(fn(), dtype=float)
        if name in TELL:
            x = tell_shape(x, TELL[name])
        elif name in PUNCH:
            # Strikes must out-shout the wind-up that preceded them: soft-clip the transient
            # so the first 300 ms carry about twice the energy at the same peak.
            x = drive(x / np.max(np.abs(x)), PUNCH[name])
        if name in PRESENCE:
            # Big impacts are mostly sub-bass, which ears (and laptop speakers) hear as quiet.
            # Lift 200 Hz-5 kHz so the hit is felt as loud as it is heavy.
            x = x + bandpass(x, 200, 5000) * PRESENCE[name]
        x = finish(x)
        p = out / f'sfx_{name}.wav'
        write_wav(p, x)
        write_meta(p)
        print(f'{p.name:28s} {len(x) / SR:5.2f}s')

if __name__ == '__main__':
    main()
