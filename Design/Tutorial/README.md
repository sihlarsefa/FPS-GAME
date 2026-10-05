# Eğitim (Tutorial) ve İlk Deneyim — C3-5

HAREKÂT’ın yeni oyuncu yolu: **Atış Poligonu** görev zinciri → **İlk 3 maç** bağlamsal ipuçları → isteğe bağlı **Er / Uzman / Komando** zorluk parkuru.

## Dosyalar

| Dosya | İçerik |
|-------|--------|
| [`tutorial_steps.json`](tutorial_steps.json) | Unity okunabilir adım verisi (`Core/Events` olay adlarıyla uyumlu) |
| [`poligon_adimlar.md`](poligon_adimlar.md) | 18 adımlık poligon görev zinciri (TR/EN metin, koşul, ipucu, süre) |
| [`ilk_mac_ipuclari.md`](ilk_mac_ipuclari.md) | İlk 3 maçta bağlamsal ipuçları ve tetikleyiciler |
| [`zorluk_parkur.md`](zorluk_parkur.md) | UZATMA: Er → Uzman → Komando parkuru |

## Entegrasyon notları (Unity / Cursor)

- Sahne: `TrainingBootstrap` (TrainingRange). Poligon görev motoru bu bootstrap üzerine eklenir.
- Olay dinleme: `IEventBus` → `Project.Core.Events.*`.
- Oyun içi olayı olmayan girdiler (hareket, eğilme, ADS, harita açma, araç binme) için `presentationSignal` alanı kullanılır; Unity sunum katmanı bu sinyalleri yayınlar.
- Kontrol şeması: `PauseMenu` kılavuzu ile aynı (WASD, Q/E, F1–F4, V, F=binme…).

## Adım sayısı

**18** poligon adımı (`tutorial_steps.json` → `steps[]`).

## İlişkili GDD

- [21-ilk-10-dk-tutorial.md](../GDD/21-ilk-10-dk-tutorial.md)
- [20-ui-ux-akislari.md](../GDD/20-ui-ux-akislari.md)
- [08-topcu-destegi.md](../GDD/08-topcu-destegi.md)
