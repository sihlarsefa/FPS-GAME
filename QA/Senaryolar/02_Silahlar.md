# Silahlar

Senaryo sayısı: **100** · Öncelik: P1=100

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## WPN-001 — SAR 9: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 15 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-002 — SAR 9: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-003 — SAR 9: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 1.5 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-004 — SAR 9: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-005 — SAR 9: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-006 — SAR 9: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.16 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-007 — SAR 9: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-008 — SAR 9: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.15×; bırakınca temel görüş açısına döner.

---

## WPN-009 — SAR 9: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 28.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-010 — SAR 9: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 70.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.55; tek bileşen hasarı 15.4; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-011 — Canik TP9: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 18 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-012 — Canik TP9: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-013 — Canik TP9: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 1.6 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-014 — Canik TP9: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-015 — Canik TP9: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-016 — Canik TP9: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.15 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-017 — Canik TP9: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-018 — Canik TP9: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.15×; bırakınca temel görüş açısına döner.

---

## WPN-019 — Canik TP9: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 26.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-020 — Canik TP9: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Canik TP9; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 70.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.55; tek bileşen hasarı 14.3; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-021 — SAR 109T: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 30 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-022 — SAR 109T: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-023 — SAR 109T: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 2.0 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-024 — SAR 109T: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-025 — SAR 109T: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-026 — SAR 109T: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.075 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-027 — SAR 109T: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single, Burst, Auto; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-028 — SAR 109T: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.2×; bırakınca temel görüş açısına döner.

---

## WPN-029 — SAR 109T: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 22.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-030 — SAR 109T: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; SAR 109T; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 100.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.5; tek bileşen hasarı 11.0; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-031 — MPT-55: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 30 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-032 — MPT-55: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-033 — MPT-55: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 2.3 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-034 — MPT-55: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-035 — MPT-55: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-036 — MPT-55: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.08 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-037 — MPT-55: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single, Burst, Auto; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-038 — MPT-55: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.35×; bırakınca temel görüş açısına döner.

---

## WPN-039 — MPT-55: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 26.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-040 — MPT-55: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-55; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 300.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.65; tek bileşen hasarı 16.9; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-041 — MPT-76: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 20 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-042 — MPT-76: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-043 — MPT-76: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 2.5 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-044 — MPT-76: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-045 — MPT-76: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-046 — MPT-76: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.1 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-047 — MPT-76: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single, Auto; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-048 — MPT-76: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.35×; bırakınca temel görüş açısına döner.

---

## WPN-049 — MPT-76: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 36.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-050 — MPT-76: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; MPT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 400.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.7; tek bileşen hasarı 25.2; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-051 — G3A7: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 20 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-052 — G3A7: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-053 — G3A7: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 2.6 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-054 — G3A7: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-055 — G3A7: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-056 — G3A7: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.10909090909090909 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-057 — G3A7: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single, Auto; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-058 — G3A7: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.35×; bırakınca temel görüş açısına döner.

---

## WPN-059 — G3A7: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 38.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-060 — G3A7: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; G3A7; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 400.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.7; tek bileşen hasarı 26.6; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-061 — KNT-76: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 10 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-062 — KNT-76: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-063 — KNT-76: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 2.8 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-064 — KNT-76: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-065 — KNT-76: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-066 — KNT-76: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.28 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-067 — KNT-76: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-068 — KNT-76: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü görünür; katalog büyütmesi 3.0×; bırakınca temel görüş açısına döner.

---

## WPN-069 — KNT-76: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 52.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-070 — KNT-76: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; KNT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 600.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.75; tek bileşen hasarı 39.0; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-071 — JNG-90: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 5 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-072 — JNG-90: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-073 — JNG-90: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 3.4 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-074 — JNG-90: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-075 — JNG-90: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-076 — JNG-90: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 1.4 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-077 — JNG-90: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-078 — JNG-90: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü görünür; katalog büyütmesi 6.0×; bırakınca temel görüş açısına döner.

---

## WPN-079 — JNG-90: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 90.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-080 — JNG-90: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; JNG-90; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 800.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.8; tek bileşen hasarı 72.0; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-081 — PMT-76: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 100 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 1 olsa da maliyet bir fişektir.

---

## WPN-082 — PMT-76: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-083 — PMT-76: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 6.0 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-084 — PMT-76: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-085 — PMT-76: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-086 — PMT-76: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.09230769230769231 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-087 — PMT-76: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Auto; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-088 — PMT-76: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.3×; bırakınca temel görüş açısına döner.

---

## WPN-089 — PMT-76: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 34.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-090 — PMT-76: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; PMT-76; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 400.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.7; tek bileşen hasarı 23.8; kaçan atışlar hasar ölçümüne katılmaz.

---

## WPN-091 — Escort: şarjör kapasitesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Silahı kuşan
2. HUD şarjör sayısını kaydet
3. Bir atış yap

### Beklenen sonuç

Başlangıç 7 mermi; her tetiklenen atışta bir mermi azalır, saçma sayısı 9 olsa da maliyet bir fişektir.

---

## WPN-092 — Escort: boş tetik

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörü tüket
2. Son mermiden sonra tetiği üç kez çek

### Beklenen sonuç

0 altında mühimmat oluşmaz; boş tetik hasar vermez.

---

## WPN-093 — Escort: yeniden doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Şarjörden 3 mermi harca
2. R bas
3. Tamamlanma süresini ölç

### Beklenen sonuç

Katalog süresi 4.2 sn (görüntü toleransı 0,15 sn); şarjör kapasiteye döner, yedekten tam 3 alınır.

---

## WPN-094 — Escort: yedeksiz doldurma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Yedek mühimmatı sıfırla
2. Şarjörden 3 harca
3. R bas

### Beklenen sonuç

Yeniden doldurma başlamaz; mevcut şarjör mermisi kaybolmaz.

---

## WPN-095 — Escort: doldurma iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. 3 mermi harca
2. R bas
3. Süre bitmeden başka silaha geç
4. Geri dön

### Beklenen sonuç

İptal edilen doldurma bedelsiz mermi eklemez; HUD takılı ilerleme göstermez.

---

## WPN-096 — Escort: atış aralığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Uygun modda 5 atışı video/zaman kaydına al
2. Ardışık atış aralığını ölç

### Beklenen sonuç

Ateş aralığı 0.85 sn altına inmez (bir simülasyon tick toleransı); sabit makro kullanılmaz, gözlenen gecikme ayrı raporlanır.

---

## WPN-097 — Escort: atış modu döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. B tuşuna tüm seçenekler dönene kadar bas
2. Her modda tetiği basılı tut

### Beklenen sonuç

Sıra katalogla uyumlu: Single; Single tek basışta bir atış, Auto basılı tutmada devam, yalnız listelenen modlar seçilebilir.

---

## WPN-098 — Escort: ADS ve büyütme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. RMB ile nişan al
2. Görüş açısı ve örtüyü gözle
3. Bırak

### Beklenen sonuç

Dürbün örtüsü açılmaz; standart ADS görünür; katalog büyütmesi 1.15×; bırakınca temel görüş açısına döner.

---

## WPN-099 — Escort: hasar bileşeni

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Hedefi FalloffStart öncesine yerleştir
2. Gövdeye tek mermi veya kontrollü tek saçma isabet ettir
3. Can farkını kaydet

### Beklenen sonuç

Zırhsız gövde bileşen hasarı 20.0; pompalıda her saçma ayrı hasar bileşenidir, tüm saçmalar toplamı can tavanına kırpılır.

---

## WPN-100 — Escort: menzil düşüşü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Atış Poligonu test düzeneği; Escort; dolu şarjör ve uygun yedek mühimmat; sabit zırhsız 100 can hedef; test düzenek erişimi yoksa Engelli.

### Adımlar

1. Aynı zırhsız hedefi 40.0 m mesafeye koy
2. Tek mermi/saçma gövde hasarını kaydet

### Beklenen sonuç

Menzil sonu çarpanı 0.2; tek bileşen hasarı 4.0; kaçan atışlar hasar ölçümüne katılmaz.

---
