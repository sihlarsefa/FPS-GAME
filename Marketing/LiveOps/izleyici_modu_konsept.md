# HAREKÂT — İzleyici / Yayıncı Arayüzü Konsepti

**Tür:** Yalnızca doküman (üretim kapsamı dışı; C2-8 uzatma).  
**Amaç:** Cast ekibi ve izleyiciler için fikir seti; Unity HUD ajanlarına referans.

---

## 1. İlkeler

- Spoiler kontrolü: resmi yayın **gecikmeli**.  
- Bilgi yoğun ama panik yok: askeri brifing estetiği, `#E30A17` vurgu.  
- Oyuncu HUD’undan ayrı katman; rekabet adil kalsın (izleyici bilgisi oyuncuya sızmaz).  
- Kurgu dili: Mavi / Kırmızı; gerçek örgüt yok.

## 2. İzleyici modu — ekran bölgeleri (1080p)

```
┌──────────────────────────────────────────────────────────┐
|  ÜST BAR: maç ID · süre · zone fazı · sunucu bölgesi     |
├───────────────┬──────────────────────────┬───────────────┤
| TİM LİSTESİ   |     ANA GÖRÜNTÜ          | OLAY AKISI    |
| (sıra, sağ  |     (oyuncu / serbest    | (öldürme,     |
|  oyuncu,    |      kamera / harita)    |  topçu, emir) |
|  hayatta)   |                          |               |
├───────────────┴──────────────────────────┴───────────────┤
| ALT: seçili tim komuta zinciri · intikal ikonu · sponsor |
└──────────────────────────────────────────────────────────┘
```

## 3. Kamera ve takip

| Mod | Açıklama |
|-----|----------|
| Oyuncu omuz / FPP aynası | Seçili askeri takip (izleyiciye özel; oyuncu görmez) |
| Serbest uçuş | POI / zone kenarı |
| Tim takımı | Ortalama tim merkezi |
| Öldürme anı | 5 sn replay buffer (cast operatörü) |
| Harita kuşbakışı | Kuzgun Vadisi SVG / in-game map |

Kısayol fikirleri: `1–0` tim seç, `[` `]` oyuncu, `M` harita, `R` replay.

## 4. Bilgi panelleri

- **Tim kartı:** 10 slot, can/zırh çubuğu, rol ikonu, komutan yıldızı.  
- **Komuta zinciri:** Emir devri olduğunda üst banner (3 sn).  
- **Zone / topçu:** Daralma timer; topçu tehdit halkası (izleyiciye).  
- **Ekonomi yok:** BR yağma özeti opsiyonel; güç spoiler’ı sınırlı tut.

## 5. Gecikme ve anti-spoiler

| Katman | Gecikme |
|--------|---------|
| Genel izleyici | 3–5 dk |
| Final | 5–10 dk |
| Takım coach sesi | Ayrı kanal; yayın karışımı yok |
| Bekleme odası | Canlı (maç dışı) |

Cast “production” görünümü gecikmesiz olabilir; dış VOD gecikmeli.

## 6. Erişilebilirlik / yayın

- Renk körlüğü: Mavi/Kırmızı’ya ek desen / ikon.  
- Yazı boyutu cast overlay için %125 seçeneği.  
- TR/EN alt yazı şeridi (duyuru satırı).

## 7. Üretim notları (sonraki faz)

- Web overlay (OBS tarayıcı kaynağı) ile başlanabilir.  
- Oyun içi spectator Unity’de ayrı kamera rig.  
- Veri: maç event stream (Backend) → overlay JSON.

Bu dosya implementasyon zorunluluğu getirmez.
