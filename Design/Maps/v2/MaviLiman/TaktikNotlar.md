# Mavi Liman

Kıyı kasabası, liman ve zeytinlik arasında kısa rotasyonlar. İç sokaklar yakın menzile, sahil yolu uzun görüşe açılır. Deniz mevcut göl primitive’lerinin sınırda kesişen iki su diskiyle temsil edildiği bir tasarım önerisidir; liman yapıları için ayrı üretici gerekir.

Kurgu tatbikat: Mavi/Kırmızı kuvvetler. Bu notlar oyun alanı tasarımı içindir.

## Lokasyonlar

### 1. Kıyı Kasabası · F6

Merkez (60, -90) m; yarıçap 66 m; tasarım kotu 30 m; yağma Medium.

Sokak halkası iki avluyu bağlar. Pazar çatısı deniz yönünü görür fakat batı sokak çıkışını göremez.

### 2. Liman Depoları · H8

Merkez (245, -265) m; yarıçap 55 m; tasarım kotu 25 m; yağma Military.

Askerî yağma depolarda toplanır. Kıyı tarafı çıkmaz olduğu için iç yol ve kuru kanal üzerinden iki geri çekilme rotası gerekir.

### 3. Deniz Feneri · H4

Merkez (260, 145) m; yarıçap 35 m; tasarım kotu 47 m; yağma High.

Tek kuleye bağımlı savunma engellenir; alçak servis yapısı alternatif siper sağlar. Tepeye araç çıkışı varsayılmaz.

### 4. Zeytinlik · C4

Merkez (-290, 155) m; yarıçap 76 m; tasarım kotu 43 m; yağma Low.

Geniş ama seyrek örtü. Teras duvarları rotasyonu böler; yağma küçük bakım kulübelerinde tutulur.

### 5. Sahil Karakolu · F2

Merkez (45, 315) m; yarıçap 55 m; tasarım kotu 41 m; yağma High.

Kuzeyden geliş ile kıyı yolunu bağlar. Araç girişi ön avluda görünür; yaya için arka servis aralığı vardır.

### 6. Eski Pazar · D8

Merkez (-130, -270) m; yarıçap 55 m; tasarım kotu 33 m; yağma Medium.

Güney girişinden dengeli başlangıç. Tezgâhlar tam mermi koruması değildir; sağlam bina köşeleri okunur.

### 7. Taş Teras · C6

Merkez (-300, -75) m; yarıçap 55 m; tasarım kotu 39 m; yağma Medium.

Limanı uzaktan izler; aradaki düşük sırt görüşü keser. Alt basamak doğuya örtülü yaklaşım verir.

### 8. Çam Korusu · B2

Merkez (-340, 350) m; yarıçap 62 m; tasarım kotu 54 m; yağma Low.

Sessiz başlangıç. Sınırdan çok iç yamaca çıkış hedeflenir; güneydoğu açık geçişe erken hazırlık gerekir.

### 9. Kanal Gözetleme · E4

Merkez (-65, 105) m; yarıçap 28 m; tasarım kotu 38 m; yağma High.

Dere geçişlerini duyurur. Dar siper iki farklı köprüye aynı anda hâkim olamaz.

### 10. Güney Hanı · F9

Merkez (70, -395) m; yarıçap 42 m; tasarım kotu 27 m; yağma Medium.

Güney intikalinden ilk durak. Duvar boşlukları pazar ve depolara eşit mesafede geçiş sunar.

## İntikal sektörleri

- **S1 / Kuzey sahil kapısı / Kirpi:** (45, 470), F1. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.
- **S2 / Güney han kapısı / Kirpi:** (0, -470), F10. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.
- **S3 / Batı teras açıklığı / T-70:** (-420, -70), A6. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.
- **S4 / Zeytinlik açıklığı / T-70:** (-190, 405), D1. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.

## Yağma dağılımı

- Low: 2/10 lokasyon (20%).
- Medium: 4/10 lokasyon (40%).
- High: 3/10 lokasyon (30%).
- Military: 1/10 lokasyon (10%).

Bu oranlar lokasyon sayısıdır; gerçek eşya düşme olasılığı veya nesne bütçesi değildir. LootCatalog kuralları değişmez. Military tek çekirdeğe sınırlı; High üç ayrı yönün riskli noktalarına dağıtılır.

## Ses ortamı

- Liman: aralıklı halat/gövde sesi; sürekli metal gürültüsü adımı örtmez.
- Deniz feneri: tepe rüzgârı, taş iç mekânda kısa yankı.
- Zeytinlik: yumuşak yaprak sürtünmesi, az yoğunlukta böcek sesi.
- Kıyı kasabası: dar sokak yansımaları; kapalı evlerde belirgin ses geçişi.
- Sahil: kıyıya yaklaştıkça artan dalga; 60 m sonra belirgin azalır.
- Sahil karakolu: sakin jeneratör; iki girişin ses bilgisi eşit tutulur.

Ses erişilebilirliği: kritik intikal, alan daralması ve emir olayları metin/ikonla da verilir. Atmosfer kanalı ayrı ses ayarı; işitme eşikleri gerçek oyuncu testi bekler.
Ayrıntılı liste: [SesOrtami.md](SesOrtami.md).
