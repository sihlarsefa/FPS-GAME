using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Transport;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Bot oluşturma parametreleri (<see cref="BotController.Create"/>).
    /// Transport atanmışsa bot o aracın <see cref="Seat"/> koltuğunda başlar ve araç inişinde (Arrived/ReleasePassengers)
    /// iner; aksi halde <see cref="GroundPosition"/> çevresinde NavMesh üzerinde doğar.
    /// </summary>
    public sealed class BotSpawnArgs
    {
        public PlayerId Id { get; set; } = PlayerId.Invalid;

        /// <summary>Rütbesiz ad ("Kartal"); görünen ad RankCatalog.FormatName ile oluşturulur.</summary>
        public string Name { get; set; }

        public int Team { get; set; }
        public TeamRole Role { get; set; } = TeamRole.Rifleman;
        public BotDifficulty Difficulty { get; set; } = BotDifficulty.Normal;

        /// <summary>İntikal aracı (null = yerde doğ).</summary>
        public TransportVehicle Transport { get; set; }

        /// <summary>Araç koltuk numarası (Transport varsa).</summary>
        public int Seat { get; set; }

        /// <summary>true ise Transport yok sayılır ve doğrudan yerde doğar.</summary>
        public bool SpawnOnGround { get; set; }

        public Vector3 GroundPosition { get; set; }

        /// <summary>Başlangıç tim lideri (yoksa komuta zincirinden çözülür).</summary>
        public Combatant SquadLeader { get; set; }

        public int Seed { get; set; }

        // ------------------------------------------------------------------ ek (isteğe bağlı) alanlar

        /// <summary>Timdeki sıra (0 = komutan). -1 → göreve göre türetilir (rütbe ve piyade silah çeşidi için).</summary>
        public int Slot { get; set; } = -1;

        /// <summary>Yerde doğuşta bakış yönü (derece, Y ekseni).</summary>
        public float GroundYaw { get; set; }

        /// <summary>Rütbe zorlaması (null → RankCatalog.RankForTeamSlot).</summary>
        public MilitaryRank? Rank { get; set; }

        /// <summary>Ölümde envanter yere düşsün mü.</summary>
        public bool DropLootOnDeath { get; set; } = true;

        /// <summary>MatchService ve ChainOfCommandService kaydı yapılsın mı (eğitim modunda kapatılabilir).</summary>
        public bool RegisterWithMatch { get; set; } = true;

        /// <summary>Görev teçhizatı verilsin mi (false → boş envanter).</summary>
        public bool GiveLoadout { get; set; } = true;

        /// <summary>Oluşturulan nesnenin ebeveyni (isteğe bağlı, sahne düzeni için).</summary>
        public Transform Parent { get; set; }
    }
}
