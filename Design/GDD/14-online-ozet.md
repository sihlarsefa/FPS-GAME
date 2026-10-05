# 14. Online Mimari Özeti

Kaynak: `Docs/CONTRACTS.md` otorite modeli. Backend ayrı ekip (`Docs/CURSOR_GOREVI.md`).

## 14.1 İlke

**Sunucu otoriteli:** hasar, envanter, AI kararları, zone yalnızca `HasAuthority` tarafında mutasyona uğrar. İstemci öngörü + uzlaştırma.

## 14.2 Katmanlar (oyuncu perspektifi)

```
İstemci (Unity) ←→ Match Instance (oyun sunucusu)
                 ←→ Lobby / Matchmaking API
                 ←→ Profil / Kariyer / Kozmetik servisi
```

## 14.3 Maç örneği

| Kavram | Offline bugün | Online hedef |
|--------|---------------|--------------|
| Oyuncu | 1 insan + 9 AI tim | 10 insan veya karışık doldurma |
| Rakip | N AI tim | Diğer insan timleri |
| Instance | Yerel | Binlerce eşzamanlı maç |

## 14.4 Senkron öncelikleri

1. Pozisyon / niyet (yüksek frekans, sıkıştırılmış)  
2. Atış / isabet olayları (güvenilir)  
3. Envanter delta  
4. Zone / topçu / emir olayları  
5. Kozmetik görünüm (düşük öncelik)

## 14.5 Güvenlik (özet)

- İstemci hasar uygulamaz.  
- Topçu cooldown sunucuda.  
- Hile: hız, duvar arkası, aimbots → sunucu doğrulama + telemetri.
