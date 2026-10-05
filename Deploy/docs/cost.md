# Maliyet tahmini ve simülasyon tablosu

Varsayımlar: GCP `europe-west3`, e2-standard-4 on-demand ≈ $0.134/saat, spot ≈ $0.040/saat,
%70 spot GameServer, gece ölçek ile ~%25 tasarruf. Tutarlar **yaklaşık USD/ay**.

| CCU | On-demand node | Spot node | Aylık compute (gece ölçekli) | Gece tasarrufu |
|-----|----------------|-----------|------------------------------|----------------|
| 500 | ~3 | ~5 | ~$250 | ~$80 |
| 1.000 | ~4 | ~9 | ~$400 | ~$130 |
| 2.500 | ~9 | ~19 | ~$900 | ~$300 |
| 5.000 | ~16 | ~37 | ~$1.700 | ~$550 |
| 10.000 | ~31 | ~72 | ~$3.200 | ~$1.050 |

Ek: Postgres managed (~$150–400), LB/egress (~$100–500), gözlemlenebilirlik (~$50–200).

Güncel CSV üretmek için:

```bash
make cost-sim
# → Deploy/docs/cost-simulation.csv
```
