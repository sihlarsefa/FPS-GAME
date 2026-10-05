# RTL arayüz riskleri — Arapça (UZATMA C2-5)

Hedef locale: `ar`. Kaynak tablolar: `Localization/strings.csv`, `unity/strings_ar.json`.

## Özet

Unity uGUI `Text` + LegacyRuntime **RTL için uygun değil**. TextMeshPro (`isRightToLeftText`) + Arapça yüz (Noto Naskh Arabic / benzeri) zorunlu. Sayılar, ikonlar ve HUD yerleşimi ayrı kontrol ister.

## Risk matrisi

| Alan | Risk | Etki | Azaltma |
|------|------|------|---------|
| Metin yönü | Yüksek | Cümleler LTR sırada okunur | TMP `isRightToLeftText`; `LocalizeStringEvent` ile locale değişiminde aç/kapa |
| Biçimli sayı `{0}` | Orta | `12 Tim` → rakam solda kalabilir | Smart String / ayrı `string.Format` sırası; ar for `%d` konumunu çeviride tut |
| HUD sabit paneller | Yüksek | Sol alt can / sağ alt cephane aynalanmazsa bilişsel çatışma | Locale=`ar` iken mirror layout veya simetrik HUD |
| İkon + metin satırları | Yüksek | Ok ikonları yanlış yön | Sprite’ları mirror etme; metin bloğunu RTL yap |
| Kill feed / bildirim | Orta | Zaman damgası + isim kayması | Tek satır TMP, `alignment = Right` |
| Mini harita / pusula | Düşük | Harita coğrafyası aynalanmamalı | Yalnızca etiket metinleri RTL |
| Klavye kısayolları | Orta | `F1 takip` harfleri LTR kalmalı | Kısayol kodlarını ayrı span / rich text olarak LTR işaretle |
| Uzun düğme metni | Yüksek | DE/AR taşması | `validation.json` layout-review; düğme min genişlik + wrap |
| Harf birleştirme (shaping) | Kritik | Ayrı glifler okunaksız | OpenType Arapça font; LegacyRuntime kullanma |
| Karışık TR/AR test | Orta | Geliştirici locale karışımı | Editor’da locale switch smoke |

## Etkilenen anahtar örnekleri

- `pause.btn.*`, `settings.btn.*` — kısa CTA, RTL hizası
- `loading.tip.*`, `notify.*` — uzun cümle, satır kaydırma
- `hud.*`, `match.msg.*` — dinamik `{0}` placeholder
- `order.name.*` — 4 emir etiketi, ikon yan yana

## Uygulama kontrol listesi (Cursor / Claude)

1. Locale `ar` seçilince tüm menü/HUD TMP bileşenlerinde RTL aç.
2. LayoutGroup `childAlignment` ve `anchor` için `ar` varyantı veya mirror root.
3. Font asset: Latin HUD + Arapça fallback zinciri (TMP Font Asset fallback).
4. Play Mode: ana menü → ayarlar → maç HUD → ölüm ekranı ekran görüntüsü.
5. LocTool `npm run validate -- --strict` + uzunluk raporu gözden geçir.

## Kabul kriteri

- Arapça locale’de hiçbir UI metni LTR cümle gibi okunmaz.
- Rakam ve silah model adları (MPT-76) şekil bozulmadan görünür.
- LegacyRuntime ile `ar` ship edilmez.
