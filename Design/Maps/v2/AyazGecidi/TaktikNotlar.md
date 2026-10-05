# Ayaz Geçidi

Yüksek dağların arasında iki rotalı geçit. Radar sırtı erken görüş, kayak evi güvenli ikmal sağlar. Kar ve kaya siluetleri açık zemini tehlikeli kılar; beyaz yüzeylerde işaretlerin dış çizgisi korunur.

Kurgu tatbikat: Mavi/Kırmızı kuvvetler. Bu notlar oyun alanı tasarımı içindir.

## Lokasyonlar

### 1. Geçit Köyü · D5

Merkez (-140, 10) m; yarıçap 65 m; tasarım kotu 76 m; yağma Medium.

Merkez geçidin batısında kapalı ikmal. Taş duvarlar kısa hareketleri korur; iki çıkışla kuzey yol baskısından ayrıl.

### 2. Kayak Evi · C3

Merkez (-300, 265) m; yarıçap 52 m; tasarım kotu 100 m; yağma Medium.

Ahşap avluda kısa menzil. Ana binaya sıkışmadan servis yoluna bir gözcü ayır; teras bütün geçidi görmez.

### 3. Radar Üssü · H2

Merkez (275, 305) m; yarıçap 55 m; tasarım kotu 118 m; yağma High.

En uzun görüş hattı. Alt sırt yaklaşımı ve anten kaidesi kör alan yaratır; çevre halkasından geri çekilme gerekir.

### 4. Ayaz Karakolu · F2

Merkez (70, 360) m; yarıçap 48 m; tasarım kotu 106 m; yağma High.

Kuzey kapısını denetler. İç avluya giren tim batı yamaç dönüşünü kaybeder; doğu çıkışı açık tutulur.

### 5. Dağ İkmal Üssü · H7

Merkez (235, -175) m; yarıçap 66 m; tasarım kotu 69 m; yağma Military.

Tek askerî yağma çekirdeği. İki avlu ve servis çukuru güçlü ekipmanı üç yaklaşım riskiyle dengeler.

### 6. Karaçam Sırtı · B7

Merkez (-320, -120) m; yarıçap 65 m; tasarım kotu 70 m; yağma Low.

Düşük yağma, örtülü geçiş. Ağaçları tam görüş engeli varsayma; yamaç altı rota merkezden ayrılır.

### 7. Eski Taş Ocağı · I5

Merkez (300, 55) m; yarıçap 58 m; tasarım kotu 86 m; yağma Medium.

Basamaklı açık alan. Alt kazı örtüsü kullanılır; üst kenarda uzun süre sabit kalınmaz.

### 8. Yayla Ağılı · C9

Merkez (-210, -330) m; yarıçap 52 m; tasarım kotu 61 m; yağma Low.

Sakin başlangıç ve güney rotasyonu. Sınırlı yağma yüzünden on kişi tek binaya yığılmaz.

### 9. Donuk Gözetleme · E4

Merkez (-55, 200) m; yarıçap 30 m; tasarım kotu 94 m; yağma High.

Kayak evi ile karakol arasında erken uyarı. Küçük kaya siperi tek girişli olmadığı için savunma sürekli döner.

### 10. Güney Sığınağı · F9

Merkez (35, -345) m; yarıçap 45 m; tasarım kotu 59 m; yağma Medium.

Geçitten çekilme durağı. Çatı kırıkları hedefi görünür kılar; duvar aralıklarıyla doğu üssüne bağlanır.

## İntikal sektörleri

- **S1 / Kuzey geçit kapısı / Kirpi:** (70, 470), F1. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.
- **S2 / Güney geçit kapısı / Kirpi:** (0, -470), F10. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.
- **S3 / Batı kaya düzlük / T-70:** (-405, 245), A3. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.
- **S4 / Doğu ikmal düzlüğü / T-70:** (385, -160), I7. 20 m kontrol yarıçapı; T-70 için düz iniş ve rotor açıklığı Unity navmesh/fizik testi bekler. Kirpi sektörleri ana yol uçlarına bağlıdır.

## Yağma dağılımı

- Low: 2/10 lokasyon (20%).
- Medium: 4/10 lokasyon (40%).
- High: 3/10 lokasyon (30%).
- Military: 1/10 lokasyon (10%).

Bu oranlar lokasyon sayısıdır; gerçek eşya düşme olasılığı veya nesne bütçesi değildir. LootCatalog kuralları değişmez. Military tek çekirdeğe sınırlı; High üç ayrı yönün riskli noktalarına dağıtılır.

## Ses ortamı

- Üst sırt: yönlü, seyrek rüzgâr; iletişimi maskelemeyecek dinamik aralık.
- Kayak evi: ahşap gıcırtısı, iç/dış geçişte düşük geçiren filtre.
- Radar üssü: düşük mekanik uğultu; yapı dışında hızla söner.
- Kar yürüyüşü: kuru/sert kar için iki yüzey; hız ve duruşa göre örnek seçimi.
- Geçit: kısa kaya yankısı; uzaktan gelen atışların yönü korunur.
- Buz göleti: yalnızca kıyı su sesi; güvenli buz üstü yürüyüşü bu konseptte yok.

Ses erişilebilirliği: kritik intikal, alan daralması ve emir olayları metin/ikonla da verilir. Atmosfer kanalı ayrı ses ayarı; işitme eşikleri gerçek oyuncu testi bekler.
Ayrıntılı liste: [SesOrtami.md](SesOrtami.md).
