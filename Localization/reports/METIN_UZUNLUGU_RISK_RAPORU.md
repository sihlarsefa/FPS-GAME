# Metin Uzunluğu Risk Raporu

Kaynak: `Localization/strings.csv` — TR referans uzunluğuna göre EN/DE/AZ/AR karşılaştırması.

## Ölçütler

| Seviye | Koşul |
|--------|--------|
| Yüksek | Çeviri/TR ≥ 1.35 veya DE +12 karakter |
| Orta | Çeviri/TR ≥ 1.20 (özellikle AR/DE) |
| Bağlam | `button` dar düğmeler; `title` HUD başlıkları; `body` ipucu/açıklama |

**Taranan anahtar:** 295  
**Risk satırı:** 174

## Önerilen UI bütçeleri (karakter)

| Bağlam | TR hedef | Güvenli üst sınır (tüm diller) |
|--------|----------|--------------------------------|
| Düğme (Primary) | ≤ 12 | ≤ 16 |
| Düğme (Ghost/ikincil) | ≤ 14 | ≤ 18 |
| Ayar satırı etiketi | ≤ 28 | ≤ 36 |
| HUD kısa bildirim | ≤ 40 | ≤ 48 |
| Merkez mesaj / zafer | ≤ 48 | ≤ 56 (veya iki satır) |
| İpucu / açıklama | ≤ 120 | ≤ 160 (kaydırılabilir) |

## Yüksek risk listesi

| Anahtar | Dil | TR uzunluk | Çev. uzunluk | Oran | Bağlam | TR | Çeviri |
|--------|-----|------------|--------------|------|--------|----|--------|
| `menu.difficulty.er` | EN | 2 | 7 | 3.5 | label | Er | Recruit |
| `rank.er` | EN | 2 | 7 | 3.5 | label | Er | Private |
| `rank.category.nco` | EN | 8 | 24 | 3.0 | label | Astsubay | Non-Commissioned Officer |
| `rank.yarbay` | EN | 6 | 18 | 3.0 | label | Yarbay | Lieutenant Colonel |
| `menu.difficulty.er` | DE | 2 | 6 | 3.0 | label | Er | Rekrut |
| `rank.er` | DE | 2 | 6 | 3.0 | label | Er | Soldat |
| `pause.control.sprint` | DE | 3 | 8 | 2.67 | label | Koş | Sprinten |
| `rank.cavus` | DE | 5 | 13 | 2.6 | label | Çavuş | Unteroffizier |
| `item.category.default` | DE | 4 | 10 | 2.5 | label | Eşya | Gegenstand |
| `menu.difficulty.er` | AZ | 2 | 5 | 2.5 | label | Er | Əsgər |
| `rank.er` | AZ | 2 | 5 | 2.5 | label | Er | Əsgər |
| `rank.yarbay` | DE | 6 | 14 | 2.33 | label | Yarbay | Oberstleutnant |
| `damage.vehicle` | AZ | 4 | 9 | 2.25 | label | Araç | Nəqliyyat |
| `item.category.throwable` | AR | 5 | 11 | 2.2 | label | Bomba | قذيفة يدوية |
| `killfeed.environment` | EN | 5 | 11 | 2.2 | label | Çevre | Environment |
| `settings.master_volume` | DE | 7 | 15 | 2.14 | label | Ana ses | Hauptlautstärke |
| `rank.astegmen` | EN | 8 | 17 | 2.12 | label | Asteğmen | Second Lieutenant |
| `rank.category.enlisted` | AZ | 8 | 17 | 2.12 | label | Er/Erbaş | Əsgər/Kiçik rütbə |
| `match.msg.kia` | EN | 12 | 25 | 2.08 | title | ŞEHİT DÜŞTÜN | YOU WERE KILLED IN ACTION |
| `pause.btn.main_menu` | AR | 8 | 16 | 2.0 | button | ANA MENÜ | القائمة الرئيسية |
| `rank.ustegmen` | EN | 8 | 16 | 2.0 | label | Üsteğmen | First Lieutenant |
| `rank.tegmen` | DE | 6 | 12 | 2.0 | label | Teğmen | Oberleutnant |
| `rank.yarbay` | AZ | 6 | 12 | 2.0 | label | Yarbay | Podpolkovnik |
| `menu.difficulty.uzman` | EN | 5 | 10 | 2.0 | label | Uzman | Specialist |
| `menu.difficulty.uzman` | DE | 5 | 10 | 2.0 | label | Uzman | Spezialist |
| `menu.difficulty.uzman` | AZ | 5 | 10 | 2.0 | label | Uzman | Mütəxəssis |
| `pause.control.fire` | DE | 4 | 8 | 2.0 | label | Ateş | Schießen |
| `damage.vehicle` | DE | 4 | 8 | 2.0 | label | Araç | Fahrzeug |
| `pause.control.sprint` | EN | 3 | 6 | 2.0 | label | Koş | Sprint |
| `rank.yarbay.abbr` | DE | 3 | 6 | 2.0 | label | Yb. | OLtCol |
| `menu.difficulty.er` | AR | 2 | 4 | 2.0 | label | Er | مجند |
| `role.default.abbr` | DE | 2 | 4 | 2.0 | label | AS | SOLD |
| `rank.er` | AR | 2 | 4 | 2.0 | label | Er | جندي |
| `rank.uzman_cavus` | DE | 11 | 21 | 1.91 | label | Uzman Çavuş | Spezial-Unteroffizier |
| `order.name.hold` | DE | 8 | 15 | 1.88 | button | Mevzi Al | Stellung halten |
| `pause.btn.settings` | DE | 7 | 13 | 1.86 | button | AYARLAR | EINSTELLUNGEN |
| `settings.title` | DE | 7 | 13 | 1.86 | title | AYARLAR | EINSTELLUNGEN |
| `settings.master_volume` | EN | 7 | 13 | 1.86 | label | Ana ses | Master volume |
| `settings.master_volume` | AR | 7 | 13 | 1.86 | label | Ana ses | الصوت الرئيسي |
| `rank.albay` | AZ | 5 | 9 | 1.8 | label | Albay | Polkovnik |
| `rank.category.officer` | DE | 5 | 9 | 1.8 | label | Subay | Offiziere |
| `item.category.throwable` | EN | 5 | 9 | 1.8 | label | Bomba | Throwable |
| `item.category.throwable` | DE | 5 | 9 | 1.8 | label | Bomba | Wurfwaffe |
| `pause.control.artillery` | DE | 13 | 23 | 1.77 | label | Topçu desteği | Artillerieunterstützung |
| `rank.ustegmen` | DE | 8 | 14 | 1.75 | label | Üsteğmen | Hauptmann a.D. |
| `rank.category.nco` | DE | 8 | 14 | 1.75 | label | Astsubay | Unteroffiziere |
| `item.category.helmet` | AZ | 4 | 7 | 1.75 | label | Kask | Dəbilqə |
| `damage.vehicle` | EN | 4 | 7 | 1.75 | label | Araç | Vehicle |
| `rank.uzman_cavus` | EN | 11 | 19 | 1.73 | label | Uzman Çavuş | Specialist Sergeant |
| `rank.category.specialist` | EN | 11 | 19 | 1.73 | label | Uzman Erbaş | Specialist Enlisted |
| `item.category.boost` | AZ | 7 | 12 | 1.71 | label | Takviye | Gücləndirici |
| `weapon.category.shotgun` | DE | 7 | 12 | 1.71 | label | Pompalı | Schrotflinte |
| `weapon.category.shotgun` | AR | 7 | 12 | 1.71 | label | Pompalı | بندقية خرطوش |
| `road.us_yolu` | AR | 7 | 12 | 1.71 | label | Üs Yolu | طريق القاعدة |
| `notify.single_fire_only` | AR | 13 | 22 | 1.69 | label | Tek ateş modu | وضع الإطلاق الفردي فقط |
| `team.pars` | DE | 9 | 15 | 1.67 | label | Pars Timi | Leoparden-Trupp |
| `settings.btn.apply` | DE | 6 | 10 | 1.67 | button | UYGULA | ÜBERNEHMEN |
| `menu.dialog.confirm` | DE | 6 | 10 | 1.67 | label | ONAYLA | BESTÄTIGEN |
| `rank.tegmen` | EN | 6 | 10 | 1.67 | label | Teğmen | Lieutenant |
| `settings.section.audio` | EN | 3 | 5 | 1.67 | label | SES | AUDIO |
| `settings.section.audio` | DE | 3 | 5 | 1.67 | label | SES | AUDIO |
| `settings.section.audio` | AR | 3 | 5 | 1.67 | label | SES | الصوت |
| `rank.yarbay.abbr` | EN | 3 | 5 | 1.67 | label | Yb. | LtCol |
| `pause.dialog.title` | AR | 14 | 23 | 1.64 | title | Ana menüye dön | العودة للقائمة الرئيسية |
| `rank.ustegmen` | AZ | 8 | 13 | 1.62 | label | Üsteğmen | Baş leytenant |
| `item.ammo_556` | DE | 10 | 16 | 1.6 | label | 5.56 Mermi | 5,56-mm-Munition |
| `item.ammo_762` | DE | 10 | 16 | 1.6 | label | 7.62 Mermi | 7,62-mm-Munition |
| `pause.control.jump` | DE | 5 | 8 | 1.6 | label | Zıpla | Springen |
| `rank.cavus` | EN | 5 | 8 | 1.6 | label | Çavuş | Sergeant |
| `killfeed.environment` | DE | 5 | 8 | 1.6 | label | Çevre | Umgebung |
| `rank.uzman_onbasi` | EN | 12 | 19 | 1.58 | label | Uzman Onbaşı | Specialist Corporal |
| `bot.state.roam` | AZ | 7 | 11 | 1.57 | label | Keşifte | Kəşfiyyatda |
| `bot.state.hold` | DE | 7 | 11 | 1.57 | label | Mevzide | In Stellung |
| `road.us_yolu` | DE | 7 | 11 | 1.57 | label | Üs Yolu | Basisstraße |
| `notify.no_ammo` | DE | 9 | 14 | 1.56 | label | Mermi yok | Keine Munition |
| `location.yikik_koy` | EN | 9 | 14 | 1.56 | label | Yıkık Köy | Ruined Village |
| `damage.arty` | AZ | 11 | 17 | 1.55 | label | Topçu Ateşi | Artilleriya atəşi |
| `team.simsek` | EN | 11 | 17 | 1.55 | label | Şimşek Timi | Thunderbolt Squad |
| `notify.no_order_system` | EN | 20 | 30 | 1.5 | label | Tim emir sistemi yok | Squad order system unavailable |
| `notify.no_frag` | DE | 14 | 21 | 1.5 | label | El bombası yok | Keine Splittergranate |

## Özet bulgular

- Dil dağılımı: DE=64, AR=49, EN=38, AZ=23
- Bağlam dağılımı: label=159, button=8, title=5, body=2
- Almanca düğme/etiketlerde en sık taşma riski (bileşik kelimeler).
- Arapça karakter sayısı düşük olsa da glif genişliği nedeniyle dar düğmelerde taşma olabilir; UI'da `overflow` + `bestFit` veya iki satır düşünülmeli.
- Placeholder'lı metinler (`{0}`) dinamik uzunluk taşır — sayısal eklemeler için ekstra 6–8 karakter pay bırakın.

## Mitigasyon

1. Dar düğmelerde kısa eşanlamlılar (DE: `FORTSETZEN`→`WEITER` gibi) sözlüğe not düşülebilir.
2. Unity Text: `horizontalOverflow = Wrap`, `resizeTextForBestFit` HUD için dikkatli kullanılsın.
3. `validate_placeholders.py` ile çeviri sonrası uzunluk eşiği otomatik kontrol.
