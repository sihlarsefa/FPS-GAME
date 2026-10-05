# Gelecek Harita — Bozkurt Sırtları (Karlı Dağ)

Kurgusal yüksek rakım harekât alanı. Kuzgun Vadisi’nden sonra ikinci harita adayı.

## Kimlik

| Alan | Değer |
|------|-------|
| Ad | Bozkurt Sırtları |
| Tema | Karlı dağ, geçit, buzul gölü, maden |
| Ölçek | 1024 × 1024 m (`HalfSize` 512) |
| MaxHeight | ~220 m (daha dik) |
| WaterLevel | ~12 m |
| Pafta | [bozkurt-sirtlari-pafta.svg](bozkurt-sirtlari-pafta.svg) |
| Yerleşim JSON | [bozkurt-sirtlari-yerlesim.json](bozkurt-sirtlari-yerlesim.json) |

## Oynanış farkı

- Görüş: sis / kar ile kısa–orta; KN zirvede güçlü.
- Hareket: dik yamaç yavaşlatır; tünel ve geçit boğazları kritik.
- İntikal: T-70 zirve/radar için değerli; Kirpi ana vadide.
- Risk: açık kar alanı — örtü az; orman kulübesi istisna.

## Lokasyon listesi

| Lokasyon | Kind | Tier | Not |
|----------|------|------|-----|
| Kartal Üssü | ForwardBase | Military | Kuzey FOB |
| Buz Geçidi | Outpost | High | Orta boğaz |
| Çığ Köyü | Village | Medium | Batı yamaç köyü |
| Maden Ocağı | Quarry | Medium | Doğu endüstri |
| Radar Zirvesi | RelayHill | High | KB hakimiyet |
| Kayak Tesisi | Farm | Medium | Sivil yapı reuse |
| Tünel Girişi | Ruins | Medium | Yeraltı yaklaşımı |
| Güney Karakolu | Karakol | High | Güney kapı |
| Orman Kulübesi | Forest | Low | Örtü |
| Buzul Gölü | Dam | Medium | Su + kontrol |
| KB / GD Gözetleme | Outpost | High | Kenar mevzi |

## Ana yollar

1. **Ana Yol (asfalt):** kuzey–güney vadi omurgası, Buz Geçidi’nden geçer.
2. **Üs Yolu:** Kartal Üssü bağlantısı.
3. **Maden Yolu:** doğu ocağı.
4. **Geçit Yolu:** kayak tesisi / doğu sırt.

## İntikal sektörleri

- **S1** Kuzey üs yaklaşımı (Kirpi/T-70)
- **S2** Radar helikopter
- **S3** Maden iniş
- **S4** Güney boğaz

## WorldTypes uyumu

JSON alanları `LocationSpec` / `RoadSpec` / `RiverSpec` / `LakeSpec` ile aynı isimleri kullanır (`Center.x/z`, `Kind`, `Tier`, `Width`, `Points[]`). Unity’ye aktarımda `Points` DensifySpacing ≈ 8 m ile yoğunlaştırılmalıdır.
