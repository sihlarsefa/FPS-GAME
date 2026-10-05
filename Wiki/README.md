# HAREKÂT Oyuncu Kılavuzu (Wiki)

Statik saha rehberi. `Design/GDD`, Unity katalogları (`WeaponCatalog`, `RankCatalog`, `LoadoutCatalog`, `ItemCatalog`), Kuzgun Vadisi paftaları ve `Tools/BalanceCalc` TTK çıktısından üretilir.

## Gereksinim

- Node.js ≥ 20
- Bağımlılık yok (düz HTML/CSS/JS + Node yerleşik test)

## Komutlar

```bash
cd Wiki
npm run build   # → dist/
npm test        # katalog + kırık link / eksik sayfa
npm run lint    # sözdizimi kontrolü
```

## Üretilen sayfalar

| Bölüm | Yol |
|-------|-----|
| Ana sayfa / yeni başlayanlar | `index.html`, `baslangic.html` |
| Silahlar + TTK | `silahlar/` |
| Rütbeler + nişan | `rutbeler/` |
| Tim görevleri | `gorevler/` |
| Lokasyonlar | `lokasyonlar/` |
| Kontroller, GDD | `kontroller.html`, `rehber/` |
| Karşılaştırma, harita, yama | `karsilastir.html`, `harita.html`, `yama-notlari.html` |
| EN özet | `en/index.html` |
| Arama indeksi | `assets/search-index.json` |

## IIS

`web.config` (kök ve `dist/`) MIME, sıkıştırma, güvenlik başlıkları (CSP, HSTS), HTTPS yönlendirme ve `.html` uzantısız rewrite içerir.

Dağıtım: `Wiki/dist` içeriğini IIS sitesine kopyalayın (URL Rewrite modülü gerekli).

## Kaynaklar (salt okunur)

- `Design/GDD/*.md`
- `Assets/_Project/Scripts/Application/Catalogs/*.cs`
- `Design/Maps/KuzgunVadisi/**`
- `Tools/BalanceCalc/out/{catalog.json,ttk.csv}`
- `Web/assets/ranks/*.svg` (nişan görselleri)
