# HAREKÂT — Tim Kimlikleri, Amblemler ve Rütbe Nişanları

Özgün kurgu varlıklar. **Resmi TSK armaları / logoları kopyalanmaz**; hilal–yıldız esintisi yalnızca stilize geometri olarak kullanılır.

Kaynak sözleşme: `MilitaryRank` (19 değer), maçta 8 tim (Kartal → Atmaca).

## Klasör

| Yol | İçerik |
|-----|--------|
| `emblems/*.svg` | 8 tim amblemi (128×128 viewBox) |
| `teams.json` | Renk, kol bandı, slogan, kısa hikâye (TR/EN) |
| `ranks/*.svg` | 19 rütbe nişanı (64×64, stilize) |
| `ranks.json` | Enum eşlemesi + kategori |
| `export.mjs` | 64/128/256/512 PNG dışa aktarım betiği |

`Design/UI/icons/ranks/` aynı 19 SVG ile senkron tutulur (HUD / menü prototipleri). Eski `astsubay.svg` genel alias olarak kalır.

## Timler

| id | Ad | Kol bandı | Slogan |
|----|-----|-----------|--------|
| `kartal` | Kartal Timi | `#3D9BFF` | Yüksekten bak, net vur. |
| `bozkurt` | Bozkurt Timi | `#8A9199` | İz peşinde, birlik içinde. |
| `simsek` | Şimşek Timi | `#2E8BC0` | Ani vuruş, temiz çıkış. |
| `yildirim` | Yıldırım Timi | `#F2A900` | Gökten iner, zeminde kalır. |
| `kilic` | Kılıç Timi | `#E30A17` | Keskin hat, net emir. |
| `kaplan` | Kaplan Timi | `#D4722A` | Sessiz av, güçlü baskı. |
| `pars` | Pars Timi | `#C4A574` | Dağ geçidi bizim. |
| `atmaca` | Atmaca Timi | `#5FC84E` | Kanatta göz, yerde karar. |

Tam hikâye ve ikincil renkler: `teams.json`.

## Rütbe nişanları

Dosya adları ASCII kebab-case; sıra `MilitaryRank` ile aynı (0=Er … 18=Albay).

Görsel dil `MenuRankInsignia` ile uyumlu **stilize** işaretler:
- Er/erbaş: kırmızı şerit / çavuş
- Uzman erbaş: altın çavuş + çubuk
- Astsubay: altın çubuk (± yıldız)
- Subay: kalkan + yıldız (± palamut çubuğu)

## Oyunda kullanım

| Yüzey | Kullanım |
|-------|----------|
| Kol bandı dokusu | `armband` hex → procedural / atlas renk katmanı; amblem silueti omuz veya bere rozeti |
| HUD tim paneli | Küçük amblem (64) + tim adı; dost renk `armband` / `primary` |
| Skor / sonuç ekranı | 128–256 amblem; “KAZANAN TİM” yanında |
| Kariyer / profil | Rütbe nişanı + isteğe bağlı favori tim amblemi |
| Web profil / Tim Kimlikleri sayfası | C3-7 `Web/` sayfası bu klasörü okur |
| Minimap / tam harita | Tim rengi nokta; amblem yalnızca seçili tim vurgusunda |

## Dışa aktarım

```bash
# Bağımlılıksız: önce rsvg-convert veya Inkscape dener; yoksa talimat yazar.
node Design/Teams/export.mjs
# Çıktı: Design/Teams/export/{64,128,256,512}/*.png
```

PNG üretimi için sistemde `rsvg-convert` (librsvg) veya `inkscape` gerekir. Yoksa betik komut satırını yazar; SVG kaynak olarak kalır.

## Kurallar

- Gerçek örgüt / birlik adı yok; Mavi–Kırmızı kuvvet tatbikat kurgusu.
- Resmi arma, flama veya nişan birebir kopyalanmaz.
- Palet: `Design/UI/StyleGuide.md` token’larıyla uyumlu (accent `#E30A17`, amber `#F2A900`, …).
