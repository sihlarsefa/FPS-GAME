# U1. Silah Denge Gerekçeleri

Katalog değerleri sabittir; bu belge **neden** bu değerlerin seçildiğini ve gerçek dünyaya dair genel notları açıklar (simülasyon iddiası yok).

## Ortak çerçeve

- **Can:** 100 HP.  
- **Zırh:** %30 / %40 / %55 azaltma (Sv.1–3).  
- **Hedef TTK bandı (gövde, zırhsız, optimal mesafe):**  
  - CQB SMG/pompalı: çok hızlı  
  - AR: 0.25–0.45 sn burst  
  - DMR/SR: düşük RPM, yüksek ceza/ödül  

## SAR 9
- **Gerekçe:** 28×2 HS = 56; yan silah olarak “son çare” yeterince tehditkâr, birincil olamayacak kadar zayıf menzil (falloff 20–70).  
- **Gerçek dünya notu:** Yerli 9 mm tabanca ailesi; hizmet/yan silah rolü.

## Canik TP9
- **Gerekçe:** SAR 9’a göre -2 hasar, +3 mermi → “daha fazla şans, biraz daha yumuşak”. Telsizciye kimlik.  
- **Not:** Sportif/hizmet tabanca hattı; yüksek kapasite hissi.

## SAR 109T
- **Gerekçe:** 22 hasar × 800 RPM → CQB’de AR’yi zorlar; 9 mm falloff 100 m’de %50’ye iner → açık alanda cezalı. Burst 3 taktik seçenek.  
- **Not:** 9 mm PDW/SMG sınıfı; yakın koruma.

## MPT-55
- **Gerekçe:** 26@750, 30 mermi → “kolay kontrol, sürdürülebilir”. Sıhhiye/telsiz’in savaşçı kalmasını sağlar; 7.62 meta’yı ezmez.  
- **Not:** 5.56 milli piyade tüfeği hattı; düşük geri tepme profili.

## MPT-76
- **Gerekçe:** 36@600, 20 mermi → komutan silahı “ağır ama güçlü”. Mag yönetimi beceri.  
- **Not:** 7.62 milli piyade; daha sert tek atış.

## G3A7
- **Gerekçe:** 38@550 — katalogda en sert AR; bombacıya “kapı kırıcı” kimliği. RPM cezası ile MPT-76’dan ayrışır.  
- **Not:** Klasik 7.62 muharebe tüfeği mirası (yerli üretim varyant anlatısı).

## KNT-76
- **Gerekçe:** 52 hasar, 3×, yarı oto — “nişancı ama takip atışı var”. JNG’den hızlı, AR’den kırılgan (10 mermi, hip kötü).  
- **Not:** DMR / nişancı tüfeği rolü.

## JNG-90
- **Gerekçe:** 90 gövde / 225 kafa (×2.5) — zırhsız gövdede neredeyse tek atış eşiği; bolt 1.4 sn “kaçırdıysan öldün”. 6× bilgi avantajı.  
- **Not:** Sürgülü keskin nişancı (Bora-12 anlatısı); uzun menzil.

## PMT-76
- **Gerekçe:** 34@650 × 100 — bastırma kralı; 6 sn reload ve ağırlık 11 “konumsal silah”.  
- **Not:** Tim makineli tüfek; ateş üstünlüğü.

## Escort
- **Gerekçe:** 20×9 pellet, falloff 8–40 min 0.2 — odanın kralı, sokağın palyaçosu.  
- **Not:** 12 ga pompalı; yakın savunma.

## Meta riskleri ve izleme

| Risk | İzleme | Olası ayar (ileride) |
|------|--------|----------------------|
| JNG kamp | HS oranı, ortalama mesafe | Falloff veya bolt süresi |
| PMT koridor | Kill share | Hip spread / move penalty |
| Escort rush | CQB kill% | Pellet sayısı |
| MPT-55 her yerde | Pick rate | MinDamageFactor |

Denge aracı: GÖREV 3 `Tools/BalanceCalc` (bu klasör dışında).
