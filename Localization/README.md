# Yerelleştirme (GÖREV 5)

HAREKÂT UI metin envanteri. **Yalnızca bu klasöre yazılır**; `Assets/` salt okunur tarandı.

## Çıktılar

| Dosya | Açıklama |
|-------|----------|
| `strings.csv` | Anahtar, TR, EN, DE, AZ, AR, `source_file` |
| `UNITY_LOCALIZATION_GECIS.md` | Unity Localization paketine geçiş önerisi |
| `ASKERI_TERIMLER_SOZLUGU.md` | Askeri terimler sözlüğü (TR/EN) |
| `scripts/validate_placeholders.py` | Placeholder / çoğul / uzunluk doğrulama |
| `reports/METIN_UZUNLUGU_RISK_RAPORU.md` | UI taşma riski raporu |

## Doğrulama

```bash
python3 Localization/scripts/validate_placeholders.py
```

## Tarama kapsamı (salt okunur)

- `Assets/_Project/Scripts/Presentation/UI/*`
- `Assets/_Project/Scripts/Presentation/Player/*`
- `Assets/_Project/Scripts/Presentation/Bootstrap/*`
- `Assets/_Project/Scripts/Application/Catalogs/*`
- `Assets/_Project/Scripts/Application/Services/*`
- `Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs`

Oyun nesnesi adları, `Debug.Log*`, `[Tooltip]` / `[Header]` ve `NameRoster` özel adları CSV’ye alınmadı (UI metni değil).
