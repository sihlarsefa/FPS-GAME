# HAREKÂT Felaket Kurtarma (DR) Runbook
#
# RTO hedefi: API ≤ 30 dk, GameServer filosu ≤ 45 dk
# RPO hedefi: Postgres ≤ 5 dk (PITR), Redis ≤ 6 saat (RDB) / AOF ile saniyeler

## 1. Olay sınıfları

| Sınıf | Örnek | İlk aksiyon |
|-------|--------|-------------|
| L1 | Tek pod crash | HPA / restart; izle |
| L2 | Bölge API down | Trafiği diğer bölgeye kaydır (ping router) |
| L3 | Postgres corrupt | PITR restore |
| L4 | Küme kaybı | DR bölgesinde Terraform + restore |

## 2. Postgres PITR geri yükleme

```bash
# 1) Yeni boş PVC / pod hazırla (veya ayrı restore StatefulSet)
kubectl -n harekat scale statefulset harekat-postgres --replicas=0

# 2) En son basebackup + WAL'ı bağla
# BACKUP_DIR=/backups/base/<STAMP>
# WAL=/wal-archive

# 3) recovery.signal + postgresql.auto.conf
cat > /var/lib/postgresql/data/pgdata/postgresql.auto.conf <<EOF
restore_command = 'cp /wal-archive/%f %p'
recovery_target_time = '2026-10-05 12:00:00+03'
recovery_target_action = 'promote'
EOF
touch /var/lib/postgresql/data/pgdata/recovery.signal

# 4) Pod'u ayağa kaldır, pg_isready bekle
kubectl -n harekat scale statefulset harekat-postgres --replicas=1
kubectl -n harekat rollout status statefulset/harekat-postgres
```

## 3. Redis kurtarma

1. Deployment'ı durdur.
2. `/data/dump.rdb` veya AOF dosyasını yedekten kopyala.
3. `appendonly yes` ile başlat; `redis-cli ping` doğrula.
4. Matchmaking kuyrukları boşalmış olabilir — oyuncular yeniden kuyruğa girer.

## 4. Bölge failover (ping routing)

1. Etkilenen bölgeyi `harekat-regions` ConfigMap'ten `enabled:false` yap (veya DNS health check).
2. Global `api.harekat.gg` latency routing diğer bölgeye düşer.
3. Agones Fleet diğer bölgede buffer artır (`kubectl patch fleetautoscaler`).

## 5. Blue-green rollback

```bash
cd Deploy/scripts
./rollback.sh
```

## 6. İletişim

- Platform on-call → `#harekat-ops`
- Durum sayfası güncellemesi 15 dk içinde
- Maç içi oyuncular: GameServer bağımsız; yeni maçlar sağlıklı bölgede

## 7. Yıllık tatbikat

- [ ] Q1: Postgres PITR tatbikatı
- [ ] Q2: Bölge failover tatbikatı
- [ ] Q3: Redis AOF kayıp senaryosu
- [ ] Q4: Tam küme recreate (Terraform)
