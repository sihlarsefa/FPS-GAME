# 7. Harekât Alanı (Zone) Fazları

Kaynak: `MatchConfig.DefaultZonePhases`, `ZoneService`.

## 7.1 Kavramlar

| Terim | Anlam |
|-------|--------|
| Mavi bölge | Güvenli harekât alanı dışı hasar veren alan |
| Beyaz çember | Bir sonraki güvenli alan (`NextZone`) |
| Waiting | Bekleme — çember sabit |
| Shrinking | Daralma — merkez/yarıçap doğrusal enterpolasyon |
| Finished | Plan bitti |

Başlangıç yarıçapı ~`mapHalfSize × 1.45` (~742 m); harita yarı boyutu 512 m.

## 7.2 Faz planı (~13 dk daralma)

| Faz | Bekleme (sn) | Daralma (sn) | Hedef yarıçap (m) | Hasar / sn |
|-----|--------------|--------------|-------------------|------------|
| 0 | 150 | 70 | 420 | 1 |
| 1 | 80 | 55 | 260 | 2 |
| 2 | 65 | 45 | 160 | 3.5 |
| 3 | 55 | 40 | 95 | 5 |
| 4 | 45 | 35 | 50 | 8 |
| 5 | 35 | 30 | 20 | 11 |
| 6 | 25 | 30 | 0 | 16 |

Toplam bekleme+daralma ≈ **855 sn** (~14.25 dk) + erken yağma penceresi.

## 7.3 Merkez seçim kuralları

1. Yeni çember, mevcut çemberin **tamamen içinde**.  
2. Merkez tercihen `|x|,|z| ≤ mapHalfSize × 0.8`.  
3. Sonraki çember, bekleme başında seçilir (HUD beyaz halka).  
4. Faz değişiminde `ZoneStageChangedEvent`.

## 7.4 Taktik etkiler

| Evre | Taktik |
|------|--------|
| Faz 0–1 | Geniş rotasyon; FOB/Karakol yağması mümkün |
| Faz 2–3 | Köprü ve dere geçişleri kritik |
| Faz 4–5 | Mevzi + sis; topçu değeri artar |
| Faz 6 | Merkez çöküşü; açık alanda kalmak ölümcül |

Ses uyarıları: `ZoneWarning`, `ZoneDamage` — ayrıntı [19-ses-tasarim.md](19-ses-tasarim.md).
