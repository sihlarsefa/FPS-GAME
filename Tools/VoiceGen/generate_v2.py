#!/usr/bin/env python3
"""HAREKAT Turkce telsiz replikleri v2 uretici (Piper + macOS Yelda, 3 stres).
Kullanim: <venv>/bin/python generate_v2.py <piper_model_dir> [--limit N]
Cikti: Assets/_Project/Resources/Audio/Voice/v2/<ses>/<stres>/<id>.wav + voice_manifest_v2.json
Ucretli API / hesap yok. Eski Voice/*.wav dosyalarina dokunmaz."""
import csv, json, os, subprocess, sys, tempfile, wave, math
import numpy as np
from scipy.signal import resample_poly
import pyloudnorm as pyln

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets/_Project/Resources/Audio/Voice/v2")
SR = 22050

STRESS = {  # length_scale carpani, noise_scale, noise_w, pitch carpani, hedef LUFS, maks bosluk(sn)->hedef bosluk
    "sakin":    dict(ls=1.00, ns=0.60, nw=0.70, pitch=1.00, lufs=-16.0, say_rate=170, gap=None),
    "catisma":  dict(ls=0.84, ns=0.70, nw=0.80, pitch=1.04, lufs=-14.5, say_rate=205, gap=(0.22, 0.14)),
    "panik":    dict(ls=0.70, ns=0.80, nw=0.90, pitch=1.09, lufs=-13.0, say_rate=245, gap=(0.12, 0.05)),
}
# ses: kaynak + kalicı perde/formant carpani (kisi ayrimi)
VOICES = {
    "dfki_er":    dict(engine="piper", model="tr_TR-dfki-medium", shift=1.00),
    "dfki_kalin": dict(engine="piper", model="tr_TR-dfki-medium", shift=0.88),
    "dfki_genc":  dict(engine="piper", model="tr_TR-dfki-medium", shift=1.10),
    "yelda":      dict(engine="say", voice="Yelda", shift=1.00),
}
CORE = [  # A3 cekirdek barklar (v2 csv yoksa)
 ("core_reload","Şarjör!"),("core_grenade","El bombası!"),("core_hit","Yaralandım!"),
 ("core_enemy_down","Düşman düştü!"),("core_cover_me","Bana ört!"),("core_moving","İlerliyorum!"),
 ("core_smoke","Sis atıyorum!"),("core_ack","Emredersiniz komutanım."),
]
TR12 = ["bir","iki","üç","dört","beş","altı","yedi","sekiz","dokuz","on","on bir","on iki"]
for i,w in enumerate(TR12,1): CORE.append((f"core_clock_{i:02d}", f"Saat {w} yönü!"))
for n,w in [(50,"elli"),(100,"yüz"),(200,"iki yüz"),(300,"üç yüz"),(400,"dört yüz")]:
    CORE.append((f"core_dist_{n}", f"{w} metre!"))

def read_rows():
    rows, seen = [], set()
    def add(k, t, role="", durum=""):
        k = k.strip()
        if k and t.strip() and k not in seen:
            seen.add(k); rows.append(dict(id=k, text=t.strip(), role=role, durum=durum))
    for fn in ["telsiz_replikleri.csv", "telsiz_replikleri_v2.csv"]:
        p = os.path.join(ROOT, "Design/Audio", fn)
        if os.path.exists(p):
            with open(p, encoding="utf-8-sig") as f:
                for r in csv.DictReader(f):
                    add(r.get("anahtar") or r.get("id") or "", r.get("metin_tr") or r.get("text") or "",
                        r.get("konusan_rol", ""), r.get("durum", ""))
    old = os.path.join(ROOT, "Assets/_Project/Resources/Audio/Voice/voice_manifest.json")
    if os.path.exists(old):
        for c in json.load(open(old, encoding="utf-8"))["clips"]:
            if c["key"].startswith("player_"): add(c["key"], c["text"], c["role"], c["durum"])
    if not any(r["id"].startswith("core_") for r in rows):
        for k, t in CORE: add(k, t, "Rifleman", "core")
    return rows

def compress_gaps(x, gap):
    if gap is None: return x
    mx, tg = int(gap[0]*SR), int(gap[1]*SR)
    thr = 10 ** (-42/20) * max(1e-4, np.max(np.abs(x)))
    quiet = np.abs(x) < thr
    out, i, n = [], 0, len(x)
    while i < n:
        if quiet[i]:
            j = i
            while j < n and quiet[j]: j += 1
            out.append(x[i:j] if j - i <= mx else x[i:i+tg])
            i = j
        else:
            j = i
            while j < n and not quiet[j]: j += 1
            out.append(x[i:j]); i = j
    return np.concatenate(out) if out else x

def trim(x):
    thr = 10 ** (-45/20) * max(1e-4, np.max(np.abs(x)))
    idx = np.where(np.abs(x) > thr)[0]
    if len(idx) == 0: return x
    a, b = max(0, idx[0]-int(0.02*SR)), min(len(x), idx[-1]+int(0.06*SR))
    return x[a:b]

def pitch_shift(x, f):  # yeniden ornekleme: perde+formant kayar, sure 1/f olur
    if abs(f-1) < 1e-3: return x
    from fractions import Fraction
    fr = Fraction(f).limit_denominator(50)
    return resample_poly(x, fr.denominator, fr.numerator)  # f>1: daha yuksek perde

def lufs_normalize(x, target):
    if len(x) < int(0.5*SR):  # kisa klip: tekrarla olc
        rep = np.tile(np.concatenate([x, np.zeros(int(0.05*SR))]), int(math.ceil(0.5*SR/len(x)))+1)
    else: rep = x
    meter = pyln.Meter(SR)
    try: l = meter.integrated_loudness(rep)
    except Exception: l = -30.0
    if not np.isfinite(l): l = -30.0
    lim = 10 ** (-1.0/20)
    y = x
    for _ in range(4):  # yumusak sinirlayici + yeniden olcum (tepe/ortalama oranı yuksek klipler icin)
        g = 10 ** ((target - l)/20)
        y = lim * np.tanh(x * g / lim)
        rep2 = np.tile(np.concatenate([y, np.zeros(int(0.05*SR))]), int(math.ceil(0.5*SR/len(y)))+1) if len(y) < 0.5*SR else y
        try: l2 = meter.integrated_loudness(rep2)
        except Exception: break
        if abs(l2 - target) < 0.7: break
        l = l - (target - l2)  # kazancı arttır
    pk = np.max(np.abs(y))
    if pk > lim: y = y * (lim/pk)
    # sonuc olcumu
    try: after = meter.integrated_loudness(np.tile(np.concatenate([y, np.zeros(int(0.05*SR))]), int(math.ceil(0.5*SR/len(y)))+1) if len(y) < 0.5*SR else y)
    except Exception: after = target
    return y, float(after)

def write_wav(p, x):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(p, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())

def read_wav(p):
    with wave.open(p, "rb") as w:
        assert w.getframerate() == SR and w.getnchannels() == 1
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float32) / 32768

def main():
    mdir = sys.argv[1]
    limit = int(sys.argv[sys.argv.index("--limit")+1]) if "--limit" in sys.argv else None
    from piper import PiperVoice, SynthesisConfig
    pv = {}
    rows = read_rows()[:limit]
    print(len(rows), "replik")
    manifest = dict(version=2, sampleRate=SR, format="wav16 mono", targetLufs={k: v["lufs"] for k, v in STRESS.items()},
                    voices={}, clips=[])
    tmp = tempfile.mkdtemp()
    total = 0
    for vname, v in VOICES.items():
        manifest["voices"][vname] = dict(engine=v["engine"], source=v.get("model") or ("macOS " + v["voice"]),
                                         shift=v["shift"], license=LICENSES[v.get("model") or "apple"])
        for sname, s in STRESS.items():
            for r in rows:
                if v["engine"] == "piper":
                    if v["model"] not in pv:
                        pv[v["model"]] = PiperVoice.load(os.path.join(mdir, v["model"] + ".onnx"))
                    # perde carpani sure degistirdiginden length_scale ile telafi
                    f = v["shift"] * s["pitch"]
                    cfg = SynthesisConfig(length_scale=s["ls"] * f, noise_scale=s["ns"], noise_w_scale=s["nw"])
                    parts = [np.frombuffer(c.audio_int16_bytes, dtype="<i2") for c in pv[v["model"]].synthesize(r["text"], cfg)]
                    x = np.concatenate(parts).astype(np.float32) / 32768 if parts else np.zeros(100, np.float32)
                    x = pitch_shift(x, f)
                else:
                    t = os.path.join(tmp, "s.wav")
                    subprocess.run(["say", "-v", v["voice"], "-r", str(s["say_rate"]), "--data-format=LEI16@22050",
                                    "-o", t, r["text"]], check=True)
                    x = pitch_shift(read_wav(t), s["pitch"] * v["shift"])
                x = trim(compress_gaps(trim(x), s["gap"]))
                x, lufs = lufs_normalize(x, s["lufs"])
                p = os.path.join(OUT, vname, sname, r["id"] + ".wav")
                write_wav(p, x); total += os.path.getsize(p)
                manifest["clips"].append(dict(id=r["id"], voice=vname, stress=sname,
                    file=f"Audio/Voice/v2/{vname}/{sname}/{r['id']}", role=r["role"], durum=r["durum"],
                    text=r["text"], durationSec=round(len(x)/SR, 3), lufs=round(lufs, 1)))
        print(vname, "tamam", total//1024//1024, "MB", flush=True)
    manifest["totalBytes"] = total
    with open(os.path.join(OUT, "voice_manifest_v2.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=1)
    print("TOPLAM", total/1024/1024, "MB", len(manifest["clips"]), "klip")

LICENSES = {
 "tr_TR-dfki-medium": "CC BY-NC-SA 4.0 (dfki-ot-data) - TICARI KULLANIM YOK; yayin oncesi gercek ses oyuncusuyla degistirilmeli",
 "apple": "Apple TTS - dagitim/lisans kosullari yayin oncesi kontrol edilmeli (yer tutucu)",
}
if __name__ == "__main__": main()
