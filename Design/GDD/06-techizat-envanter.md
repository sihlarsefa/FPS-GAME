# 6. Teçhizat ve Envanter

Kaynak: `ItemCatalog`.

## 6.1 Mühimmat

| Eşya | Ağırlık / adet | Yerden alınan |
|------|----------------|---------------|
| 9mm Mermi | 0.375 | 30 |
| 5.56 Mermi | 0.5 | 30 |
| 7.62 Mermi | 0.7 | 30 |
| 12 Kalibre Fişek | 1.25 | 10 |

## 6.2 Tıbbi

| Eşya | Ağırlık | Kullanım | İyileşme | Tavan |
|------|---------|----------|----------|-------|
| Sargı Bezi | 2 | 4 sn | +10 | 75 HP |
| İlk Yardım Çantası | 10 | 6 sn | +75 | 75 HP |
| Sıhhiye Çantası | 20 | 8 sn | +100 | 100 HP |

**Oynanış kuralı:** Sargı/ilk yardım 75 HP’ye kadar; tam can için sıhhiye çantası veya boost sonrası bakım gerekir.

## 6.3 Takviye (boost)

| Eşya | Ağırlık | Kullanım | Boost |
|------|---------|----------|-------|
| Enerji İçeceği | 4 | 4 sn | +40 |
| Ağrı Kesici | 10 | 6 sn | +60 |

Boost, savaş sonrası toparlanma ve zone koşusunda avantaj sağlar (can yenileme eğrisi sunum katmanında).

## 6.4 Atılabilir

| Eşya | Ağırlık | İşlev |
|------|---------|--------|
| El Bombası | 12 | Frag hasar (`DamageSourceIds.FragGrenade`) |
| Sis Bombası | 14 | Görüş kesme (`SmokeVolume`) |

## 6.5 Çelik yelek

| Seviye | Dayanıklılık | Hasar azaltma |
|--------|--------------|---------------|
| 1 | 200 | %30 |
| 2 | 220 | %40 |
| 3 | 250 | %55 |

## 6.6 Kask

| Seviye | Dayanıklılık | Hasar azaltma |
|--------|--------------|---------------|
| 1 | 80 | %30 |
| 2 | 150 | %40 |
| 3 | 230 | %55 |

Kask özellikle kafa çarpanına karşı kritik (JNG-90 HS×2.5).

## 6.7 Sırt çantası

| Seviye | Kapasite (ağırlık birimi) |
|--------|---------------------------|
| 1 | 150 |
| 2 | 200 |
| 3 | 250 |

## 6.8 Envanter ilkeleri

1. Silahlar slot bazlı; eşya ağırlığı kapasiteyi tüketir.  
2. Ölümde `DropLootOnDeath` → yerde yağma.  
3. Yağma kademeleri harita lokasyonuna bağlı (`LootTier`: Low → Military).  
4. Dost ateşi kapalıysa müttefik yağması kuralları sunum/oyun politikasına bağlı (offline: düşman cesedi öncelikli).
