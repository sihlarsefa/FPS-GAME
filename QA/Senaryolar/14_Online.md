# Online ve dayanıklılık

Senaryo sayısı: **16** · Öncelik: P1=16

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## NET-001 — Kuyruk temel akışı

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Giriş yap
2. Tek hesapla kuyruğa gir
3. Bileti izle

### Beklenen sonuç

Bilet ve durum gösterilir; atama olursa tek maç/sunucu bilgisi gelir.

---

## NET-002 — Kuyruk iptali

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Kuyruğa gir
2. Atama öncesi iptal et
3. Tekrar durum sorgula

### Beklenen sonuç

İptal UI ve sunucu durumunda tutarlı; istemci bekliyor ekranında kilitlenmez.

---

## NET-003 — Tim kuyruğu

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. 10 üyeyi aynı timde hazır yap
2. Komutan kuyruğa girsin

### Beklenen sonuç

Tim bölünmeden aynı eşleştirme akışını izler; destek yoksa eksik özellik raporlanır.

---

## NET-004 — On birinci üye

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. 10 kişilik time ek hesapla davet kodundan katıl

### Beklenen sonuç

Üye sayısı 10'u aşmaz; anlaşılır ret gösterilir.

---

## NET-005 — Yanlış davet kodu

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Var olmayan davet koduyla katıl

### Beklenen sonuç

Başka tim açığa çıkmadan hata gösterilir; mevcut tim üyeliği bozulmaz.

---

## NET-006 — Bağlantı sırasında kopma

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Sunucuya bağlanırken ağı 15 sn kes
2. Geri aç

### Beklenen sonuç

Sonuç açık başarı/hata durumuna ulaşır; sonsuz yükleme/çift karakter oluşmaz.

---

## NET-007 — Maç içinde kopma

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Devam eden maçta ağı 15 sn kes
2. Geri aç

### Beklenen sonuç

Belgelenmiş reconnect politikası uygulanır; politika yoksa karar açığı kaydı oluşturulur, başarılı varsayılmaz.

---

## NET-008 — 100 ms RTT

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Şekillendiricide 100 ms RTT, 0 kayıp kur
2. 5 dk çatış

### Beklenen sonuç

P95 komut/hasar gecikmesi ölçülür; çift ateş/çift eşya alımı olmaz.

---

## NET-009 — 200 ms RTT

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Şekillendiricide 200 ms RTT kur
2. Hareket ve atış kaydet

### Beklenen sonuç

Düzeltme sıçramaları ve eylem gecikmesi raporlanır; sunucu otoritesi korunur.

---

## NET-010 — Yüzde 1 kayıp

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. 100 ms RTT + yüzde 1 kayıpla 5 dk oyna

### Beklenen sonuç

Güvenilir eylemler kaybolup çoğalmaz; kopma oranı kaydedilir.

---

## NET-011 — Yüzde 5 kayıp

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. 100 ms RTT + yüzde 5 kayıpla 5 dk oyna

### Beklenen sonuç

Kötü bağlantı anlaşılır; sunucu çökmez, envanter çiftlenmez.

---

## NET-012 — Jitter

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. 100 ms RTT üzerine ±50 ms jitter ekle
2. 5 dk oyna

### Beklenen sonuç

P95/P99 gecikme ve düzeltmeler ölçülür; zaman sırası ters hasar doğurmaz.

---

## NET-013 — Sunucu yeniden başlama

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Test maçındayken kontrollü sunucuyu yeniden başlat

### Beklenen sonuç

İstemci açık hata/menüye dönüş alır; sonuçlar sahte galibiyet diye kaydedilmez.

---

## NET-014 — Token süresi dolması

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. Kısa ömürlü test token ile bekle
2. Profil isteği yap

### Beklenen sonuç

İstemci yenileme/yeniden giriş politikası uygular; token UI/logda açığa çıkmaz.

---

## NET-015 — Aynı eşyaya eşzamanlı alma

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. İki test oyuncusu aynı yağmaya aynı anda F bassın

### Beklenen sonuç

Sunucu tek geçerli sahip/dağıtım üretir; toplam miktar korunur.

---

## NET-016 — Otoritesiz hasar

- **Öncelik:** P1
- **Kaynak:** `Backend/ClientSdk/HarekatClient.cs`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

İzole Windows Dedicated Server + IIS/MSSQL test dağıtımı; iki test hesabı; ağ şekillendirme yalnız yetkili laboratuvarda. Online entegrasyon bulunmuyorsa Engelli.

### Adımlar

1. İstemci tarafı test fixture ile yerel can değerini değiştir
2. Sunucu snapshot bekle

### Beklenen sonuç

Sunucu otoritesi yerel değişikliği maç sonucuna kabul etmez.

---
