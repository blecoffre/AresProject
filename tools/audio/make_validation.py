"""Validation (Entrée) : les frappes les plus lourdes de la prise, alourdies pour se distinguer
de KeyPress tout en restant le même clavier — ralenties (plus graves), un impact grave dessous."""
import wave, array, math, os, sys
PICKS = ["s_11", "s_14", "s_16", "s_33", "s_10"]
SPEED = 0.88          # < 1 : plus lent, donc plus grave (~ -2,2 demi-tons)
THUMP_GAIN = 0.35     # impact grave, relatif au pic de la frappe
TARGET_RMS_DB = -26.0 # KeyPress : médiane -30 dB ; la validation pèse un peu plus
out_dir = sys.argv[1]; os.makedirs(out_dir, exist_ok=True)
for n, name in enumerate(PICKS, 1):
    w = wave.open('all_strokes/%s.wav' % name); r = w.getframerate()
    x = [v / 32768 for v in array.array('h', w.readframes(w.getnframes()))]; w.close()
    m = int(len(x) / SPEED)
    y = []
    for i in range(m):                      # rééchantillonnage linéaire
        p = i * SPEED; k = int(p); f = p - k
        y.append(x[k] * (1 - f) + (x[k + 1] if k + 1 < len(x) else 0) * f)
    pk = max(abs(v) for v in y)
    ph = 0.0
    for i in range(int(0.05 * r)):          # impact : sinus 95 -> 55 Hz, 50 ms
        t = i / r
        ph += 2 * math.pi * (95 - 40 * t / 0.05) / r
        y[i] += THUMP_GAIN * pk * math.sin(ph) * math.exp(-t / 0.015) * min(1, i / (0.001 * r))
    fo = int(0.02 * r)
    for i in range(fo): y[-1 - i] *= i / fo
    rms = math.sqrt(sum(v * v for v in y) / len(y))
    g = 10 ** (TARGET_RMS_DB / 20) / rms
    g = min(g, 10 ** (-1 / 20) / max(abs(v) for v in y))
    pcm = array.array('h', (max(-32767, min(32767, int(round(v * g * 32767)))) for v in y))
    o = wave.open(os.path.join(out_dir, 'ui_validation_%02d.wav' % n), 'wb')
    o.setnchannels(1); o.setsampwidth(2); o.setframerate(r); o.writeframes(pcm.tobytes()); o.close()
    print('ui_validation_%02d  <- %s  %d ms  RMS %.1f dB' % (n, name, len(y) * 1000 // r, 20 * math.log10(rms * g)))
