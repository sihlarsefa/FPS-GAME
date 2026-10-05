# Renk Körlüğü Simülasyonu Raporu (UZATMA)

Kapsam: `Design/UI` paleti ve HUD/menü kodlaması. Simülasyon: protanopia, deuteranopia, tritanopia için nitel değerlendirme (yazılım filtreleri: Coblis / Chrome DevTools approximate).

## Özet

| Risk | Durum | Mitigasyon |
|------|--------|------------|
| Dost mavi `#3D9BFF` vs düşman `#FF4A3D` | Orta (proto/deut’ta doygunluk yakınlaşır) | Şekil: daire=dost, elmas/kare=düşman; “Mavi/Kırmızı kuvvet” metni |
| Accent `#E30A17` vs Enemy `#FF4A3D` | Yüksek karışma riski | Accent yalnızca UI chrome; dünya/HUD düşman işaretinde enemy + ikon |
| Zone mavi vs safe beyaz | Düşük | Beyaz halka + süre metni + ses |
| Can çubuğu beyaz→kehribar→kırmızı | Orta | Sayısal can değeri her zaman görünür |
| Kill feed renkleri | Orta | İsim sırası + silah etiketi sabit |

## Protanopia / Deuteranopia

- Kırmızı–yeşil ekseni zayıflar: başarı yeşili (`#5FC84E`) kill feed’de dost isimle karışabilir → **şekil veya “dost” öneki** önerilir.
- Kehribar (`#F2A900`) çoğu simülasyonda sarımsı kalır; rütbe/uyarı için güvenli vurgu.
- Accent kırmızısı koyu zeytin zeminde hâlâ “sıcak” görülür ama düşmandan ayırt için **ikon silueti şart**.

## Tritanopia

- Mavi zone halkası soluklaşabilir → kalınlık + kesikli safe halka + “FAZ / süre” metni zorunlu (mockup’ta mevcut).
- Ally mavi griliğe kayabilir → tim panelinde rütbe SVG + isim yeterince ayırt eder.

## HUD kontrol listesi (Unity uygulaması)

1. Tim pini: mavi dolgu + üçgen yön; düşman: kırmızı + kare.
2. Emir ikonları renk yanında farklı geometri (takip ok, mevzi üçgen, taarruz dolu üçgen, toplan halka).
3. Hasar yönü: sadece kırmızı flaş değil; ok / yay konumu.
4. Mini harita zone: renk körü modunda opsiyonel desen (çizgili halka).

## Sonuç

Palet GDD ile uyumlu; renk körlüğü için **renk + şekil + metin** üçlüsü StyleGuide’da kural olarak sabitlendi. Mockup ikon seti bu ayrımı destekler.
