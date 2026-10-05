# HAREKÂT kapasite hesabı

## Tasarım sabitleri

| Parametre | Değer |
|-----------|--------|
| Tim boyutu | 10 oyuncu |
| Maç başına oyuncu | 40–60 (4–6 tim) |
| Ortalama doluluk | 50 oyuncu / maç |
| GameServer | Maç başına 1 Agones GameServer |
| CPU / sunucu | 1–2 vCPU (request 1, limit 2) |
| RAM / sunucu | 2–4 GiB |
| Backend pod | 250m–1000m CPU, 256–512 Mi |

## Formüller

```
eşzamanlı_maç ≈ CCU / 50
gerekli_ready_buffer ≈ 5  (veya CCU/1000 * 2)
toplam_gameserver ≈ eşzamanlı_maç + buffer
node_sayısı ≈ ceil(toplam_gameserver / 2)   # e2-standard-4 ≈ 2 sunucu
```

## Hedef senaryolar

| CCU | Maç | Sunucu (+buf) | Node (≈) | Bölge dağılımı (IST/FRA/AMS) |
|-----|-----|---------------|----------|------------------------------|
| 500 | 10 | 15 | 8 | 8 / 4 / 3 |
| 1.000 | 20 | 25 | 13 | 12 / 7 / 6 |
| 2.500 | 50 | 55 | 28 | 25 / 15 / 15 |
| 5.000 | 100 | 105 | 53 | 45 / 30 / 30 |
| 10.000 | 200 | 205 | 103 | 90 / 60 / 55 |

## Ping routing

İstemci üç bölgeye UDP/TCP probe atar; `maxPingMs` ve sticky TTL ile en düşük RTT seçilir.
Türkiye oyuncuları ağırlıklı **eu-istanbul**; AB **eu-frankfurt** / **eu-amsterdam**.
