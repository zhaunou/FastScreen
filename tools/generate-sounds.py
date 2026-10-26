"""Generate original local WAV previews. No packages or network required."""
import argparse
import math
from pathlib import Path
import random
import struct
import wave

RATE = 44100


def write_sound(path, duration, sample):
    values = [sample(i / RATE) for i in range(round(RATE * duration))]
    peak = max(max(abs(v) for v in values), 1e-12)
    pcm = b''.join(struct.pack('<h', round(v / peak * 0.32 * 32767)) for v in values)
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

    write_sound(folder / '01-camera-click.wav', 0.22, click)
    write_sound(folder / '02-soft-pop.wav', 0.30, pop)
    write_sound(folder / '03-light-chime.wav', 0.43, chime)
    for path in sorted(folder.glob('*.wav')):
        print(path)


if __name__ == '__main__':
    main()
