"""Deux musiques bouche-trou pour Ares, en boucle sans couture.

- menu : nappe sombre et lente (ré mineur), un terminal en veille. 32 s.
- jeu  : pulsation sobre à 100 BPM, basse filtrée, nappe, arpège discret. 16 mesures, 38,4 s.

Sans couture, par construction :
- toute fréquence et tout LFO continus font un nombre ENTIER de cycles sur la boucle ;
- les sons ponctuels (notes, percussions) qui débordent la fin sont repliés au début ;
- le bruit (non périodique) est rendu plus long puis fondu enchaîné sur lui-même.

Usage : python synth_music.py <dossier_sortie>
"""
import array, math, os, random, sys, wave

SR = 44100
TAU = 2 * math.pi
rng = random.Random(2077)


def q(f, loop):
    """Arrondit une fréquence pour qu'elle fasse un nombre entier de cycles sur la boucle."""
    return round(f * loop) / loop


def note(n):
    """Fréquence d'une note MIDI."""
    return 440.0 * 2 ** ((n - 69) / 12)


class Stereo:
    def __init__(self, seconds):
        self.n = int(seconds * SR)
        self.l = [0.0] * self.n
        self.r = [0.0] * self.n

    def add(self, mono, at, gain=1.0, pan=0.0, wrap=True):
        """pan : -1 gauche, +1 droite (loi à puissance constante). Replie ce qui dépasse."""
        gl = gain * math.cos((pan + 1) * math.pi / 4)
        gr = gain * math.sin((pan + 1) * math.pi / 4)
        o = int(at * SR)
        n = self.n
        for i, v in enumerate(mono):
            j = o + i
            if j >= n:
                if not wrap:
                    break
                j %= n
            self.l[j] += v * gl
            self.r[j] += v * gr


def polyblep_saw(freq, dur, phase=0.0):
    """Dent de scie à bande limitée bon marché (PolyBLEP) : pas de repliement audible."""
    n = int(dur * SR)
    out = [0.0] * n
    dt = freq / SR
    p = phase
    for i in range(n):
        v = 2 * p - 1
        if p < dt:
            t = p / dt
            v -= t + t - t * t - 1
        elif p > 1 - dt:
            t = (p - 1) / dt
            v -= t * t + t + t + 1
        out[i] = v
        p += dt
        if p >= 1:
            p -= 1
    return out


def lowpass(x, fc):
    f = fc if callable(fc) else (lambda i, c=fc: c)
    y = 0.0
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        a = 1 - math.exp(-TAU * f(i) / SR)
        y += a * (v - y)
        out[i] = y
    return out


def env_adsr(n, a, d, s, r):
    """Enveloppe en échantillons : attaque, déclin vers s, maintien, relâchement final."""
    out = [0.0] * n
    ai, di, ri = int(a * SR), int(d * SR), int(r * SR)
    for i in range(n):
        if i < ai:
            g = i / max(ai, 1)
        elif i < ai + di:
            g = 1 - (1 - s) * (i - ai) / max(di, 1)
        else:
            g = s
        if i >= n - ri:
            g *= (n - i) / max(ri, 1)
        out[i] = g
    return out


def finish(st, rms_db, ceiling_db=-1.0):
    tot = sum(v * v for v in st.l) + sum(v * v for v in st.r)
    rms = math.sqrt(tot / (2 * st.n)) or 1.0
    g = 10 ** (rms_db / 20) / rms
    pk = max(max(abs(v) for v in st.l), max(abs(v) for v in st.r)) * g
    c = 10 ** (ceiling_db / 20)
    if pk > c:
        g *= c / pk
    st.l = [v * g for v in st.l]
    st.r = [v * g for v in st.r]
    return st


def save(st, path):
    pcm = array.array("h")
    for a, b in zip(st.l, st.r):
        pcm.append(max(-32767, min(32767, int(round(a * 32767)))))
        pcm.append(max(-32767, min(32767, int(round(b * 32767)))))
    w = wave.open(path, "wb")
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(pcm.tobytes()); w.close()


def looped_noise(seconds, fc, xfade=2.0):
    """Bruit filtré qui boucle : rendu plus long, puis la fin fondue sur le début."""
    n, x = int(seconds * SR), int(xfade * SR)
    raw = lowpass([rng.uniform(-1, 1) for _ in range(n + x)], fc)
    out = raw[:n]
    for i in range(x):
        t = i / x
        out[i] = raw[n + i] * math.cos(t * math.pi / 2) + out[i] * math.sin(t * math.pi / 2)
    return out


# ---------------------------------------------------------------- menu

def menu(loop=32.0):
    """Nappe de ré mineur 7 : quelques voix sinusoïdales désaccordées, chacune respirant à son
    propre rythme lent, un souffle filtré, et de rares « pings » de données au loin."""
    st = Stereo(loop)
    n = st.n
    # (note MIDI, gain, pan, cycles de respiration sur la boucle, phase de respiration)
    voices = [(38, 0.9, 0.0, 1, 0.0), (45, 0.55, -0.3, 2, 1.3), (50, 0.5, 0.3, 3, 2.1),
              (53, 0.35, -0.45, 2, 4.0), (60, 0.22, 0.5, 3, 0.7), (64, 0.12, -0.2, 4, 3.3)]
    for midi, gain, pan, breaths, bph in voices:
        f = q(note(midi), loop)
        beat = q(0.12 + 0.05 * (midi % 5), loop)  # désaccord lent, lui aussi périodique
        w1, w2, wb = TAU * f / SR, TAU * (f + beat) / SR, TAU * breaths / loop / SR
        mono = [0.0] * n
        for i in range(n):
            b = 0.55 + 0.45 * math.sin(wb * i + bph)
            mono[i] = b * (math.sin(w1 * i) + 0.7 * math.sin(w2 * i) + 0.12 * math.sin(2 * w1 * i))
        st.add(mono, 0, gain, pan)

    air = looped_noise(loop, lambda i: 500.0)
    wa = TAU * 2 / loop / SR
    st.add([v * (0.6 + 0.4 * math.sin(wa * i)) for i, v in enumerate(air)], 0, 0.35, 0.0)

    # Pings : notes aiguës de la gamme, longue extinction, écho gauche/droite.
    for at, midi in ((3.1, 81), (11.7, 76), (17.9, 86), (26.3, 81)):
        f = note(midi)
        m = int(2.5 * SR)
        ping = [math.sin(TAU * f * i / SR) * math.exp(-i / (0.5 * SR)) * min(1, i / 40) for i in range(m)]
        for k, (dt, pan) in enumerate(((0.0, -0.4), (0.45, 0.5), (0.9, -0.6))):
            st.add(ping, at + dt, 0.07 * (0.55 ** k), pan)
    return finish(st, -21)


# ---------------------------------------------------------------- jeu

def duck(t, period, depth=0.45, down=0.005, up=0.25):
    """Creux de volume calé sur la grosse caisse, en temps GLOBAL de la boucle : il tombe en 5 ms
    (une chute instantanée ferait une petite marche, donc un clic) puis remonte en 250 ms."""
    p = t % period
    if p < down:
        return 1.0 - depth * p / down
    return 1.0 - depth + depth * min(1.0, (p - down) / up)


def game(bpm=100, bars=16):
    beat = 60.0 / bpm
    bar = 4 * beat
    loop = bars * bar
    st = Stereo(loop)

    # Grille d'accords, 4 mesures chacun : Rém - Sib - Solm - La (tension qui relance la boucle).
    chords = [(38, (50, 53, 57)), (34, (50, 53, 58)), (31, (50, 55, 58)), (33, (49, 52, 57))]

    # Grosse caisse douce sur les temps 1 et 3 : pulsation sans marteler.
    kick_len = int(0.35 * SR)
    kick = []
    ph = 0.0
    for i in range(kick_len):
        t = i / SR
        ph += TAU * (45 + 60 * math.exp(-t / 0.03)) / SR
        kick.append(math.sin(ph) * math.exp(-t / 0.12) * min(1, i / 20))
    # Charleston fermé sur les contretemps, très en retrait.
    hat = lowpass([rng.uniform(-1, 1) for _ in range(int(0.03 * SR))], 9000)
    hat = [v - u for v, u in zip(hat, lowpass(hat, 6000))]
    hat = [v * math.exp(-i / (0.008 * SR)) for i, v in enumerate(hat)]

    for b in range(bars):
        for k in (0, 2):
            st.add(kick, b * bar + k * beat, 0.9)
        for k in range(4):
            st.add(hat, b * bar + k * beat + beat / 2, 0.18, 0.35 if k % 2 else -0.35)

    # Basse : croches sur la fondamentale, filtre qui s'ouvre puis se ferme à chaque note.
    for b in range(bars):
        root = chords[(b // 4) % 4][0]
        f = note(root)
        for k in range(8):
            at = b * bar + k * beat / 2
            dur = beat / 2 * 0.9
            x = polyblep_saw(f, dur)
            m = len(x)
            x = lowpass(x, lambda i, m=m: 180 + 900 * math.exp(-i / (0.04 * SR)))
            e = env_adsr(m, 0.004, 0.08, 0.6, 0.03)
            accent = 1.0 if k % 2 == 0 else 0.7
            st.add([v * g for v, g in zip(x, e)], at, 0.55 * accent)

    # Nappe : accords tenus, filtrés, avec un « pompage » sous la grosse caisse.
    for c in range(bars // 4):
        _, tones = chords[c % 4]
        at = c * 4 * bar
        dur = 4 * bar + 0.8
        for j, midi in enumerate(tones):
            f = note(midi)
            x = [a + b for a, b in zip(polyblep_saw(f, dur), polyblep_saw(f * 1.004, dur, 0.37))]
            x = lowpass(x, 900)
            e = env_adsr(len(x), 0.9, 0.5, 0.8, 0.9)
            pump = [duck(at + i / SR, 2 * beat) for i in range(len(x))]
            st.add([v * g * p for v, g, p in zip(x, e, pump)], at, 0.07, (-0.5, 0.0, 0.5)[j])

    # Arpège : seulement sur la seconde moitié, pour que la boucle respire. Écho décalé.
    for b in range(bars // 2, bars):
        tones = chords[(b // 4) % 4][1]
        for k in range(8):
            midi = tones[(k * 2 + b) % 3] + 24
            f = note(midi)
            m = int(0.25 * SR)
            x = [math.sin(TAU * f * i / SR) * math.exp(-i / (0.06 * SR)) * min(1, i / 30) for i in range(m)]
            at = b * bar + k * beat / 2
            st.add(x, at, 0.06, -0.3)
            st.add(x, at + 3 * beat / 4, 0.035, 0.5)
    return finish(st, -19)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in (("music_menu_placeholder", menu), ("music_game_placeholder", game)):
        st = fn()
        save(st, os.path.join(out, name + ".wav"))
        # Contrôle du raccord : écart entre le dernier et le premier échantillon, comparé à
        # l'écart moyen entre deux échantillons voisins. Un rapport proche de 1 = pas de clic.
        jump = abs(st.l[0] - st.l[-1])
        avg = sum(abs(st.l[i] - st.l[i - 1]) for i in range(1, st.n)) / (st.n - 1)
        print("%-24s %5.1f s  raccord : saut %.4f / écart moyen %.4f" % (name, st.n / SR, jump, avg))
