# Gelecek Harita — Liman Koyu (Kıyı Kasabası)

Kurgusal Ege/Akdeniz esintili kıyı kasabası. Şehir içi CQB + liman açık alan karışımı.

## Kimlik

| Alan | Değer |
|------|-------|
| Ad | Liman Koyu |
| Tema | Liman, tersane, çarşı, zeytin sırtı |
| Ölçek | 1024 × 1024 m |
| MaxHeight | ~90 m (daha alçak) |
| WaterLevel | ~16 m (deniz güney/doğu) |
| Pafta | [liman-koyu-pafta.svg](liman-koyu-pafta.svg) |
| Yerleşim JSON | [liman-koyu-yerlesim.json](liman-koyu-yerlesim.json) |

## Oynanış farkı

- Yoğun bina: Merkez Çarşı / Balıkçı Mahallesi / Üst Mahalle CQB.
- Liman ve tersane: uzun koridor + konteyner örtü; Military loot limanda.
- Deniz kenarı: kaçış sınırlı — flanş ve KN fener burnunda güçlü.
- Zeytin Sırtı: düşük loot, kuşatma ve T-70 sızması.

## Lokasyon listesi

| Lokasyon | Kind | Tier | Not |
|----------|------|------|-----|
| Merkez Çarşı | Village | Medium | Harita kalbi |
| Liman | ForwardBase | Military | Ana kale |
| Tersane | Quarry | High | Endüstri CQB |
| Fener Burnu | Outpost | High | KN burnu |
| Balıkçı Mahallesi | Village | Medium | Dar sokak |
| Üst Mahalle | Village | Medium | Yükseklik |
| Kışla | Karakol | High | KD askeri |
| Depo Sahası | Farm | Medium | Açık depo |
| Zeytin Sırtı | Forest | Low | Batı örtü |
| İskele Oteli | Ruins | Medium | Sahil harabe |
| Kuzey Karakol | Karakol | High | Kara girişi |
| Doğu Tersane Giriş | Outpost | High | Kenar |

## Ana yollar

1. **Sahil Yolu (asfalt):** batı–doğu kıyı omurgası.
2. **Çarşı Yolu:** kuzey–güney şehir omurgası.
3. **Kışla Yolu / Zeytin Yolu:** kanat bağlantıları.

## İntikal sektörleri

- **S1** Kuzey karayolu (Kirpi)
- **S2** Zeytin helikopter
- **S3** Tersane iniş
- **S4** Liman helipad

## Tasarım notu

`LocationKind` mevcut enum ile sınırlıdır; tersane `Quarry`, liman `ForwardBase`, otel `Ruins` olarak eşlenir. İleride yeni `Kind` eklenirse JSON güncellenir.
