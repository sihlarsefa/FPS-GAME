# 11. Ses ve Görsel Yönelim (Özet)

Ayrıntılı ses: [19-ses-tasarim.md](19-ses-tasarim.md).  
Teknik: prosedürel `GameAudio` / `GameVfx` (dış asset yok).

## 11.1 Görsel dil

| Öğe | Yönelim |
|-----|---------|
| Siluet | Low-poly askeri; dijital kamuflaj |
| Palet | Zeytin / toprak / beton; vurgu kırmızısı (#E30A17) UI’da |
| Okunurluk | Mavi/kırmızı kol bandı (kuvvet); dost-düşman siluet |
| Zone | Mavi duvar + beyaz sonraki çember |
| VFX | Namlu flaşı, iz mermisi, kan, patlama, sis, toz |

## 11.2 Kamera

- FPP dünya + viewmodel kameraları.  
- ADS zoom silah `AdsZoom` değerine bağlı (DMR 3×, SR 6×).  
- FOV ayarı ayarlardan.

## 11.3 Ses katmanları (özet öncelik)

1. Kritik UI (isabet, ölüm, zone)  
2. Kendi silahın  
3. Yakın düşman ateşi / ıslık  
4. Ayak sesi / araç  
5. Ambiyans / uzak muharebe  

## 11.4 Erişilebilirlik

- Renk körlüğü: kuvvetler şekil+renk.  
- Ses stereosu yön için; altyazılı komuta bildirimleri (UI).  
- Titreşim opsiyonel (gelecek).
