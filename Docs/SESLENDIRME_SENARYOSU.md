# Seslendirme Senaryosu (Türkçe ses oyuncuları için)

Amaç: Yayın sürümünde TTS yer tutucuların (v2, dfki CC BY-NC-SA = ticari yasak) gerçek ses oyuncularıyla değiştirilmesi. Tam replik listesi: `Design/Audio/telsiz_replikleri.csv` (anahtar, rol, metin_tr, ton, süre), `Design/Audio/telsiz_replikleri_v2.csv` (varsa) ve `Assets/_Project/Resources/Audio/Voice/v2/voice_manifest_v2.json` (id, metin, süre).

## Roller (en az 4 ses, ideal 6)
| Rol | Karakter | Ses |
|---|---|---|
| Leader (Takım komutanı) | Sakin, otoriter, kısa emirler | Kalın, 35-45 yaş |
| Rifleman (Piyade) | Gergin ama disiplinli | Orta, 25-30 yaş |
| Marksman (Nişancı) | Soğukkanlı, alçak ses | Hafif kısık, 30+ |
| MachineGunner (Makineli) | Gür, yüksek enerji | Güçlü, bas |
| Medic (Sıhhiye) | Şefkatli ama hızlı | Orta, kadın veya erkek |
| Radioman (Telsizci) | Net, tane tane | Orta-tiz, net diksiyon |
Oyuncu (`player_*`): birinci tekil, nefesli, yakın mikrofon hissi.

## Üç stres seviyesi (her replik 3 çekim)
- **Sakin:** normal hız, kontrollü nefes, telsiz disiplini ("Anlaşıldı", "Emredersiniz komutanım").
- **Çatışma:** %15-20 hızlı, daha yüksek ses, kısa nefes, komut tonu; kelimeler hâlâ net.
- **Panik:** %30 hızlı, perde yüksek, nefes nefese, cümle kesilebilir; yine de anlaşılır olmalı (yön/mesafe bilgisi net).
Kaydı sıkıştırarak değil gerçek efor harcayarak (koşup nefes tüketerek) yapın.

## Ton ve telaffuz
- Askerî Türkçe: "Temas!", "Şarjör!", "Emredersiniz komutanım", "Saat on iki yönü, üç yüz metre". Saat yönleri ve mesafe sayıları yazıyla okunur (iki yüz, yüz elli).
- Ünlem cümleleri bağırma değil projeksiyonludur; kulaklıkta tiz patlama olmamalı.
- Argo ve küfür yok. Kurgusal birlik/yer adları CSV'deki gibi okunur.

## Mikrofon ve ortam
- İki set: (1) **Yakın/telsiz** seti: kondansatör mikrofon, 5-10 cm, pop filtre, ölü oda; oyun içi telsiz filtresi (HP 400 Hz, LP 3.5 kHz) kodda uygulanır, kayıt temiz olmalı. (2) **Açık hava** seti: aynı repliklerin 1-2 m mesafeden, hafif ortam yansımalı çekimi (uzaktan bağırma için).
- 48 kHz / 24 bit WAV, mono, klipler arası 1 sn sessizlik; tepe -6 dBFS altında. Her replik 3 alternatif çekim (sonra seçilecek).
- Nefes, öksürük, hırıltı ve yaralı sesler (`Yaralandım!`, ağrı) ayrı efor klibi olarak ekstra kaydedilir.

## Teslim
- Dosya adı: `<id>.wav` (CSV `anahtar`). Klasör: `<oyuncu_adi>/<sakin|catisma|panik>/`. Oyun tarafı `Voice/v2/<ses>/<stres>/<id>` düzenini bekler; kod değişmez.
- Lisans: ses oyuncusu sözleşmesinde oyun içi ticari kullanım ve dağıtım hakkı yazılı olmalı.
