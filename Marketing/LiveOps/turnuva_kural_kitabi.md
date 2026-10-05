# HAREKÂT — Resmi Turnuva Kural Kitabı

**Sürüm:** 1.0 (Sezon 1 taslağı)  
**Format:** FPP Tim Battle Royale · 10 kişilik tim  
**Kurgu:** Mavi / Kırmızı tatbikat — gerçek örgüt adı yok  
**Dil:** Türkçe (resmi) · İngilizce özet bölüm sonunda

---

## 1. Amaç

Adil, tekrarlanabilir ve izlenebilir resmi maçlar düzenlemek. Turnuvalar **kozmetik ödül** ve sıralama içindir; oyun içi güç satın alınamaz.

## 2. Katılım koşulları

| Kural | Açıklama |
|-------|----------|
| Platform | Windows istemci (Steam / resmi launcher) |
| Hesap | Doğrulanmış HAREKÂT hesabı; tek hesap / oyuncu |
| Yaş | Bölgesel Steam + yasal yaş sınırı |
| Tim boyutu | Tam **10** oyuncu (yedek: en fazla 2, maç başı 10 sahada) |
| İsim | Küfür, nefret, gerçek örgüt adı yasak |
| Check-in | Maçtan **30 dk** önce lobby check-in zorunlu |

**Yedek kullanımı:** Maç başlamadan önce Komutanlık (turnuva admin) onayı. Maç başladıktan sonra yedek girişi yalnızca bağlantı kopması protokolüyle (bkz. §7).

## 3. Maç formatı

### 3.1 Lobby

- Harita: Sezon 1 varsayılanı **Kuzgun Vadisi** (etkinlik haftalarında varyant duyurulur).
- Lobide kuvvet rengi (Mavi/Kırmızı) kozmetik / takım kimliği; oyun dengesi aynıdır.
- İntikal: turnuva ayarına göre T-70 veya Kirpi (haftalık duyuru).
- Özel lobi şifresi ve sunucu bölgesi Komutanlık tarafından verilir.

### 3.2 Maç akışı

1. Check-in → rol / teçhizat kilidi (turnuva preset).  
2. Brifing 2 dk (sessiz / komuta kanalı açık).  
3. İntikal → tatbikat.  
4. Son tim ayakta veya süre sonu (60 dk tavan, duyurulursa değişir).  
5. Sonuç ekranı + demolar / VOD arşivi (varsa).

### 3.3 Yasak / izin

| İzinli | Yasak |
|--------|--------|
| Oyun içi komuta emirleri, telsiz | Üçüncü parti aim / ESP / macro |
| Resmi ses (Discord Tim odası) | Overlay cheat, paket manipülasyonu |
| Yayın (gecikmeli, §8) | Hesap paylaşma, boost satışı |
| Kozmetik skin | Güç veren mod, config exploit |

## 4. Puanlama

Her maç sonunda tim puanı:

```
TimPuan = YerlesimPuani + (TakimOldurme × OldurmeCarpani)
```

### 4.1 Yerleşim puanı (örnek: 20 tim lobby)

| Sıra | Puan |
|------|------|
| 1 | 20 |
| 2 | 16 |
| 3 | 14 |
| 4 | 12 |
| 5 | 10 |
| 6 | 8 |
| 7 | 6 |
| 8 | 4 |
| 9–12 | 2 |
| 13–20 | 1 |

*Tim sayısı farklıysa tablo orantılı ölçeklenir; sezon başında sabitlenir.*

### 4.2 Öldürme

| Parametre | Değer |
|-----------|--------|
| Öldürme çarpanı | **1** puan / tim öldürmesi |
| Takım öldürme tavanı (maç başı) | **15** (üstü sayılmaz — farm önleme) |
| Dost ateşi | Puan vermez; tekrarlayan DF uyarı → ceza |

### 4.3 Seri / turnuva toplamı

- **Grup aşaması:** N maç toplam puan.  
- **Playoff:** Eleme veya üstünlük serisi (BO3 / BO5 duyuru).  
- Beraberlikte sırayla: (1) toplam yerleşim, (2) toplam öldürme, (3) son maç yerleşimi, (4) seed / kura.

## 5. Cezalar

| İhlal | İlk | Tekrar |
|-------|-----|--------|
| Geç check-in | −2 puan | Maç forfeit |
| Yasaklı isim / toxic chat | Uyarı | Maç DQ / turnuva DQ |
| Rol dışı exploit (bilinçsiz) | Maç sonucu iptal | Turnuva DQ |
| Hile (kanıtlı) | Anında DQ + hesap inceleme | Kalıcı turnuva yasağı |
| Smurf / çoklu hesap | DQ | Hesap cezası |
| Yayın spoiler (finalde) | Uyarı | VOD hakkı kısıtı |

Cezalar `#turnuva-sonuc` ve resmi tabloya işlenir.

## 6. Hile ve denetim

1. **Sunucu otoritesi** + anti-cheat telemetrisi (şüphe skoru).  
2. Maç sonrası **demo / replay** talebi (48 saat saklama hedefi).  
3. Şüpheli istatistik (anormal isabet, hız) manuel inceleme.  
4. Oyuncu raporları: Discord `#turnuva-rapor` veya destek formu; **kişisel veri yok**.  
5. Kanıt standardı: klip + zaman damgası + maç ID + oyuncu adı.

**Diskalifiye kararı** Komutanlık + en az bir bağımsız moderatör onayıyla alınır.

## 7. Bağlantı kopması protokolü

| Durum | Kural |
|-------|--------|
| İntikal öncesi kopma | 5 dk yeniden bağlan; yoksa yedek (onaylı) |
| Maç içi kopma | Yeniden bağlanmaya izin; pause yok (BR doğası) |
| Sunucu çökmesi | Maç iptal / yeniden başlat (Komutanlık kararı) |
| 3+ tim aynı anda ağ sorunu | Lobby yeniden |

Kasıtlı AFK / sabotaj: ceza tablosu.

## 8. Yayın ve VOD

- Resmi yayın gecikmesi: **en az 3 dk** (finalde 5–10 dk önerilir).  
- Takım yayınları: rakip pozisyon spoileri yasak (turnuva chat’te).  
- İzleyici modu konsepti: `izleyici_modu_konsept.md`.

## 9. İtiraz süreci

1. **Başvuru süresi:** Maç bitişinden itibaren **60 dakika**.  
2. **Kanal:** Discord `#turnuva-itiraz` veya e-posta (duyurulan adres).  
3. **İçerik:** Maç ID, iddia, kanıt linki, etkilenen puan.  
4. **İnceleme:** 24–48 saat içinde ön karar.  
5. **Kesin karar:** Sezon Komutanlığı; yazılı gerekçe.  
6. **Tekrar itiraz:** Yalnız yeni kanıt ile, 1 kez.

İtiraz sırasında puanlar “geçici” işaretlenebilir; tablo kilitlenene kadar ödül dağıtılmaz.

## 10. Ödüller

Turnuva ödülleri **yalnızca kozmetik** (bkz. `sezon_odulleri.md`). Nakit ödül varsa ayrı sponsor sözleşmesiyle duyurulur; oyun dengesi etkilenmez.

## 11. Fair play yemini (kısa)

> Hile kullanmayacağım, hesap ticareti yapmayacağım, gerçek örgüt adı kullanmayacağım, rakiplere saygı göstereceğim.

Check-in sırasında onay kutusu / yazılı kabul.

---

## English summary

- **Format:** 10-player FPP squad BR.  
- **Score:** Placement table + team kills (cap 15/match, ×1).  
- **Cheats:** Instant DQ; appeals within 60 minutes with evidence.  
- **Rewards:** Cosmetics only — no pay-to-win.  
- **Fiction:** Blue vs Red exercise; no real organization names.
