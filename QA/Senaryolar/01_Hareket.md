# Hareket

Senaryo sayısı: **25** · Öncelik: P1=25

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## MOV-001 — Yürüme hızı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. W ile 5 saniye yürü
2. Başlangıç/bitiş koordinatını kaydet

### Beklenen sonuç

Engelsiz düz hatta yaklaşık 23 m; nominal hız 4,6 m/sn; ölçüm toleransı yüzde 5.

---

## MOV-002 — İleri sprint

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. W+Shift ile 5 saniye ilerle
2. Bırakıp yürümeye geç

### Beklenen sonuç

Sprint yaklaşık 36 m; bırakınca yürüyüşe döner; hız takılı kalmaz.

---

## MOV-003 — Geri sprint engeli

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. S+Shift ile 5 saniye geri yürü
2. İleri sprint mesafesiyle karşılaştır

### Beklenen sonuç

Geri hareket ileri sprintin 7,2 m/sn hızına çıkmaz.

---

## MOV-004 — Çapraz hız

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. W ile 5 saniye ölç
2. Aynı zeminde W+D ile ölç

### Beklenen sonuç

Çapraz toplam hız düz yürüyüşten yüzde 5 fazla olmaz; hız vektörü normalize edilir.

---

## MOV-005 — Çömelme anahtarı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. C bas bırak
2. 5 saniye yürü
3. C ile ayağa kalk

### Beklenen sonuç

Çömelme kalıcıdır; nominal hız 2,4 m/sn; ikinci basış ayakta duruşa döner.

---

## MOV-006 — Çömelme basılı tutma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Ctrl basılı yürüyüp bırak
2. Kamera yüksekliğini izle

### Beklenen sonuç

Ctrl bırakılınca yeterli tavan boşluğunda ayağa kalkılır.

---

## MOV-007 — Yüzüstü hareket

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Z ile yat
2. 5 saniye yürü
3. Z ile kalk

### Beklenen sonuç

Nominal hız 1,1 m/sn; yüzüstü kamera zeminin içine girmez.

---

## MOV-008 — Çömelirken zıplama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. C ile çömel
2. Space bas

### Beklenen sonuç

Çömelme sırasında zıplama başlamaz.

---

## MOV-009 — Yatarken zıplama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Z ile yat
2. Space bas

### Beklenen sonuç

Yüzüstü duruşta zıplama başlamaz.

---

## MOV-010 — Normal zıplama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Ayakta Space bas
2. Havadayken tekrar Space bas

### Beklenen sonuç

Tek zıplama oluşur; havada ikinci zıplama üretilmez.

---

## MOV-011 — Alçak tavanda kalkış

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. 1,3 m tavan altına çömelerek gir
2. C ile kalkmayı dene

### Beklenen sonuç

Ayakta kapsül tavana geçmez; boşluk olmadan ayakta duruşa geçilmez.

---

## MOV-012 — Yatış tavan geçişi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. 0,9 m yüksek tünelde yat
2. Z ile kalkmayı dene

### Beklenen sonuç

Kapsül engelle kesişmez; tünelden çıkınca kalkış mümkün olur.

---

## MOV-013 — Kapı genişliği

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. 1,3 m açıklıktan ayakta geç
2. İki yönde tekrarla

### Beklenen sonuç

Sözleşmedeki asgari kapı açıklığı iki yönde takılmadan geçilir.

---

## MOV-014 — Rampa geçişi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. 35 derece rampadan yukarı yürü
2. Aşağı dön

### Beklenen sonuç

Merdiven yerine kullanılan rampa kontrollü geçilir; oyuncu zeminden düşmez.

---

## MOV-015 — Duvar çarpışması

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Düz duvara W ile 5 saniye yürü
2. Köşede çapraz yürü

### Beklenen sonuç

Duvarın içinden geçilmez; köşede kalıcı kilitlenme olmaz.

---

## MOV-016 — Sol eğilme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Q basılı nişan al
2. Q bırak

### Beklenen sonuç

Kamera sola eğilir; bırakınca merkezine döner, hitbox duvar arkasını bedelsiz aşmaz.

---

## MOV-017 — Sağ eğilme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. E basılı nişan al
2. E bırak

### Beklenen sonuç

Sağa eğilme ve merkez dönüşü soldaki davranışla tutarlıdır.

---

## MOV-018 — Sprint sırasında eğilme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. W+Shift sırasında Q bas
2. Sprint bırak

### Beklenen sonuç

Sprint sırasında eğilme uygulanmaz; sprint bitince kontrol düzelir.

---

## MOV-019 — Kamera düşey sınır

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Fareyi sürekli yukarı sonra aşağı hareket ettir

### Beklenen sonuç

Bakış takla atmaz; düşey açı sınırlarında kalır.

---

## MOV-020 — Ters Y uygulaması

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Ayarlar ters Y aç
2. Fareyi yukarı hareket ettir
3. Ayarı kapat

### Beklenen sonuç

Bakış yönü anında tersine döner; kapatınca varsayılan yön döner.

---

## MOV-021 — Düşme hasarı sınırı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Test düzeneğinde 12 m/sn iniş hızı ayarla
2. İnişi kaydet

### Beklenen sonuç

12 m/sn için düşme hasarı 0 olur; ölüm olayı oluşmaz.

---

## MOV-022 — Düşme hasarı üstü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Test düzeneğinde 16 m/sn iniş ayarla
2. Can değişimini ölç

### Beklenen sonuç

Zırh dışı düşme hasarı (16−12)×7,5=30; yalnız bir iniş uygulanır.

---

## MOV-023 — Araçta motor kilidi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Kirpi sürücü koltuğuna gir
2. Hareket tuşlarını kullan

### Beklenen sonuç

Karakter yaya motoruyla koltuktan uzaklaşmaz; yalnız araç hareketi uygulanır.

---

## MOV-024 — Odak kaybı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. Yürürken Alt+Tab yap
2. Tuşları dışarıda bırak
3. Oyuna dön

### Beklenen sonuç

Sıkışmış tuş yüzünden sürekli hareket oluşmaz; fare kilidi uygun geri yüklenir.

---

## MOV-025 — Duruş hitbox uyumu

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Player/CharacterControllerMotor.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemcide offline Atış Poligonu; düz 50 m parkur, koordinat ve süre kaydı; varsayılan hareket ayarları.

### Adımlar

1. İkinci test istemcisinden ayakta/çömelmiş/yatan hedefi gözle
2. Görünen başa ateş et

### Beklenen sonuç

Görünen beden ve hasar alanı her duruşta hizalıdır; boş havaya görünmez hitbox kalmaz.

---
