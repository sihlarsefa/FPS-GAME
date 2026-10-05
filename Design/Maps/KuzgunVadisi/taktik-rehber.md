# Kuzgun Vadisi — Taktik Rehber

Kaynak yerleşim: `MapLayout.CreateKuzgunVadisi`. İntikal sektörleri tasarım katmanıdır (S1–S6).

## Genel arazi

- Ortada **Kuzgun Deresi** kuzey→güney akar; ana yol batı kıyısından iner, köprüyle doğuya geçer.
- Kenarlarda dağ sırtları doğal sınır; orta vadi hareket koridoru.
- Erken ganimet: karakol, gözetleme, röle. Geç oyun kalesi: **İleri Üs (H6)**.

## İntikal sektörlerine göre önerilen rotalar

| Sektör | İniş | İlk 90 sn hedef | Alternatif |
|--------|------|-----------------|------------|
| S1 Kuzey Boğazı | Kirpi | Ana yol → Karakol yolu / Kuzey patika | Röle (yavaş) |
| S2 KD Sırt | T-70 | Sınır Karakolu (G2) | Yamaç Köyü |
| S3 Doğu Sırtı | T-70 | Doğu Gözetleme → FOB | Çam Sırtı örtü |
| S4 Güney Boğazı | Kirpi | Baraj / Yıkık Köy | Ağıl → Ocak |
| S5 Batı Dağ | T-70 | Batı Gözetleme / Röle | Kuzgun Köyü |
| S6 FOB Helipad | T-70 | Üs içi grev veya doğu yol çıkışı | Yamaç Köyü |

## Bölge planları (özet)

### Kuzgun Köyü (D5) — Village, Medium
- **Saldırı:** Doğu köy yolundan L şeklinde; bir tim dere güneyinden örtülü.
- **Savunma:** İki kat + cami hattı; batı tarla mayın gibi açık — KN buraya bakmasın, doğuya baksın.
- **Kaçış:** Yamaç yolu köprüsü veya ana yol güney.

### Yamaç Köyü (H4) — Village, Medium
- **Saldırı:** Köprü (batı) + doğu yolu kıskaç.
- **Savunma:** Doğu sırt KN; FOB trafiğini erken duy.
- **Kaçış:** Orman yolu → Çam Sırtı.

### Sınır Karakolu (G2) — Karakol, High
- **Saldırı:** S2 hava baskını veya karakol yolu köprüsünden duman + kama.
- **Savunma:** Kule + duvar; kuzey açık — izci şart.
- **Kaçış:** KD sırt veya ana yola dönüş.

### İleri Üs (H6) — ForwardBase, Military
- **Saldırı:** Üs yolu + doğu yolu eşzamanlı; helipad açık — flanş.
- **Savunma:** Hesco halkası; iç depo son hat.
- **Kaçış:** Doğu patika veya ana yol güney.

### Taş Ocağı (C7) — Quarry, Medium
- **Saldırı:** Ocak yolu boğazında pusu kır, teraslara tırman.
- **Savunma:** Üst teras KN; tek araç girişi kontrol.
- **Kaçış:** Ağıl yolu veya batı sırt.

### Kuzgun Barajı (F9) — Dam, Medium
- **Saldırı:** Doğu ayaktan kontrol binası; gölet kuzeyi açık alan — koşma.
- **Savunma:** Baraj tepesi flanş; iki ayak.
- **Kaçış:** Ana yol güney boğaz (S4).

### Röle Tepesi (B3) — RelayHill, High
- **Saldırı:** Kıvrımlı yol yavaş — S5 veya sırt yürüyüşü.
- **Savunma:** Bunker + anten çevresi; 360° görüş.
- **Kaçış:** Batı patika / kuzey sırt.

### Çam Sırtı (I2) — Forest, Low
- **Saldırı:** Örtülü sızma; ganimet ikincil.
- **Savunma:** Ağaç hattı pusu; kulübe çevresi açık (ClearRadius).
- **Kaçış:** Yamaç / doğu yamaç.

### Ağıl (D9) — Farm, Low
- **Saldırı:** Ahır içi yakın mesafe.
- **Savunma:** Kısa süreli; geçiş noktası.
- **Kaçış:** Güney patika gözetleme.

### Yıkık Köy (G8) — Ruins, Medium
- **Saldırı:** Bodrum temizliği yavaş; KN yıkıntıdan destek.
- **Savunma:** Enkaz delikleri; baraja yakın baskı.
- **Kaçış:** Harabe yolu → ana yol.

### Gözetleme noktaları (D2, I5, B5, D9)
- Hızlı High ganimet; küçük yarıçap.
- Tutma süresi kısa; bilgi (görüş) asıl değer.
- Her biri ilgili intikal sektörüne (S2/S3/S5/S4) bağlanır.

## Tim rolleri — harita ipuçları

| Rol | Öncelik |
|-----|---------|
| Tim Komutanı | Sektör seçimi, köprü zamanlaması |
| Keskin Nişancı | Röle, karakol kulesi, baraj ayakları, FOB kule |
| Makineli | Köprü ve ana yol boğazları |
| Sıhhiyeci | Köy içi / üs içi toplanma |
| Telsizci | Sektör ve grid çağrıları (D5, H6…) |
| Bombacı | Bunker, ahır, bodrum, Hesco kapısı |
| Piyade | Kanat ve temizleme |

## Tipik maç akışı

1. **0–3 dk:** Gözetleme / karakol / röle yağması veya köy güvenli loot.
2. **3–8 dk:** Dere hattı ve köprü kontrolü; FOB’a sızma veya kuşatma.
3. **Geç:** Bölge güneye/merkeze sıkışırsa baraj–yıkık–üs üçgeni sıcak olur.
