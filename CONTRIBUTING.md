# Contributing — HAREKÂT

## Kod stili

- C# : mevcut dosya stiline uy (Türkçe kullanıcı metinleri, İngilizce tip/üye adları).
- `Project.*` içinde **`UnityEngine.Application`** tam adı.
- Claude/Cursor sahiplik listesine (`Docs/DURUM.md`, `CURSOR_FAZ*.md`) uy; başkasının dalgasındaki dosyaya dokunma.

## Commit

- Kısa, neden odaklı mesaj (Türkçe veya İngilizce tutarlı olsun).
- Yalnızca kendi değiştirdiğin dosyaları stage et.
- `.env`, secret, `Library/`, büyük binary (LFS dışında) commit etme.

## PR kontrol listesi

- [ ] `zsh Tools/UnityVerify/verify.sh … --player --tests` → 0 hata
- [ ] İlgili PlayMode / Backend testleri yeşil
- [ ] Claude alanına dokunulmadı (veya DURUM’a not)
- [ ] Web/Wiki ise lint/test; Deploy ise diğer IIS sitelerine etki yok
- [ ] `Docs/FAZ2_DURUM.md` veya DURUM güncellendi

## Ajanlar

Paralel Claude/Cursor çalışırken: **git checkout/reset/clean/stash yasak**.
