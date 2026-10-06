# Sesli replikler (Türkçe) - YER TUTUCU

- Kaynak: `Design/Audio/telsiz_replikleri.csv` (225 satır) + 10 oyuncu çağrısı (`player_*`).
- Üretim: macOS `say -v Yelda -r 190` + `afconvert` (WAV, 16-bit, 22.05 kHz, mono) -> `Assets/_Project/Resources/Audio/Voice/<anahtar>.wav`; dizin: `voice_manifest.json`.
- Bu sesler YER TUTUCU TTS'tir (Yelda, macOS); sonradan ses oyuncusuyla değiştirilecek. Yayından önce Apple TTS çıktısının lisans/dağıtım koşulları KONTROL EDİLMELİ.
- Oynatma: `RadioVoicePlayer` (dost: HP 400 Hz + LP 3.5 kHz + hafif bozulma, squelch önce/sonra, rol başına perde 0.85-0.95; oyuncu: filtresiz, perde 0.92). `RadioChatterSystem` olayları bağlar; bomba için `RadioChatterSystem.CalloutGrenade()` çağrılabilir.

## v2 (A4-turkce-ses-uretimi, 2026-10-06)

- Araç: `Tools/VoiceGen/generate_v2.py` (Piper TTS + macOS Yelda; ücretli API ve hesap yok). Piper bir venv içinde kuruldu (`pip install piper-tts scipy pyloudnorm`), sistem geneline dokunulmadı.
- Piper Türkçe sesleri (rhasspy/piper-voices, Hugging Face `tr/tr_TR/`): **yalnızca `dfki` mevcut**. `tr_TR-fahrettin-medium` ve `tr_TR-fettah-medium` depoda YOK (404; dizin listesi sadece `dfki`). Yani istenen 3 ses yerine 1 Piper sesi kullanıldı.
- Lisanslar:

| Ses | Kaynak | Lisans | Ticari kullanım |
|---|---|---|---|
| tr_TR-dfki-medium | marytts/dfki-ot-data, Piper model kartı | **CC BY-NC-SA 4.0** | **HAYIR** (NonCommercial + ShareAlike) |
| macOS Yelda | Apple TTS | Apple lisans koşulları | Belirsiz, yayın öncesi kontrol edilmeli |

- Sonuç: v2 sesleri **yalnızca dahili test/yer tutucu**tur. Ticari yayında dfki çıktısı kullanılamaz (NC) ve türevleri aynı lisansla paylaşılmak zorundadır (SA). Yayın öncesi tüm replikler gerçek Türkçe ses oyuncularıyla yeniden kaydedilmelidir; senaryo için `Docs/SESLENDIRME_SENARYOSU.md`.
- 4 ses kimliği: `dfki_er` (doğal), `dfki_kalin` (formant 0.88, daha kalın), `dfki_genc` (formant 1.10, daha genç), `yelda` (kadın, Apple). Üç dfki varyantı aynı modelin perde kaydırmasıdır; gerçek farklı konuşmacı değildir.
- Stres: sakin (normal hız, -16 LUFS), çatışma (~%16 hızlı, +4% perde, -14.5 LUFS, uzun boşluklar kısaltılır), panik (~%30 hızlı, +9% perde, -13 LUFS, boşluklar 50 ms).
- Normalizasyon: ffmpeg yok; `pyloudnorm` (BS.1770) ile hedef LUFS, yumuşak (tanh) sınırlayıcı, tepe -1 dBFS.
