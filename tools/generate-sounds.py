"""Generate original local WAV previews. No packages or network required."""
import argparse
import math
from pathlib import Path
import random
import struct
import wave

RATE = 44100


def write_sound(path, duration, sample, level=0.32):
    values = [sample(i / RATE) for i in range(round(RATE * duration))]
    peak = max(max(abs(v) for v in values), 1e-12)
    pcm = b''.join(struct.pack('<h', round(v / peak * level * 32767)) for v in values)
    with wave.open(str(path), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'obj' / 'sound-previews')
    folder = parser.parse_args().output
    folder.mkdir(parents=True, exist_ok=True)
    rng = random.Random(20261026)

    def click(t):
        value = 0.0
        for start, gain in ((0.012, 1.0), (0.078, 0.55)):
            u = t - start
            if u >= 0:
                attack = min(u / 0.0015, 1.0)
                value += gain * attack * math.exp(-u * 100) * (0.7 * rng.uniform(-1, 1) + 0.3 * math.sin(2 * math.pi * 1800 * u))
        return value

    def pop(t):
        u = t - 0.012
        if u < 0:
            return 0.0
        envelope = (1 - math.exp(-u * 700)) * math.exp(-u * 30)
        phase = 2 * math.pi * (420 * u + 160 * (1 - math.exp(-u * 35)) / 35)
        return envelope * (math.sin(phase) + 0.18 * math.sin(phase * 2))

    def chime(t):
        value = 0.0
        for start, freq, gain in ((0.015, 660, 0.7), (0.105, 990, 1.0)):
            u = t - start
            if u >= 0:
                value += gain * (1 - math.exp(-u * 500)) * math.exp(-u * 22) * (math.sin(2 * math.pi * freq * u) + 0.12 * math.sin(2 * math.pi * freq * 3 * u))
        return value

    soft_rng = random.Random(20261027)
    filtered_noise = 0.0

    def soft_tap(t):
        nonlocal filtered_noise
        u = t - 0.015
        if u < 0:
            return 0.0
        filtered_noise = 0.92 * filtered_noise + 0.08 * soft_rng.uniform(-1, 1)
        envelope = (1 - math.exp(-u * 260)) * math.exp(-u * 42)
        phase = 2 * math.pi * (310 * u + 90 * (1 - math.exp(-u * 30)) / 30)
        return envelope * (math.sin(phase) + 0.15 * filtered_noise)

    def warm_confirmation(t):
        value = 0.0
        for start, frequency, gain in ((0.015, 440, 0.6), (0.085, 550, 1.0)):
            u = t - start
            if u >= 0:
                envelope = (1 - math.exp(-u * 170)) * math.exp(-u * 25)
                value += gain * envelope * math.sin(2 * math.pi * frequency * u)
        return value

    shutter_rng = random.Random(20261028)
    shutter_noise = 0.0

    def gentle_shutter(t):
        nonlocal shutter_noise
        shutter_noise = 0.88 * shutter_noise + 0.12 * shutter_rng.uniform(-1, 1)
        value = 0.0
        for start, frequency, gain in ((0.015, 330, 1.0), (0.075, 470, 0.45)):
            u = t - start
            if u >= 0:
                envelope = (1 - math.exp(-u * 350)) * math.exp(-u * 65)
                value += gain * envelope * (math.sin(2 * math.pi * frequency * u) + 0.4 * shutter_noise)
        return value

    def rounded_bell(t):
        u = t - 0.015
        if u < 0:
            return 0.0
        envelope = (1 - math.exp(-u * 160)) * math.exp(-u * 25)
        return envelope * (math.sin(2 * math.pi * 740 * u) + 0.09 * math.sin(2 * math.pi * 1480 * u))

    def soft_success(t):
        u = t - 0.015
        if u < 0:
            return 0.0
        envelope = (1 - math.exp(-u * 110)) * math.exp(-u * 24)
        return envelope * sum(gain * math.sin(2 * math.pi * frequency * u)
                              for frequency, gain in ((523.25, 1.0), (659.25, 0.5), (783.99, 0.35)))

    write_sound(folder / '01-camera-click.wav', 0.22, click)
    write_sound(folder / '02-soft-pop.wav', 0.30, pop)
    write_sound(folder / '03-light-chime.wav', 0.43, chime)
    write_sound(folder / '04-soft-tap-louder.wav', 0.28, soft_tap, level=0.27)
    write_sound(folder / '05-warm-confirmation-louder.wav', 0.44, warm_confirmation, level=0.27)
    write_sound(folder / '06-gentle-shutter.wav', 0.28, gentle_shutter, level=0.27)
    write_sound(folder / '07-rounded-bell.wav', 0.44, rounded_bell, level=0.27)
    write_sound(folder / '08-soft-success.wav', 0.48, soft_success, level=0.27)
    for path in sorted(folder.glob('*.wav')):
        print(path)


if __name__ == '__main__':
    main()
