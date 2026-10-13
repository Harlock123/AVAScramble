#!/usr/bin/env python3
"""Synthesises AVAScramble's sound effects and tunes into ../src/AVAScramble/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAScramble", "Sounds")
RNG = random.Random(1981)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def engine_drone(seconds=1.5):
    """The ship's engine: a low throbbing buzz, made to loop without a seam."""
    n = int(RATE * seconds)
    out, ph1, ph2 = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f1 = 80 + 4 * math.sin(2 * math.pi * 3 * t / seconds)
        ph1 += f1 / RATE
        ph2 += 120 / RATE
        s = voice("saw", ph1) * 0.5 + voice("square", ph2) * 0.15 + RNG.uniform(-1, 1) * 0.08
        s *= 0.8 + 0.2 * math.sin(2 * math.pi * 12 * t / seconds)
        out.append(s)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Laser.wav", tone(0.07, 2600, 1200, "pulse", release=1.2), 0.24)
    write("BombDrop.wav", tone(0.5, 1600, 500, "tri", attack=0.01, release=0.3), 0.3)
    write("Boom.wav", mix(tone(0.35, 900, 70, "noise", release=0.7), tone(0.25, 180, 60, "square", release=1.0)), 0.5)
    write("Fuel.wav", cat(*[tone(0.05, hz(n), kind="tri", release=0.4) for n in ["C6", "E6", "G6", "C7", "E7"]]), 0.4)
    write("Mystery.wav", cat(*[tone(0.05, f, kind="pulse", release=0.4) for f in [900, 1400, 1100, 1700, 1300, 2000]]), 0.38)
    write("RocketLaunch.wav", mix(tone(0.6, 200, 900, "noise", release=0.5), tone(0.6, 120, 420, "saw", release=0.6, gain=0.5)), 0.32)
    write("Ufo.wav", tone(0.6, 600, 600, "square", release=0.4, vibrato=0.2, vib_rate=14), 0.22)
    write("Death.wav", cat(mix(tone(1.1, 1000, 40, "noise", release=0.5), tone(1.1, 380, 45, "saw", release=0.6)), silence(0.1)), 0.6)
    write("LowFuel.wav", cat(*[tone(0.12, f, kind="square", release=0.2) for f in [1200, 900, 1200, 900, 1200, 900]]), 0.4)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["E6", "G#6", "B6", "E7"] * 2]), 0.45)
    write("Engine.wav", engine_drone(), 0.42)

    write("TitleTune.wav", tune(138,
        [("E5", 0.5), ("G5", 0.5), ("B5", 0.5), ("E6", 0.5), ("D6", 0.5), ("B5", 0.5), ("G5", 1),
         ("A5", 0.5), ("C6", 0.5), ("E6", 0.5), ("C6", 0.5), ("B5", 1), ("G5", 1),
         ("F#5", 0.5), ("A5", 0.5), ("C6", 0.5), ("A5", 0.5), ("G5", 0.5), ("F#5", 0.5), ("D5", 1),
         ("E5", 0.5), ("G5", 0.5), ("B5", 0.5), ("D#6", 0.5), ("E6", 2)],
        [(n, 1) for n in ["E2", "B2", "E2", "B2", "A2", "E3", "E2", "B2", "D2", "A2", "D2", "B1", "E2", "B2", "E2", "E2"]]), 0.6)
    write("StartTune.wav", tune(176,
        [("B4", 0.25), ("E5", 0.25), ("G5", 0.25), ("B5", 0.25), ("E6", 0.5), ("D6", 0.25), ("E6", 0.25), ("B5", 0.5), ("E6", 1)],
        [("E2", 1), ("B2", 1), ("E3", 1)]), 0.6)
    write("BaseDown.wav", tune(150,
        [("E5", 0.25), ("G#5", 0.25), ("B5", 0.25), ("E6", 0.25), ("G#6", 0.5), ("B6", 0.5), ("E7", 1.5), ("-", 0.5),
         ("B6", 0.25), ("G#6", 0.25), ("B6", 0.25), ("E7", 2)],
        [("E2", 1), ("B2", 1), ("E3", 2), ("B2", 1), ("E3", 2)]), 0.6)
    write("GameOver.wav", tune(84,
        [("B4", 1), ("G4", 1), ("E4", 1), ("F#4", 1), ("D#4", 1), ("E4", 2), ("-", 1)],
        [("E2", 2), ("C2", 2), ("B1", 2), ("E2", 2)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
