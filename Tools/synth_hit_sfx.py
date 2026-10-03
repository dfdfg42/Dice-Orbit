"""타격음 8종을 절차적으로 합성한다 (타격감 리워크 2026-10-03).
usage: python Tools/synth_hit_sfx.py Assets/Sounds/Combat
- 44.1kHz mono 16-bit WAV. 외부 음원·모델을 쓰지 않는다 (numpy + scipy 신호 합성).
- 층 쌓기: 딸깍(트랜지언트) + 몸통(피치가 떨어지는 사인) + 노이즈 꼬리 → tanh 소프트 클립 → 피크 정규화.
- 만화풍 UI에 맞게 짧고 둥글게 — 사실적인 타격음이 아니라 "퍽·팡·톡".
"""
import os
import sys
import wave
import numpy as np
from scipy import signal

SR = 44100
rng = np.random.default_rng(20261003)


def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def env_exp(t, tau, attack=0.002):
    """빠른 어택 + 지수 감쇠."""
    a = np.clip(t / attack, 0.0, 1.0)
    return a * np.exp(-t / tau)


def sweep_sine(t, f0, f1, tau_f):
    """주파수가 f0에서 f1으로 지수적으로 떨어지는(또는 오르는) 사인."""
    f = f1 + (f0 - f1) * np.exp(-t / tau_f)
    phase = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(phase)


def band_noise(n, lo, hi):
    x = rng.standard_normal(n)
    sos = signal.butter(4, [lo, hi], btype="bandpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def high_noise(n, lo):
    """딸깍용 밝은 노이즈 — 초고역(쉿 소리)은 자른다."""
    x = rng.standard_normal(n)
    sos = signal.butter(4, [lo, min(lo * 2.4, 9000)], btype="bandpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def swept_band_noise(n, centers, q=2.2):
    """중심 주파수가 시간에 따라 움직이는 대역 노이즈 (휘두르는 소리). 상태변수 필터를 샘플 단위로 돌린다."""
    x = rng.standard_normal(n)
    out = np.zeros(n)
    low = band = 0.0
    for i in range(n):
        f = 2.0 * np.sin(np.pi * min(centers[i], SR * 0.24) / SR)
        high = x[i] - low - band / q
        band += f * high
        low += f * band
        out[i] = band
    return out


def finish(x, drive=1.6, peak=0.89, fade=0.012, lowpass=None):
    x = np.tanh(x * drive)
    if lowpass:   # 포화가 만든 고역 배음과 노이즈의 쉿 소리를 깎는다 — 몸통(저·중역)이 주인공
        sos = signal.butter(4, lowpass, btype="lowpass", fs=SR, output="sos")
        x = signal.sosfilt(sos, x)
    n_fade = int(SR * fade)
    if n_fade > 0 and len(x) > n_fade:
        x[-n_fade:] *= np.linspace(1.0, 0.0, n_fade)
    m = np.max(np.abs(x))
    if m > 0:
        x = x / m * peak
    return x


def hit_light():
    t = t_axis(0.16)
    body = sweep_sine(t, 300, 150, 0.03) * env_exp(t, 0.035)
    noise = band_noise(len(t), 1200, 4200) * env_exp(t, 0.022) * 0.7
    click = high_noise(len(t), 3000) * env_exp(t, 0.004, 0.0005) * 0.3
    return finish(body * 1.3 + noise * 0.6 + click, drive=1.4, lowpass=5200)


def hit_medium():
    t = t_axis(0.26)
    body = sweep_sine(t, 210, 85, 0.045) * env_exp(t, 0.065)
    noise = band_noise(len(t), 800, 3600) * env_exp(t, 0.038) * 0.75
    crack = high_noise(len(t), 2600) * env_exp(t, 0.008, 0.0005) * 0.4
    return finish(body * 1.5 + noise * 0.6 + crack, drive=1.9, lowpass=4500)


def hit_heavy():
    t = t_axis(0.46)
    body = sweep_sine(t, 150, 48, 0.07) * env_exp(t, 0.13)
    sub = sweep_sine(t, 75, 38, 0.1) * env_exp(t, 0.16) * 0.6
    noise = band_noise(len(t), 450, 2600) * env_exp(t, 0.065) * 0.8
    crack = high_noise(len(t), 2200) * env_exp(t, 0.012, 0.0005) * 0.45
    return finish(body * 1.8 + sub + noise * 0.6 + crack, drive=2.6, lowpass=3600)


def hit_kill():
    base = hit_heavy()
    t = t_axis(0.62)
    x = np.zeros(len(t))
    x[: len(base)] += base * 0.9
    # 팡 — 밝은 종소리 세 개가 30ms 간격으로 올라간다
    for i, f in enumerate([880.0, 1320.0, 1760.0]):
        start = int(SR * (0.03 + 0.03 * i))
        tt = t[: len(t) - start]
        bell = (np.sin(2 * np.pi * f * tt) + 0.4 * np.sin(2 * np.pi * f * 2.01 * tt)) * env_exp(tt, 0.11, 0.001)
        x[start:] += bell * 0.35
    return finish(x, drive=1.5, lowpass=6500)


def block():
    t = t_axis(0.3)
    x = np.zeros(len(t))
    # 금속성 — 배음이 정수배가 아닌 부분음들
    for f, tau, g in [(1180, 0.09, 1.0), (1873, 0.07, 0.7), (2511, 0.06, 0.55), (3320, 0.045, 0.4), (4170, 0.035, 0.3)]:
        x += np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * env_exp(t, tau, 0.0008) * g
    click = high_noise(len(t), 4000) * env_exp(t, 0.003, 0.0004) * 0.9
    thud = sweep_sine(t, 240, 150, 0.03) * env_exp(t, 0.03) * 0.5
    return finish(x * 0.6 + click * 0.5 + thud, drive=1.3, peak=0.8, lowpass=8000)


def tick():
    t = t_axis(0.14)
    blip = sweep_sine(t, 520, 940, 0.05) * env_exp(t, 0.035, 0.004)   # 올라가는 방울 소리
    blip2 = sweep_sine(t, 1040, 1700, 0.05) * env_exp(t, 0.02, 0.004) * 0.25
    return finish(blip + blip2, drive=1.1, peak=0.7)


def swing():
    dur = 0.22
    t = t_axis(dur)
    k = t / dur
    centers = 500 + 2600 * np.sin(np.pi * np.clip(k * 1.15, 0, 1)) ** 2      # 올라갔다 내려오는 중심 주파수
    amp = np.sin(np.pi * k) ** 1.5                                           # 가운데가 볼록한 볼륨
    x = swept_band_noise(len(t), centers) * amp
    return finish(x, drive=1.0, peak=0.6, fade=0.02, lowpass=3600)


def monster_strike():
    dur = 0.42
    t = t_axis(dur)
    k = t / dur
    centers = 220 + 900 * np.sin(np.pi * np.clip(k * 2.2, 0, 1)) ** 2
    whoosh = swept_band_noise(len(t), centers, q=1.6) * np.clip(np.sin(np.pi * np.clip(k * 2.4, 0, 1)), 0, 1) * 0.6
    start = int(SR * 0.11)
    tt = t[: len(t) - start]
    thud = np.zeros(len(t))
    thud[start:] = sweep_sine(tt, 120, 42, 0.06) * env_exp(tt, 0.12) * 1.4 + band_noise(len(tt), 300, 1800) * env_exp(tt, 0.05) * 0.6
    return finish(whoosh + thud, drive=2.2, lowpass=3000)


SOUNDS = {
    "hit_light": hit_light,
    "hit_medium": hit_medium,
    "hit_heavy": hit_heavy,
    "hit_kill": hit_kill,
    "hit_block": block,
    "hit_tick": tick,
    "attack_swing": swing,
    "monster_strike": monster_strike,
}


def write_wav(path, x):
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def main():
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    for name, fn in SOUNDS.items():
        x = fn()
        path = os.path.join(out_dir, name + ".wav")
        write_wav(path, x)
        spec = np.abs(np.fft.rfft(x))
        freqs = np.fft.rfftfreq(len(x), 1 / SR)
        centroid = float((spec * freqs).sum() / max(spec.sum(), 1e-9))
        rms = float(np.sqrt(np.mean(x ** 2)))
        print(f"{name:15s} {len(x) / SR:5.2f}s peak={np.max(np.abs(x)):.2f} rms={rms:.3f} centroid={centroid:6.0f}Hz -> {path}")


if __name__ == "__main__":
    main()
