# Yapay zekâ

Senaryo sayısı: **12** · Öncelik: P1=12

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## AI-001 — Dost hedefi reddi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Silahlı botun önüne yalnız dost yerleştir
2. 10 saniye izle

### Beklenen sonuç

Bot dostu düşman seçmez, ateş açmaz.

---

## AI-002 — Görünen düşman

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Silahlı botun açık görüşüne düşman koy

### Beklenen sonuç

Bot zorluk tepki süresi sonrası düşmana döner/çatışır; hedef kimliği doğrudur.

---

## AI-003 — Duvar arkasını görmeme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Düşmanı kapalı duvar arkasına al
2. Son bilinen hedefi izle

### Beklenen sonuç

Yeni görüş bilgisi duvardan üretilmez; geçmiş konum bilgisi yeni konumla karıştırılmaz.

---

## AI-004 — Sis görüş engeli

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Bot ve düşman arasına sis at
2. Süre boyunca izle

### Beklenen sonuç

SmokeVolume görüş hattını engeller; kesintisiz tam görüş nişanı sürmez.

---

## AI-005 — Zone emre üstün

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Dışarıdaki bota Hold ver
2. Güvenli alanı gözle

### Beklenen sonuç

Zone kaçışı mevzi emrinden önce gelir; bot ölümüne yerinde beklemez.

---

## AI-006 — Mevzide savunma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Hold emrindeki botun görüşüne düşman sok

### Beklenen sonuç

Mevzi davranışı düşman tehdidini yok saymaz; Engage olur.

---

## AI-007 — Silahsız bot

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Botun silahlarını boşalt
2. Önüne düşman koy

### Beklenen sonuç

Silahsız bot geçersiz silahtan ateş üretmez; uygun karar/yağma davranışı gözlenir.

---

## AI-008 — Mühimmat yağmalama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Botu kullanılabilir mermisiz bırak
2. Uygun kalibre mühimmat koy

### Beklenen sonuç

Bot uygun mühimmata erişebiliyorsa alır; alakasız kalibreyi ateş için kullanmaz.

---

## AI-009 — Lider değişimi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Takipteki lideri öldür
2. Yeni komutanı uzaklaştır

### Beklenen sonuç

Takip hedefi ölü liderde takılı kalmaz; yeni liderle güncellenir.

---

## AI-010 — NavMesh dar geçit

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Timle 1,3 m kapıdan geç
2. 30 saniye izle

### Beklenen sonuç

Botlar birbirini kalıcı kilitlemez; zeminden geçmeden çıkış bulur.

---

## AI-011 — Zorluk tepki karşılaştırması

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Aynı seed sahnesini Easy/Normal/Hard ile 10 kez çalıştır

### Beklenen sonuç

Hedef tepki profilleri 0,9/0,55/0,3 sn ile yönsel uyumlu; dağılım kaydedilir, tek örnekten üstünlük hükmü verilmez.

---

## AI-012 — Bot ölüm temizliği

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BotDecisionService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed; normal zorluk; Mavi/Kırmızı bot takımları; görüş ve emir durumları gözlemci kaydında.

### Adımlar

1. Bir botu öldür
2. Olaylar ve hareketini izle

### Beklenen sonuç

Ölü bot yeni ateş/yağma/komuta kararı üretmez; kayıt listesinden temizlenir.

---
