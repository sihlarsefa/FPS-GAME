#!/usr/bin/env python3
"""v2 CSV (Design/Audio/telsiz_replikleri_v2.csv) -> Ogg Vorbis (q~0.4, mono 22.05k). Yalnizca eksik id'ler; manifesti ekler.
Kullanim: <venv>/bin/python generate_v2_ogg.py <piper_model_dir>   (venv: piper-tts scipy pyloudnorm soundfile)"""
import csv, json, os, sys
import numpy as np
import soundfile as sf
sys.path.insert(0, os.path.dirname(__file__))
import generate_v2 as g

VOICES = ["dfki_er", "dfki_kalin", "dfki_genc"]
def write_ogg(p, x):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with sf.SoundFile(p, "w", samplerate=g.SR, channels=1, format="OGG", subtype="VORBIS") as f:
        try: f.compression_level = 0.6  # libsndfile: 1.0=en dusuk kalite; 0.6 ~ Vorbis q4
        except Exception: pass
        f.write(np.clip(x, -1, 1).astype("float32"))

def main():
    from piper import PiperVoice, SynthesisConfig
    pv = PiperVoice.load(os.path.join(sys.argv[1], "tr_TR-dfki-medium.onnx"))
    mp = os.path.join(g.OUT, "voice_manifest_v2.json")
    man = json.load(open(mp, encoding="utf-8"))
    done = {(c["id"], c["voice"], c["stress"]) for c in man["clips"]}
    rows = list(csv.DictReader(open(os.path.join(g.ROOT, "Design/Audio/telsiz_replikleri_v2.csv"), encoding="utf-8-sig")))
    n = 0; total = 0
    for vname in VOICES:
        shift = g.VOICES[vname]["shift"]
        for sname, s in g.STRESS.items():
            for r in rows:
                rid, text = r["id"].strip(), r["text"].strip()
                if not rid or not text or (rid, vname, sname) in done: continue
                base = os.path.join(g.OUT, vname, sname, rid)
                if os.path.exists(base + ".wav"): continue
                f = shift * s["pitch"]
                cfg = SynthesisConfig(length_scale=s["ls"] * f, noise_scale=s["ns"], noise_w_scale=s["nw"])
                parts = [np.frombuffer(c.audio_int16_bytes, dtype="<i2") for c in pv.synthesize(text, cfg)]
                x = np.concatenate(parts).astype(np.float32) / 32768 if parts else np.zeros(100, np.float32)
                x = g.pitch_shift(x, f)
                x = g.trim(g.compress_gaps(g.trim(x), s["gap"]))
                x, lufs = g.lufs_normalize(x, s["lufs"])
                if len(x) < 64: x = np.concatenate([x, np.zeros(64 - len(x), np.float32)])
                try: write_ogg(base + ".ogg", x)
                except Exception as e: print("HATA", vname, sname, rid, len(x), e, flush=True); continue
                total += os.path.getsize(base + ".ogg"); n += 1
                man["clips"].append(dict(id=rid, voice=vname, stress=sname, file=f"Audio/Voice/v2/{vname}/{sname}/{rid}",
                    role=r.get("voiceHint", ""), durum=r.get("category", ""), text=text,
                    durationSec=round(len(x)/g.SR, 3), lufs=round(lufs, 1), codec="ogg"))
                if n % 300 == 0:
                    print(n, total//1024//1024, "MB", flush=True)
                    json.dump(man, open(mp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    man["formatOgg"] = "ogg vorbis mono 22050 (v2 CSV klipleri); eski klipler wav16"
    man["totalBytes"] = man.get("totalBytes", 0) + total
    json.dump(man, open(mp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("BITTI", n, "klip", total/1024/1024, "MB")
if __name__ == "__main__": main()
