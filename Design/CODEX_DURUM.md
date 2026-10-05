# Codex FAZ 2 — İlerleme (Cursor ajanları)

> Codex limit / yok → Cursor paralel ajanlar C2-1…C2-8 uyguladı.

| Görev | Klasör | Durum | Doğrulama |
|-------|--------|-------|-----------|
| C2-1 | `Web/` | ✅ | lint/test OK · 52 route sözleşme |
| C2-2 | `Wiki/` | ✅ | build OK · 83 sayfa |
| C2-3 | `QA/` | ✅ | 368 senaryo CSV + TestPlan/Smoke |
| C2-4 | `Tools/SqlReports/` | ✅ | 9 SQL script + views/procs/indexes |
| C2-5 | `Localization/` + `Tools/LocTool/` | ✅ | LocTool 8/8 test · TR/EN/DE/AZ/AR |
| C2-6 | `Design/Maps/v2/` | ✅ | Ayaz Geçidi + Mavi Liman layout.json |
| C2-7 | `Design/UI/` | ✅ | StyleGuide + screens.html + ikonlar |
| C2-8 | `Marketing/LiveOps/` | ✅ | turnuva/lig/takvim/SSS/sysreq |

## Günlük
- 2026-10-05 20:25 — İkinci dalga 8 ajan **hepsi success**. Doğrulama komutları geçti.
- 2026-10-05 20:09 — İlk dalga limit yüzünden öldü; yeniden başlatıldı.

---

# Codex FAZ 3 — İlerleme

| Görev | Klasör | Durum | Doğrulama |
|-------|--------|-------|-----------|
| C3-1 | `Design/Assets/` | ✅ | models/sounds/materials/animations CSV kapsama |
| C3-2 | `Design/Art/Briefs/` | ✅ | silah/araç/asker/yapı briefleri |
| C3-3 | `Design/Audio/` | ✅ | ses haritası + telsiz CSV + ortam/müzik |
| C3-4 | `Design/Teams/` | ✅ | teams.json + emblems/ranks SVG |
| C3-5 | `Design/Tutorial/` | ✅ | tutorial_steps + poligon/zorluk |
| C3-6 | `Design/Progression/` | ✅ | achievements/cosmetics/season1/ekonomi |
| C3-7 | `Web/` | ✅ | İndir / sysreq / patchnotes→HTML+RSS / Tim / Başarımlar |
| C3-8 | `QA/`, `Wiki/` | ⏳ | Claude ENTEGRASYON sonrası |

## C3-7 notları (2026-10-05)
- `#/download`: Windows kurulum yer tutucu, SHA-256, Steam yakında, kurulum adımları.
- `#/requirements`: `Marketing/LiveOps/sistem_gereksinimleri_windows.md` ile tutarlı min/önerilen.
- `content/patchnotes/*.md` → `scripts/patchnotes-build.mjs` → JSON HTML + `rss.xml`.
- `#/teams`: 8 tim + özgün SVG amblemler (`assets/teams/`).
- `#/achievements`: 55’lik katalog (`content/data/achievements.json`).
- UZATMA (basın kiti / KVKK) **yapılmadı** (görev talimatı).
- `web.config` korundu.

## Günlük
- 2026-10-05 21:45 — C3-1…7 dosya envanteri doğrulandı; C3-8 Claude sonrası.
- 2026-10-05 20:28 — FAZ 3 başlatıldı.
