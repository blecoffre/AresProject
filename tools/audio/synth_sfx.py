"""Synthèse des sons d'événements d'Ares — style « terminal militaire » : synthé froid et sobre.

Python standard uniquement. Chaque son est décrit par quelques réglages en tête de sa fonction :
c'est là qu'on retouche « plus grave », « plus court », « moins agressif ».

Usage : python synth_sfx.py <dossier_sortie>
"""
import array, math, os, random, sys, wave

SR = 48000
TAU = 2 * math.pi
rng = random.Random(1337)  # graine fixe : régénérer donne exactement les mêmes sons


# ---------------------------------------------------------------- briques de base

def silence(sec):
    return [0.0] * int(sec * SR)


def mix(dst, src, at=0.0, gain=1.0):
    o = int(at * SR)
    need = o + len(src)
    if need > len(dst):
        dst.extend([0.0] * (need - len(dst)))
    for i, v in enumerate(src):
        dst[o + i] += v * gain
    return dst


def glide(f0, f1, dur, curve="exp"):
    """Fréquence en fonction du temps (secondes)."""
    if curve == "exp" and f0 > 0 and f1 > 0:
        k = math.log(f1 / f0)
        return lambda t: f0 * math.exp(k * min(t / dur, 1.0))
    return lambda t: f0 + (f1 - f0) * min(t / dur, 1.0)


def osc(freq, dur, shape="sine", harmonics=8, drive=3.0, phase=0.0):
    """Oscillateur à fréquence constante ou variable (fonction du temps). Formes à bande
    limitée (additive) : pas de repliement, donc pas de sifflement parasite dans les aigus."""
    f = freq if callable(freq) else (lambda t, c=freq: c)
    n = int(dur * SR)
    out = [0.0] * n
    ph = phase
    nyq = SR / 2
    for i in range(n):
        fi = f(i / SR)
        ph += TAU * fi / SR
        if shape == "sine":
            v = math.sin(ph)
        elif shape == "soft":       # sinus saturé : carré arrondi, sans dureté
            v = math.tanh(drive * math.sin(ph)) / math.tanh(drive)
        elif shape == "saw":
            v = 0.0
            for k in range(1, harmonics + 1):
                if k * fi >= nyq:
                    break
                v += math.sin(k * ph) / k
            v *= 0.55
        elif shape == "square":
            v = 0.0
            for k in range(1, 2 * harmonics, 2):
                if k * fi >= nyq:
                    break
                v += math.sin(k * ph) / k
            v *= 0.8
        else:
            raise ValueError(shape)
        out[i] = v
    return out


def noise(dur):
    return [rng.uniform(-1, 1) for _ in range(int(dur * SR))]


def env(x, attack=0.003, decay=None, hold=0.0, release=0.01):
    """Attaque linéaire, maintien, puis décroissance exponentielle (decay = constante de temps)
    ou extinction linéaire en fin de son."""
    n = len(x)
    a, h, r = int(attack * SR), int(hold * SR), int(release * SR)
    for i in range(n):
        g = 1.0
        if i < a:
            g = i / a
        elif decay is not None and i >= a + h:
            g = math.exp(-(i - a - h) / (decay * SR))
        if i >= n - r and r > 0:
            g *= (n - i) / r
        x[i] *= g
    return x


def lowpass(x, fc):
    f = fc if callable(fc) else (lambda t, c=fc: c)
    y = 0.0
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        a = 1 - math.exp(-TAU * f(i / SR) / SR)
        y += a * (v - y)
        out[i] = y
    return out


def highpass(x, fc):
    lp = lowpass(x, fc)
    return [v - l for v, l in zip(x, lp)]


def bandpass(x, fc, q=2.0):
    """Filtre d'état variable (Chamberlin), fréquence pouvant varier dans le temps."""
    f = fc if callable(fc) else (lambda t, c=fc: c)
    low = band = 0.0
    damp = 1.0 / q
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        k = 2 * math.sin(math.pi * min(f(i / SR), SR / 6) / SR)
        low += k * band
        high = v - low - damp * band
        band += k * high
        out[i] = band
    return out


def crush(x, hold_fn, bits_fn):
    """Écrasement numérique : échantillonnage maintenu et quantification grossière, croissants."""
    out = [0.0] * len(x)
    held, count = 0.0, 0
    for i, v in enumerate(x):
        t = i / SR
        if count <= 0:
            levels = 2 ** max(2, bits_fn(t))
            held = round(v * levels) / levels
            count = max(1, int(hold_fn(t)))
        count -= 1
        out[i] = held
    return out


def tone(freq, dur, partials=((1, 1.0),), **envargs):
    """Une note : somme de partiels sinusoïdaux (rang, amplitude), enveloppée."""
    x = [0.0] * int(dur * SR)
    for rank, amp in partials:
        mix(x, osc(freq * rank if not callable(freq) else (lambda t, r=rank: freq(t) * r), dur), 0, amp)
    return env(x, **envargs)


def finish(x, rms_db, fade_out=0.01, ceiling_db=-1.0):
    """Coupe le silence final, fondu de sortie, puis règle le NIVEAU MOYEN (RMS) : c'est lui
    que l'oreille compare, pas le pic. Un sinus pur à 1 kHz et un bruit balayé au même pic ne
    sonnent pas du tout aussi fort. Le pic reste plafonné pour ne jamais saturer."""
    end = len(x)
    while end > 0 and abs(x[end - 1]) < 1e-4:
        end -= 1
    x = x[:end]
    r = min(int(fade_out * SR), len(x) // 2)
    for i in range(r):
        x[-1 - i] *= i / r
    rms = math.sqrt(sum(v * v for v in x) / len(x)) or 1.0
    g = 10 ** (rms_db / 20) / rms
    pk = max(abs(v) for v in x) * g
    ceiling = 10 ** (ceiling_db / 20)
    if pk > ceiling:
        g *= ceiling / pk
    return [v * g for v in x]


def save(x, path):
    pcm = array.array("h", (max(-32767, min(32767, int(round(v * 32767)))) for v in x))
    w = wave.open(path, "wb")
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(pcm.tobytes()); w.close()


# ---------------------------------------------------------------- les sons

def refusal(low=196.0, interval=0.75):
    """« Pas encore » : deux notes graves descendantes, carré arrondi, étouffé. Très discret."""
    x = []
    for k, f in enumerate((low, low * interval)):
        n = osc(f, 0.05, "soft", drive=3.0)
        n = env(lowpass(n, 1400), attack=0.003, decay=0.025, release=0.008)
        mix(x, n, k * 0.052)
    return finish(x, -31)  # au niveau d'une frappe de KeyPress (médiane -30 dB) : discret


def trace_tier1():
    """Palier 1 : un bip simple, sec. « Attention. »"""
    x = tone(880, 0.11, ((1, 1.0), (3, 0.18)), attack=0.002, hold=0.07, decay=0.03, release=0.012)
    return finish(x, -18)


def trace_tier2():
    """Palier 2 : deux bips plus aigus, plus rapprochés."""
    x = []
    for k in range(2):
        mix(x, tone(1046.5, 0.075, ((1, 1.0), (3, 0.22)), attack=0.002, hold=0.05, decay=0.02, release=0.01), k * 0.12)
    return finish(x, -17)


def trace_tier3():
    """Palier 3 : trois bips rapides en seconde mineure (battement âpre), légèrement saturés."""
    x = []
    for k in range(3):
        b = []
        mix(b, osc(1174.7, 0.06, "soft", drive=1.8))
        mix(b, osc(1244.5, 0.06, "soft", drive=1.8))
        mix(x, env(lowpass(b, 5000), attack=0.002, hold=0.04, decay=0.02, release=0.008), k * 0.09)
    return finish(x, -16)


def seizure():
    """Saisie Fédérale : une sirène qui s'effondre pendant que le système se fait écraser."""
    dur = 1.6
    siren = osc(glide(900, 140, 1.4), dur, "saw", harmonics=10)
    siren = lowpass(siren, glide(7000, 700, dur))
    siren = env(siren, attack=0.005, hold=1.0, decay=0.25, release=0.05)
    siren = crush(siren, hold_fn=lambda t: 1 + 15 * min(1, max(0, (t - 0.25) / 1.1)) ** 2,
                  bits_fn=lambda t: 12 - 8 * min(1, max(0, (t - 0.2) / 1.2)))
    x = []
    mix(x, siren, 0, 0.8)
    burst = env(highpass(noise(0.09), 2500), attack=0.001, decay=0.03, release=0.01)
    mix(x, burst, 0, 0.6)
    thump = env(osc(glide(90, 32, 0.4), 0.45, "sine"), attack=0.002, decay=0.15, release=0.03)
    mix(x, thump, 0.0, 1.0)
    return finish(x, -15, fade_out=0.08)


def victory():
    """Victoire : arpège montant lumineux qui se résout sur une note tenue."""
    notes = (523.25, 659.25, 783.99, 1046.5)
    x = []
    for k, f in enumerate(notes[:-1]):
        mix(x, tone(f, 0.35, ((1, 1.0), (2, 0.3), (3, 0.12)), attack=0.004, decay=0.12, release=0.03), k * 0.095, 0.7)
    last_at = 3 * 0.095
    final = tone(1046.5, 1.4, ((1, 1.0), (2, 0.25)), attack=0.006, decay=0.6, release=0.2)
    shimmer = tone(1049.5, 1.4, ((1, 1.0),), attack=0.02, decay=0.6, release=0.2)  # léger désaccord : chorus
    mix(x, final, last_at, 0.8)
    mix(x, shimmer, last_at, 0.35)
    pad = []
    for f in (261.63, 329.63, 392.0):
        mix(pad, tone(f, 1.7, ((1, 1.0),), attack=0.25, decay=0.9, release=0.3), 0, 0.22)
    mix(x, pad, 0.05)
    return finish(x, -17, fade_out=0.2)


def prestige_purchase():
    """Achat de prestige : « accès accordé », deux notes montantes et un reflet aigu."""
    x = []
    mix(x, env(highpass(noise(0.004), 3000), attack=0.0005, release=0.002), 0, 0.25)
    mix(x, tone(783.99, 0.12, ((1, 1.0), (2, 0.25)), attack=0.003, decay=0.06, release=0.02), 0.0, 0.8)
    mix(x, tone(1174.66, 0.45, ((1, 1.0), (2, 0.25)), attack=0.003, decay=0.16, release=0.05), 0.075, 0.9)
    mix(x, tone(2349.3, 0.35, ((1, 1.0),), attack=0.01, decay=0.1, release=0.05), 0.08, 0.12)
    return finish(x, -17, fade_out=0.05)


def exfiltration():
    """Terre Brûlée : une rafale de données qui accélère, se coupe net, puis un choc sourd."""
    x = []
    run = 0.8
    t, gap = 0.0, 0.06
    while t < run:
        p = t / run
        f = rng.uniform(600, 1800) * (1 + 1.2 * p)
        d = rng.uniform(0.006, 0.012)
        b = env(osc(f, d, "soft", drive=4.0), attack=0.0008, release=0.003)
        mix(x, b, t, 0.35 + 0.5 * p)
        t += gap
        gap = max(0.007, gap * 0.9)
    whoosh = lowpass(noise(run), glide(400, 9000, run))
    whoosh = env(whoosh, attack=run * 0.8, release=0.002)
    mix(x, whoosh, 0, 0.5)
    x = x[:int(run * SR)]  # la coupure nette fait partie du son
    thunk = env(osc(glide(75, 38, 0.3), 0.32, "soft", drive=1.5), attack=0.002, decay=0.1, release=0.03)
    mix(x, thunk, run + 0.01, 1.0)
    return finish(x, -15, fade_out=0.03)


def data_wiper():
    """Data Wiper : la purge. Bruit balayé de l'aigu au grave, chute grave saturée, crépitements."""
    dur = 0.9
    sweep = bandpass(noise(dur), glide(7000, 110, 0.75), q=3.0)
    sweep = env(sweep, attack=0.01, hold=0.3, decay=0.2, release=0.05)
    x = []
    mix(x, sweep, 0, 1.0)
    drop = osc(glide(170, 38, 0.5), 0.55, "soft", drive=2.2)
    mix(x, env(drop, attack=0.004, decay=0.18, release=0.04), 0.02, 0.8)
    for _ in range(14):
        at = rng.uniform(0.0, 0.3)
        c = env(highpass(noise(0.003), 4000), attack=0.0003, release=0.002)
        mix(x, c, at, rng.uniform(0.3, 0.7))
    return finish(x, -16, fade_out=0.06)


SOUNDS = [
    ("sfx_refusal_01", lambda: refusal(196.0, 0.75)),
    ("sfx_refusal_02", lambda: refusal(185.0, 0.7)),
    ("sfx_trace_tier1", trace_tier1),
    ("sfx_trace_tier2", trace_tier2),
    ("sfx_trace_tier3", trace_tier3),
    ("sfx_seizure", seizure),
    ("sfx_victory", victory),
    ("sfx_prestige_purchase", prestige_purchase),
    ("sfx_exfiltration", exfiltration),
    ("sfx_data_wiper", data_wiper),
]

if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in SOUNDS:
        x = fn()
        save(x, os.path.join(out, name + ".wav"))
        onset = next(i for i, v in enumerate(x) if abs(v) > 0.01) / SR * 1000
        pk = 20 * math.log10(max(abs(v) for v in x))
        rms = 20 * math.log10(math.sqrt(sum(v * v for v in x) / len(x)))
        print("%-24s %5d ms  pic %5.1f dBFS  RMS %5.1f dB  attaque %4.1f ms" % (name, len(x) * 1000 // SR, pk, rms, onset))
