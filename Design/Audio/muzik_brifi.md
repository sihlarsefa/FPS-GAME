# Müzik Brifi — Besteci

Oyun: **HAREKÂT** — Türk askeri FPP tim battle royale (kurgu tatbikatı; Mavi/Kırmızı kuvvet).  
Ses kimliği: `SoundId.MenuMusic` (+ ileride MatchStinger / Victory stem ayrımı önerilir).  
Referans duygu: disiplinli, Anadolu coğrafyası, abartısız kahramanlık — Hollywood trailer şişkinliği yok.

---

## 1. Genel yön

| Madde | Yön |
|-------|-----|
| Tonalite | Minör / Dorian ağırlıklı; majör zaferde kısa parıltı |
| Tempo | Menü 72–84 BPM; maç başı 88–100; zafer 96–110 sonra yavaşlama |
| Enstrüman esintisi | Bağlama (veya saz benzeri plucked), ney/kaval flüt çizgisi, davul/def hafif, modern pad + düşük brass |
| Yasak | Resmi marş alıntısı, telifli melodi, aşırı orientalize klişe “yılan dansı” motifleri |
| Dil | Vokal yok veya kelimesiz koro (sözsüz); oyun içi VO ile çakışmasın |
| Dinamik | Stem’li (müzik / ritim / lead) — duck için Music bus öncelik 2 |

Anadolu enstrümanları **esinti**: otantik kayıt veya örnek kütüphane; “etnik turizm” pastişi değil, askeri gerilim + toprak hissi.

---

## 2. Ana menü

| Alan | Spec |
|------|------|
| Süre | 2:00–2:40 loop (dikişsiz) |
| Yapı | Intro 8 sn (ney solo) → beden (bağlama + pad) → köprü → bedene dönüş |
| Enerji | Orta-düşük; buton UI sesleri üstte kalsın |
| Varyant | İsteğe bağlı “gece harekâtı” koyu mix (−2 semiton pad) |
| Teslim | Stereo WAV 48 kHz 24-bit + stem’ler (pad / rytm / lead) |

**Duygu cümlesi:** “Dağ sırtında bekleyen tim; henüz ateş yok.”

---

## 3. Maç başı (iniş / intikal)

| Alan | Spec |
|------|------|
| Süre | 0:45–1:15 one-shot veya kısa loop; helikopter/Kirpi SFX önde |
| Yapul | Stinger 3–5 sn + yükselen davul; menü müziğinden motif türevi |
| Duck | HelicopterRotor / VehicleEngine −4…−8 dB müzik |
| Bitiş | Paraşüt/iniş tamam → fade 2 sn; maç içi müzik **yok** (ambiyans + DistantBattle) |

**Duygu cümlesi:** “Kapı açıldı; vadinin soğuk havası.”

---

## 4. Zafer

| Alan | Spec |
|------|------|
| Süre | 0:50–1:20 |
| Yapı | Kısa sessizlik 0.5 sn → bağlama motif + majör dönüş → brass kısa → ney kapanış |
| UI | EndScreen skorlarla birlikte; müzik −6 dB metin okunurluğu için opsiyonel |
| Yenilgi (opsiyonel) | Aynı tema minör, tempo −10 BPM, lead yok — ayrı dosya önerilir |

**Duygu cümlesi:** “Tim ayakta; abartısız saygı.”

---

## 5. Teknik / entegrasyon

- Unity: `MenuMusic` loop; maç başı/zafer şimdilik aynı enum veya one-shot overlay (implementasyon Cursor).  
- Peak −1 dBTP; LUFS menü ≈ −16 LUFS, stinger ≈ −14, zafer ≈ −15.  
- Mobil/kulaklık: mid sıkışıklığına dikkat; 200–400 Hz çamur yok.  
- Lisans: orijinal besteci işi; sample kütüphane ticari/Steam uyumlu.

---

## 6. Referans dinleme (ruh, kopya değil)

- Askeri gerilim: düşük davul + ambient (Modern Warfare menü ruhu, melodi kopyalanmaz).  
- Anadolu dokunuş: bağlama/ney dokusu çağdaş prodüksiyonla.  
- Battle royale: maç içi sessizlik disiplini (PUBG lobisine benzer “maçta müzik yok”).

## 7. Teslim kontrol listesi

- [ ] Menü loop dikişsiz  
- [ ] Stem’ler hizalı  
- [ ] Stinger + zafer WAV  
- [ ] Metadata: BPM, ton, süre  
- [ ] Ticari kullanım onayı yazılı  
