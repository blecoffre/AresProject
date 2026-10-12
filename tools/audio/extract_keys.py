"""Extrait des frappes isolées d'une prise de frappe continue.

Usage : python extract_keys.py <prise.wav> <dossier_sortie> <prefixe> [dossier_reference]

- Détecte chaque frappe à son attaque (montée brusque d'énergie).
- Regroupe l'appui et le relâchement d'une même touche (tout ce qui suit l'attaque de moins de 70 ms).
- Ne garde que les frappes « propres » : rien de fort juste avant l'attaque, et au moins 60 ms
  de son avant la frappe suivante.
- Coupe de l'attaque jusqu'à la frappe suivante (110 ms après le pic au plus), fondus courts.
- Aligne le volume sur les sons du dossier de référence (le corps de la frappe, 30 premières ms),
  pour que les nouvelles variantes se fondent dans la famille existante.
"""
import array, glob, math, os, sys, wave

GROUP_MS = 70          # appui + relâchement d'une même touche
MIN_AFTER_PEAK_MS = 60 # durée minimale exploitable avant la frappe suivante
MAX_AFTER_PEAK_MS = 110
PRE_MS = 2
FADE_IN_MS, FADE_OUT_MS = 1, 25
FRAME_MS = 2


def read_mono(path):
    w = wave.open(path)
    ch, sw, r, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
    raw = w.readframes(n)
    w.close()
    if sw == 2:
        data = array.array("h", raw)
        scale = 32768.0
    elif sw == 3:
        data = [int.from_bytes(raw[i:i + 3], "little", signed=True) for i in range(0, len(raw), 3)]
        scale = 8388608.0
    elif sw == 4:
        data = array.array("i", raw)
        scale = 2147483648.0
    else:
        raise SystemExit("Format non géré (%d octets/échantillon) : exporter en WAV 16 bits." % sw)
    mono = [sum(data[i + c] for c in range(ch)) / (ch * scale) for i in range(0, len(data), ch)]
    return mono, r


def body_rms(seg, r):
    body = seg[: 30 * r // 1000]
    return math.sqrt(sum(x * x for x in body) / max(len(body), 1))


def main():
    src, out_dir, prefix = sys.argv[1], sys.argv[2], sys.argv[3]
    ref_dir = sys.argv[4] if len(sys.argv) > 4 else None
    os.makedirs(out_dir, exist_ok=True)

    a, r = read_mono(src)
    ms = r // 1000
    fl = FRAME_MS * ms
    frames = [max(abs(x) for x in a[i:i + fl]) for i in range(0, len(a) - fl, fl)]
    peak = max(frames)
    floor_lvl = peak * 10 ** (-38 / 20)

    # Attaques : une trame 9 dB au-dessus du maximum des 10 ms précédentes.
    look = 10 // FRAME_MS
    onsets = []
    for k in range(look, len(frames)):
        prev = max(frames[k - look:k])
        if frames[k] > floor_lvl and frames[k] > prev * 2.8:
            t = k * fl
            if not onsets or t - onsets[-1] > GROUP_MS * ms:
                onsets.append(t)

    # Pic de chaque attaque, pour écarter les fragments trop faibles (bout de frappe voisine,
    # frottement) : une vraie frappe atteint au moins le quart du pic médian.
    def onset_peak(idx):
        st = onsets[idx]
        en = min(st + GROUP_MS * ms, onsets[idx + 1] if idx + 1 < len(onsets) else len(a))
        return max(abs(x) for x in a[st:en])
    peaks = sorted(onset_peak(i) for i in range(len(onsets)))
    median_peak = peaks[len(peaks) // 2] if peaks else 0

    kept, rejected = [], 0
    for idx, start in enumerate(onsets):
        nxt = onsets[idx + 1] if idx + 1 < len(onsets) else len(a)
        win_end = min(start + GROUP_MS * ms, nxt)
        pk_i = max(range(start, win_end), key=lambda i: abs(a[i]))
        pk = abs(a[pk_i])

        before = a[max(0, start - 10 * ms):start]
        noisy_before = before and max(abs(x) for x in before) > pk * 10 ** (-18 / 20)
        too_close = nxt - pk_i < MIN_AFTER_PEAK_MS * ms
        if noisy_before or too_close or pk < median_peak * 0.25:
            rejected += 1
            continue

        s = max(0, start - PRE_MS * ms)
        e = min(nxt - 3 * ms, pk_i + MAX_AFTER_PEAK_MS * ms)
        seg = a[s:e]
        n_in, n_out = FADE_IN_MS * ms, min(FADE_OUT_MS * ms, len(seg) // 2)
        for i in range(n_in):
            seg[i] *= i / n_in
        for i in range(n_out):
            seg[-1 - i] *= i / n_out
        kept.append((s, seg))

    if ref_dir:
        refs = []
        for f in glob.glob(os.path.join(ref_dir, "*.wav")):
            ra, rr = read_mono(f)
            refs.append(body_rms(ra, rr))
        target = sum(refs) / len(refs)
    else:
        target = 0.05

    for n, (s, seg) in enumerate(kept, 1):
        g = target / body_rms(seg, r)
        pcm = array.array("h", (max(-32768, min(32767, int(round(x * g * 32767)))) for x in seg))
        clipped = sum(1 for x in pcm if abs(x) >= 32767)
        path = os.path.join(out_dir, "%s%02d.wav" % (prefix, n))
        w = wave.open(path, "wb")
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(r)
        w.writeframes(pcm.tobytes()); w.close()
        print("%s  à %6.2f s  durée %3d ms  gain %+5.1f dB%s" % (
            os.path.basename(path), s / r, len(seg) // ms, 20 * math.log10(g),
            "  /!\\ saturé" if clipped else ""))

    print("\n%d attaques détectées, %d frappes gardées, %d écartées (collées à une voisine)."
          % (len(onsets), len(kept), rejected))


main()
