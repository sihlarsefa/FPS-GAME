using System;
using System.Collections.Generic;
using System.IO;
using Project.Application.Replay;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Replay
{
    /// <summary>
    /// Maç tekrarı kaydedici: 10 Hz'de tüm savaşçıların konum/yön/duruş/silah/hayatta durumunu ve olayları
    /// (atış, isabet, ölüm, patlama, bölge) bellekte biriktirir; maç bitince ya da sahne kapanınca gzip'li ikili
    /// dosya olarak <c>persistentDataPath/Replays</c> altına yazar. Biçim: <see cref="ReplayCodec"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReplayRecorder : MonoBehaviour
    {
        public const string FolderName = "Replays";
        public const string Extension = ".hrpl";
        private const int MaxFilesKept = 20;
        private const int MaxFrames = 36000 * 2; // 2 saat @10Hz

        private readonly ReplayData _data = new ReplayData();
        private readonly HashSet<int> _known = new HashSet<int>();
        private IEventBus _bus;
        private IZoneService _zone;
        private float _start;
        private float _next;
        private bool _saved;
        private bool _subscribed;

        private Action<WeaponFiredEvent> _onFired;
        private Action<HitConfirmedEvent> _onHit;
        private Action<PlayerDiedEvent> _onDied;
        private Action<ExplosionEvent> _onExplosion;
        private Action<ZoneStageChangedEvent> _onZone;
        private Action<MatchEndedEvent> _onEnded;

        /// <summary>Kayıt klasörü (oluşturulmaz).</summary>
        public static string Folder => Path.Combine(UnityEngine.Application.persistentDataPath, FolderName);

        /// <summary>Verilen nesneye kaydediciyi ekler ve kaydı başlatır (yinelenen çağrı mevcut olanı döndürür).</summary>
        public static ReplayRecorder Attach(GameObject host, string mapId, int matchSeed, int worldSeed, int localPlayerId)
        {
            if (host == null)
                return null;
            var r = host.GetComponent<ReplayRecorder>();
            if (r == null)
                r = host.AddComponent<ReplayRecorder>();
            r.Begin(mapId, matchSeed, worldSeed, localPlayerId);
            return r;
        }

        /// <summary>Kaydedilen tekrar dosyalarını yeniden eskiye sıralı döndürür.</summary>
        public static List<string> ListFiles()
        {
            var list = new List<string>();
            try
            {
                var dir = Folder;
                if (Directory.Exists(dir))
                {
                    list.AddRange(Directory.GetFiles(dir, "*" + Extension));
                    list.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tekrar] Liste okunamadı: " + e.Message);
            }
            return list;
        }

        /// <summary>Dosyadan tekrarı yükler; hata olursa null.</summary>
        public static ReplayData Load(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                    return ReplayCodec.Read(fs);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tekrar] Yüklenemedi: " + e.Message);
                return null;
            }
        }

        private void Begin(string mapId, int matchSeed, int worldSeed, int localPlayerId)
        {
            if (_subscribed)
                return;
            _data.MapId = mapId ?? string.Empty;
            _data.MatchSeed = matchSeed;
            _data.WorldSeed = worldSeed;
            _data.LocalPlayerId = localPlayerId;
            _data.RecordedAtUtcTicks = DateTime.UtcNow.Ticks;
            _start = Time.time;
            _next = 0f;

            GameContextTryResolve();
            Subscribe();
            SampleFrame(0f);
        }

        private void GameContextTryResolve()
        {
            if (GameContext.TryGet<IEventBus>(out var bus))
                _bus = bus;
            if (GameContext.TryGet<IZoneService>(out var zone))
                _zone = zone;
        }

        private void Subscribe()
        {
            if (_bus == null)
                return;
            _onFired = e => Add(ReplayEventType.Shot, e.ShooterId.Value, -1, e.HasOrigin ? new Vector3(e.Origin.X, e.Origin.Y, e.Origin.Z) : PositionOf(e.ShooterId.Value), 0f, e.WeaponId, false);
            _onHit = e => Add(ReplayEventType.Hit, e.AttackerId.Value, e.VictimId.Value, PositionOf(e.VictimId.Value), e.Damage, null, e.IsHeadshot);
            _onDied = e => Add(ReplayEventType.Death, e.KillerId.Value, e.VictimId.Value, PositionOf(e.VictimId.Value), 0f, e.WeaponId, e.IsHeadshot);
            _onExplosion = e => Add(ReplayEventType.Explosion, e.AttackerId.Value, -1, new Vector3(e.Position.X, e.Position.Y, e.Position.Z), e.Radius, null, false);
            _onZone = e => Add(ReplayEventType.Zone, -1, -1, Vector3.zero, e.PhaseIndex, null, false);
            _onEnded = _ => Save();
            _bus.Subscribe(_onFired);
            _bus.Subscribe(_onHit);
            _bus.Subscribe(_onDied);
            _bus.Subscribe(_onExplosion);
            _bus.Subscribe(_onZone);
            _bus.Subscribe(_onEnded);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _bus == null)
                return;
            _bus.Unsubscribe(_onFired);
            _bus.Unsubscribe(_onHit);
            _bus.Unsubscribe(_onDied);
            _bus.Unsubscribe(_onExplosion);
            _bus.Unsubscribe(_onZone);
            _bus.Unsubscribe(_onEnded);
            _subscribed = false;
        }

        private static Vector3 PositionOf(int id)
        {
            return CombatantRegistry.TryGet(new PlayerId(id), out var c) ? c.transform.position : Vector3.zero;
        }

        private void Add(ReplayEventType type, int actor, int target, Vector3 pos, float value, string weapon, bool flag)
        {
            if (_saved || _data.Frames.Count >= MaxFrames)
                return;
            _data.Events.Add(new ReplayEvent
            {
                Time = Time.time - _start,
                Type = type,
                Actor = actor,
                Target = target,
                X = pos.x, Y = pos.y, Z = pos.z,
                Value = value,
                Weapon = _data.WeaponIndex(weapon),
                Flag = flag
            });
        }

        private void Update()
        {
            if (_saved || !_subscribed)
                return;
            var t = Time.time - _start;
            if (t < _next)
                return;
            SampleFrame(t);
            _next = t + _data.SampleInterval;
        }

        private void SampleFrame(float t)
        {
            if (_data.Frames.Count >= MaxFrames)
                return;

            var all = CombatantRegistry.All;
            var frame = new ReplayFrame { Time = t, Actors = new ReplayActorSample[all.Count] };
            if (_zone != null)
            {
                var z = _zone.CurrentZone;
                frame.ZoneX = z.CenterX;
                frame.ZoneZ = z.CenterZ;
                frame.ZoneRadius = z.Radius;
            }

            var n = 0;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsInitialized)
                    continue;
                var id = c.Id.Value;
                if (_known.Add(id))
                    _data.Players.Add(new ReplayPlayer { Id = id, Name = c.DisplayName ?? ("Asker " + id), Team = c.Team, IsBot = c.IsBot });

                var p = c.transform.position;
                var look = c.EyePoint != null ? c.EyePoint.eulerAngles : c.transform.eulerAngles;
                var pitch = look.x > 180f ? look.x - 360f : look.x;
                var inv = c.Inventory;
                var active = inv != null ? inv.ActiveWeapon : null;
                var def = active != null ? active.Definition : null;
                frame.Actors[n++] = new ReplayActorSample
                {
                    Id = id,
                    X = p.x, Y = p.y, Z = p.z,
                    Yaw = c.transform.eulerAngles.y,
                    Pitch = pitch,
                    Stance = (byte)c.Stance,
                    Weapon = def != null ? _data.WeaponIndex(def.WeaponId) : -1,
                    Alive = c.IsAlive
                };
            }

            if (n != frame.Actors.Length)
                Array.Resize(ref frame.Actors, n);
            _data.Frames.Add(frame);
        }

        /// <summary>Kaydı diske yazar (bir kez). Başarılıysa dosya yolunu, değilse null döndürür.</summary>
        public string Save()
        {
            if (_saved)
                return null;
            _saved = true;
            Unsubscribe();
            if (_data.Frames.Count < 2)
                return null;

            try
            {
                var dir = Folder;
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "tekrar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Extension);
                using (var fs = File.Create(path))
                    ReplayCodec.Write(_data, fs, true);
                Prune();
                Debug.Log("[Tekrar] Kaydedildi: " + path + " (" + _data.Frames.Count + " kare, " + _data.Events.Count + " olay).");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tekrar] Kaydedilemedi: " + e.Message);
                return null;
            }
        }

        private static void Prune()
        {
            var files = ListFiles();
            for (var i = MaxFilesKept; i < files.Count; i++)
            {
                try { File.Delete(files[i]); }
                catch { /* kilitli dosya: sonra denenir */ }
            }
        }

        private void OnApplicationQuit() => Save();
        private void OnDestroy() => Save();
    }
}
