"""Nettoie les frappes de clavier : recale sur l'attaque, coupe avant la frappe suivante,
égalise le volume perçu, fondus courts. Lit les originaux, écrit dans le dossier du projet."""
import array, glob, math, os, sys, wave

SRC = sys.argv[1]
DST = sys.argv[2]

PRE_MS = 2          # marge avant l'attaque
MAX_AFTER_PEAK_MS = 110
FADE_IN_MS = 1
FADE_OUT_MS = 25
TARGET_PEAK_DB = -3.0   # le plus fort des sons après égalisation

def env_db(a, i, w, pk):
    seg = a[i:i + w]
    m = max((abs(x) for x in seg), default=0)
    return 20 * math.log10(max(m, 1) / pk)

clips = []
for f in sorted(glob.glob(os.path.join(SRC, "*.wav"))):
    w = wave.open(f)
    assert w.getnchannels() == 1 and w.getsampwidth() == 2, f
    r = w.getframerate()
    a = array.array("h", w.readframes(w.getnframes()))
    w.close()

    ms = r // 1000
    pk_i = max(range(len(a)), key=lambda i: abs(a[i]))
    pk = abs(a[pk_i])

    # Attaque : en remontant depuis le pic, premier point où l'enveloppe (1 ms) passe sous -30 dB.
    start = pk_i
    limit = max(0, pk_i - 20 * ms)
    while start > limit and env_db(a, start - ms, ms, pk) > -30:
        start -= ms
    start = max(0, start - PRE_MS * ms)

    # Fin : avant que l'enveloppe ne remonte (frappe suivante), au plus MAX_AFTER_PEAK_MS après le pic.
    end = min(len(a), pk_i + MAX_AFTER_PEAK_MS * ms)
    i = pk_i + 30 * ms
    floor = 0.0
    while i < end:
        d = env_db(a, i, 5 * ms, pk)
        floor = min(floor, d)
        if d > floor + 12 and d > -24:   # remontée nette = nouvelle frappe
            end = i - 3 * ms
            break
        i += 5 * ms

    seg = [float(x) for x in a[start:end]]
    n_in, n_out = FADE_IN_MS * ms, min(FADE_OUT_MS * ms, len(seg) // 2)
    for k in range(n_in):
        seg[k] *= k / n_in
    for k in range(n_out):
        seg[-1 - k] *= k / n_out

    # Volume perçu : RMS des 30 premières ms (le corps de la frappe).
    body = seg[: 30 * ms]
    rms = math.sqrt(sum(x * x for x in body) / len(body))
    clips.append((f, r, seg, rms, start, end, pk_i))

target_rms = min(c[3] for c in clips)  # référence provisoire
gains = [target_rms / c[3] for c in clips]
loudest = max(max(abs(x) for x in c[2]) * g for c, g in zip(clips, gains))
scale = (32767 * 10 ** (TARGET_PEAK_DB / 20)) / loudest

for (f, r, seg, rms, start, end, pk_i), g in zip(clips, gains):
    out = array.array("h", (max(-32768, min(32767, int(round(x * g * scale)))) for x in seg))
    path = os.path.join(DST, os.path.basename(f))
    w = wave.open(path, "wb")
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(r)
    w.writeframes(out.tobytes()); w.close()
    ms = r / 1000
    peak_db = 20 * math.log10(max(abs(x) for x in out) / 32768)
    print("%-16s attaque %4.0f ms  pic %4.0f ms  -> durée %3.0f ms  gain %+5.1f dB  pic %5.1f dBFS"
          % (os.path.basename(f), start / ms, pk_i / ms, (end - start) / ms,
             20 * math.log10(g * scale), peak_db))
