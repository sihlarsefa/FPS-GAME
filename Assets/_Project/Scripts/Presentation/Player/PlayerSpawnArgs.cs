using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Transport;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Yerel oyuncu oluşturma parametreleri (bkz. <see cref="PlayerController.Create"/>).
    /// Transport atanmışsa oyuncu araçtaki <see cref="Seat"/> koltuğunda başlar; aksi halde
    /// <see cref="GroundPosition"/>/<see cref="GroundYaw"/> ile yere konur.
    /// </summary>
    public sealed class PlayerSpawnArgs
    {
        public PlayerId Id { get; set; } = new PlayerId(0);

        /// <summary>Rütbesiz görünen ad (rütbe kısaltması otomatik eklenir).</summary>
        public string Name { get; set; } = "Komutan";

        public int Team { get; set; }
        public TeamRole Role { get; set; } = TeamRole.Leader;

        /// <summary>İntikal aracı (helikopter/Kirpi). Null ise yerde başlar.</summary>
        public TransportVehicle Transport { get; set; }

        /// <summary>İntikal aracındaki koltuk numarası.</summary>
        public int Seat { get; set; }

        public Vector3 GroundPosition { get; set; }
        public float GroundYaw { get; set; }

        /// <summary>Oyuncu ayarları (hassasiyet, FOV, ters Y). Null ise SettingsService/varsayılan.</summary>
        public GameSettings Settings { get; set; }

        /// <summary>
        /// İsteğe bağlı rütbe. Boşsa kariyer rütbesi kullanılır; tim komutanı rolünde en az Yüzbaşı olur
        /// (komuta zinciri oyuncuyla başlasın diye).
        /// </summary>
        public MilitaryRank? Rank { get; set; }

        /// <summary>Hazır teçhizat yerine özel loadout uygulamak için (null = rol teçhizatı).</summary>
        public Loadout Loadout { get; set; }

        /// <summary>false ise hiç teçhizat verilmez (antrenman modu kendi verir).</summary>
        public bool ApplyLoadout { get; set; } = true;

        /// <summary>Antrenman: mermi tükenmez.</summary>
        public bool InfiniteAmmo { get; set; }

        /// <summary>false ise MatchService/ChainOfCommandService kaydı yapılmaz (antrenman).</summary>
        public bool RegisterInMatch { get; set; } = true;

        /// <summary>Can üst sınırı.</summary>
        public float MaxHealth { get; set; } = 100f;
    }
}
