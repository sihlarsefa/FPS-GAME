using System;

namespace Project.Application.Dialogue
{
    /// <summary>Asker stres hâli: sakin / çatışma / panik.</summary>
    public enum DialogueStress
    {
        Calm = 0,
        Combat = 1,
        Panic = 2
    }

    /// <summary>Sesin yayın yolu: yakın bağırma (3D, filtresiz, yüksek) ya da telsiz (2D, filtreli).</summary>
    public enum DialogueChannel
    {
        None = 0,
        Shout = 1,
        Radio = 2
    }

    /// <summary>Replik önceliği. High ve üstü kesilemez (araya girilmez), sıraya alınır.</summary>
    public enum DialoguePriority
    {
        Ambient = 0,
        Chatter = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>telsiz_replikleri_v2.csv kategori adları (sabitler).</summary>
    public static class DialogueCats
    {
        public const string MagEmpty = "mag_empty";
        public const string Reload = "reload";
        public const string ReloadLast = "reload_last";
        public const string AmmoRequest = "ammo_request";
        public const string GrenadeThrow = "grenade_throw";
        public const string GrenadeIncoming = "grenade_incoming";
        public const string Wounded = "wounded";
        public const string HitSelf = "hit_self";
        public const string Bleeding = "bleeding";
        public const string Critical = "critical";
        public const string Medic = "medic";
        public const string ManDown = "man_down";
        public const string SelfHeal = "self_heal";
        public const string RevivedThanks = "revive_thanks";
        public const string Reviving = "reviving";
        public const string EnemyDown = "enemy_down";
        public const string EnemyDownHeadshot = "enemy_down_hs";
        public const string EnemyDownMulti = "enemy_down_multi";
        public const string CoverMe = "cover_me";
        public const string Covering = "covering";
        public const string Suppress = "suppress";
        public const string Advance = "advance";
        public const string FallBack = "fall_back";
        public const string Holding = "holding";
        public const string Flank = "flank";
        public const string Clear = "clear";
        public const string Smoke = "smoke";
        public const string TakingFire = "taking_fire";
        public const string SniperWarn = "sniper_warn";
        public const string FriendlyFire = "friendly_fire";
        public const string ReportToLeader = "report_to_leader";
        public const string LowHealth = "low_health";
        public const string Watch = "watch";
        public const string Breath = "breath";

        public const string PartOpen = "part_open";
        public const string PartClock = "part_clock";
        public const string PartNum = "part_num";
        public const string PartUnit = "part_unit";
        public const string PartTarget = "part_target";
        public const string PartTail = "part_tail";
        public const string ContactFull = "contact_full";

        public const string AckEnlisted = "ack_er";
        public const string AckNco = "ack_nco";
        public const string AckPeer = "ack_peer";
        public const string AckSuperior = "ack_superior";
        public const string AckNegative = "ack_negative";
    }

    /// <summary>Kategori kuralları: öncelik, bekleme süresi, hangi yollardan söylenebileceği.</summary>
    public readonly struct DialogueCategoryRule
    {
        public readonly DialoguePriority Priority;
        public readonly float Cooldown;
        public readonly bool AllowShout;
        public readonly bool AllowRadio;

        public DialogueCategoryRule(DialoguePriority priority, float cooldown, bool allowShout, bool allowRadio)
        {
            Priority = priority;
            Cooldown = cooldown;
            AllowShout = allowShout;
            AllowRadio = allowRadio;
        }
    }

    public static class DialogueCategoryRules
    {
        public static DialogueCategoryRule Get(string category)
        {
            switch (category)
            {
                case DialogueCats.MagEmpty: return new DialogueCategoryRule(DialoguePriority.Normal, 6f, true, false);
                case DialogueCats.Reload: return new DialogueCategoryRule(DialoguePriority.Normal, 8f, true, true);
                case DialogueCats.ReloadLast: return new DialogueCategoryRule(DialoguePriority.High, 20f, true, true);
                case DialogueCats.AmmoRequest: return new DialogueCategoryRule(DialoguePriority.Normal, 15f, true, true);
                case DialogueCats.GrenadeThrow: return new DialogueCategoryRule(DialoguePriority.High, 3f, true, false);
                case DialogueCats.GrenadeIncoming: return new DialogueCategoryRule(DialoguePriority.Critical, 2f, true, false);
                case DialogueCats.Wounded: return new DialogueCategoryRule(DialoguePriority.High, 6f, true, true);
                case DialogueCats.HitSelf: return new DialogueCategoryRule(DialoguePriority.High, 5f, true, true);
                case DialogueCats.Bleeding: return new DialogueCategoryRule(DialoguePriority.Normal, 12f, true, true);
                case DialogueCats.Critical: return new DialogueCategoryRule(DialoguePriority.High, 10f, true, true);
                case DialogueCats.Medic: return new DialogueCategoryRule(DialoguePriority.High, 8f, true, true);
                case DialogueCats.ManDown: return new DialogueCategoryRule(DialoguePriority.High, 6f, true, true);
                case DialogueCats.SelfHeal: return new DialogueCategoryRule(DialoguePriority.Chatter, 10f, true, false);
                case DialogueCats.RevivedThanks: return new DialogueCategoryRule(DialoguePriority.Normal, 6f, true, true);
                case DialogueCats.Reviving: return new DialogueCategoryRule(DialoguePriority.Normal, 6f, true, false);
                case DialogueCats.EnemyDown:
                case DialogueCats.EnemyDownHeadshot:
                case DialogueCats.EnemyDownMulti: return new DialogueCategoryRule(DialoguePriority.Normal, 5f, true, true);
                case DialogueCats.CoverMe: return new DialogueCategoryRule(DialoguePriority.Normal, 7f, true, true);
                case DialogueCats.Covering: return new DialogueCategoryRule(DialoguePriority.Normal, 7f, true, false);
                case DialogueCats.Suppress: return new DialogueCategoryRule(DialoguePriority.Chatter, 9f, true, false);
                case DialogueCats.Advance: return new DialogueCategoryRule(DialoguePriority.Normal, 8f, true, true);
                case DialogueCats.FallBack: return new DialogueCategoryRule(DialoguePriority.Normal, 10f, true, true);
                case DialogueCats.Holding: return new DialogueCategoryRule(DialoguePriority.Chatter, 14f, true, true);
                case DialogueCats.Flank: return new DialogueCategoryRule(DialoguePriority.Normal, 10f, true, true);
                case DialogueCats.Clear: return new DialogueCategoryRule(DialoguePriority.Chatter, 12f, true, true);
                case DialogueCats.Smoke: return new DialogueCategoryRule(DialoguePriority.High, 6f, true, true);
                case DialogueCats.TakingFire: return new DialogueCategoryRule(DialoguePriority.High, 7f, true, true);
                case DialogueCats.SniperWarn: return new DialogueCategoryRule(DialoguePriority.High, 8f, true, true);
                case DialogueCats.FriendlyFire: return new DialogueCategoryRule(DialoguePriority.Critical, 4f, true, false);
                case DialogueCats.PartOpen:
                case DialogueCats.ContactFull: return new DialogueCategoryRule(DialoguePriority.High, 5f, true, true);
                case DialogueCats.ReportToLeader: return new DialogueCategoryRule(DialoguePriority.Chatter, 14f, false, true);
                case DialogueCats.LowHealth: return new DialogueCategoryRule(DialoguePriority.Normal, 14f, true, true);
                case DialogueCats.Watch:
                case DialogueCats.Breath: return new DialogueCategoryRule(DialoguePriority.Ambient, 20f, true, false);
                case DialogueCats.AckEnlisted:
                case DialogueCats.AckNco:
                case DialogueCats.AckPeer:
                case DialogueCats.AckSuperior:
                case DialogueCats.AckNegative: return new DialogueCategoryRule(DialoguePriority.High, 0.5f, true, true);
                default: return new DialogueCategoryRule(DialoguePriority.Normal, 8f, true, true);
            }
        }
    }
}
