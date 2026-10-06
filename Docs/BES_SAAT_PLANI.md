# 5 Saatlik Kesintisiz Bitirme Planı (2026-10-06 05:45)

**Hedef:** PUBG lobisi kalitesinde menü + oyun içi görsellerin kendi gözümle doğrulanması. Referans: kullanıcının PUBG lobi ekran görüntüsü (yakın plan karakter, keskin ışık, gerçek sahne; sis/bulanıklık YOK).

## Çalışma döngüsü (her tur ~45-60 dk, 5+ tur)
1. Build → `Tools/UnityVerify/oto_ekran.sh` → PNG'ler
2. **Claude PNG'lere kendisi bakar**, PUBG referansıyla kıyaslar, somut kusur listesi çıkarır
3. Dar kapsamlı düzeltme dalgası (3-6 ajan) + Claude'un kendi elle düzeltmeleri
4. Tekrar build + tekrar bakış. Kusur listesi bitene kadar.

## Tur 1 — Lobi dioraması (PUBG formatı)
- Menü fonu: dev sisli arazi DEĞİL; kameraya 2,5-4 m mesafede KÜÇÜK bir köşe sahnesi:
  gerçek dokulu (ambientCG) taş duvar + kum torbası + sandık/varil (Poly Haven), tek asker ayakta silahlı,
  alacakaranlık ana ışık + arka kenar ışığı, KESKİN görüntü (menüde DoF/sis kapalı ya da çok hafif)
- Havada süzülen asker/ateş/bayrak sorunu kökten çözülür (zemin garantili görünür)
- Ateş: şekilli alev kartları + kıvılcım + ışık; blob yasak
- Kamera: sabit, 1-2 cm'lik nefes hareketi; "her şey dönüyor" hissi bitecek
- Rütbe kartı yazı taşması düzelir
## Tur 2 — Yakın plan karakter
- 2 m'den iyi görünen asker: yüksek çözünürlük kamuflaj + kumaş normal haritası, balaklava+gözlük,
  doğal silah tutuş pozu, kask/teçhizat oturması
## Tur 3 — Oyun içi manzara
- 5 bakış noktasından ekran: arazi, bitki, ufuk, bölge duvarı, gece; kusur listesi → düzeltme
## Tur 4 — Çatışma görselleri
- benchmark sahnesinde ateş/izler/decal/patlama kareleri; silah viewmodel ışığı
## Tur 5 — Ses + hata süpürme + final build
- Player.log sıfır istisna; miks kontrol listesi; sürüm notu

Kural: her turda kullanıcıya 1 ekran görüntüsü seti raporu. Kullanıcı onayı olmadan "bitti" denmez.
