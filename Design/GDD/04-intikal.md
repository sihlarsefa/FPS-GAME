# 4. İntikal Sistemi (T-70 / Kirpi)

Kaynak: `InsertionMethod`, `MatchConfig` (PlaneAltitude 120, PlaneSpeed 38), Transport katmanı.

## 4.1 Seçenekler

| Yöntem | Enum | Anlatı | Oynanış |
|--------|------|--------|---------|
| T-70 helikopteri | `Helicopter` | Havadan intikal | Yüksekten bakış, geniş iniş seçimi, rotor sesi/toz |
| Kirpi ZPT | `ArmoredVehicle` | Karadan intikal | Yol/patika odaklı, daha güvenli ama yavaş yaklaşım |

Varsayılan oyuncu intikali: **Helikopter**.

## 4.2 T-70 (helikopter)

- **Avantaj:** Haritanın büyük kısmına erken erişim; yüksek nokta (Röle, Karakol) için avantaj.  
- **Dezavantaj:** İniş anında gürültü; açık alanda düşman gözlemine açık.  
- **Tasarım notu:** `PlaneAltitude` / `PlaneSpeed` skydive / rota simülasyonunda kullanılır; “uçak” metaforu teknik isimde kalır, oyuncu yüzünde T-70 anlatılır.  
- **Ses/VFX:** `HelicopterRotor`, rotor tozu (`GameVfx.Dust`).

## 4.3 Kirpi (zırhlı personel taşıyıcı)

- **Avantaj:** Kapalı taşıma hissi; yol ağı üzerinden kontrollü iniş; erken ateş altında daha az “düşüş kırılganlığı”.  
- **Dezavantaj:** Esnek iniş noktası sınırlı; köprü ve ana yol darboğazlarına yönelme riski.  
- **Ses:** `VehicleEngine`, `VehicleDoor`.

## 4.4 Sektör önerileri

| İntikal | Önerilen erken hedefler | Risk |
|---------|-------------------------|------|
| T-70 → kuzey | Sınır Karakolu, Kuzey Gözetleme | Yüksek yağma, erken çatışma |
| T-70 → doğu | İleri Üs, Doğu Gözetleme | Military loot, kalabalık |
| T-70 → batı | Röle Tepesi, Taş Ocağı | Yüksek zemin / orta loot |
| Kirpi → ana yol | Kuzgun Köyü → Yamaç | Güvenli yağma, yavaş tempo |
| Kirpi → güney | Baraj, Yıkık Köy, Ağıl | Orta risk, zone’a bağlı |

## 4.5 Tim AI intikal sonrası

İnişten sonra varsayılan emir **Follow**; komutan yağma/rotaya yön verir. Bölge dışı oluşursa emirler geçici olarak ikincil kalır.
