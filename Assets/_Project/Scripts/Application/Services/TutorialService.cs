using System;
using System.Collections.Generic;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Bir adım koşulu: sinyal anahtarı (olay/sinyal adı), isteğe bağlı nitelik ve gereken sayı.</summary>
    public sealed class TutorialCondition
    {
        public string Key = string.Empty;
        public string Qualifier = string.Empty;
        public int Count = 1;
        /// <summary>-1: fark etmez, 0: öldürme olmamalı, 1: öldürme olmalı.</summary>
        public int IsKill = -1;
        public float MinRadius;
        /// <summary>-1: fark etmez, 0: çağrı/uyarı aşaması, 1: isabet aşaması.</summary>
        public int IsImpact = -1;
        /// <summary>Sinyalin taşıdığı süre (sn) en az bu kadar olmalı.</summary>
        public float MinSeconds;
        public bool WhileAiming;
        public bool WhileScoped;
        public float MinZoom;
    }

    /// <summary>Sinyalle birlikte gelen süzgeç verisi.</summary>
    public readonly struct TutorialSignalData
    {
        public readonly bool HasKill;
        public readonly bool IsKill;
        public readonly bool HasRadius;
        public readonly float Radius;
        public readonly bool HasImpact;
        public readonly bool IsImpact;
        public readonly float Seconds;

        public TutorialSignalData(bool hasKill, bool isKill, bool hasRadius, float radius, bool hasImpact, bool isImpact, float seconds)
        {
            HasKill = hasKill; IsKill = isKill; HasRadius = hasRadius; Radius = radius;
            HasImpact = hasImpact; IsImpact = isImpact; Seconds = seconds;
        }

        public static TutorialSignalData Kill(bool isKill) => new TutorialSignalData(true, isKill, false, 0f, false, false, 0f);
        public static TutorialSignalData Blast(float radius) => new TutorialSignalData(false, false, true, radius, false, false, 0f);
        public static TutorialSignalData Strike(bool isImpact) => new TutorialSignalData(false, false, false, 0f, true, isImpact, 0f);
        public static TutorialSignalData Held(float seconds) => new TutorialSignalData(false, false, false, 0f, false, false, seconds);
    }

    /// <summary>Eğitim adımı tanımı (JSON'dan yüklenir).</summary>
    public sealed class TutorialStepDef
    {
        public string Id = string.Empty;
        public string Goal = string.Empty;
        public string ScreenText = string.Empty;
        public string Hint = string.Empty;
        public float TimeLimitSeconds;
        public bool AnyOf;
        public int RewardXp;
        public List<TutorialCondition> Conditions = new List<TutorialCondition>();
    }

    /// <summary>
    /// Saf adım durum makinesi: oyun olayları (IEventBus) ve sunum sinyalleri (<see cref="Signal"/>) ile ilerler.
    /// Süre dolarsa adım ödülsüz geçilir; oyuncu adımı atlayabilir.
    /// </summary>
    public sealed class TutorialService : IDisposable
    {
        private readonly IReadOnlyList<TutorialStepDef> _steps;
        private readonly IEventBus _bus;
        private int[] _counts = Array.Empty<int>();
        private bool _started;
        private bool _disposed;

        public TutorialService(IReadOnlyList<TutorialStepDef> steps, IEventBus bus = null)
        {
            _steps = steps ?? Array.Empty<TutorialStepDef>();
            _bus = bus;
            if (_bus == null)
                return;
            _bus.Subscribe<WeaponFiredEvent>(OnFired);
            _bus.Subscribe<HitConfirmedEvent>(OnHit);
            _bus.Subscribe<WeaponReloadStartedEvent>(OnReloadStarted);
            _bus.Subscribe<WeaponReloadedEvent>(OnReloaded);
            _bus.Subscribe<ExplosionEvent>(OnExplosion);
            _bus.Subscribe<ItemUsedEvent>(OnItemUsed);
            _bus.Subscribe<LootPickedUpEvent>(OnLoot);
            _bus.Subscribe<SquadOrderIssuedEvent>(OnOrder);
            _bus.Subscribe<ArtilleryStrikeEvent>(OnArtillery);
        }

        /// <summary>Nişan alıyor mu (WhileAiming süzgeci için); boşsa süzgeç geçilemez.</summary>
        public Func<bool> AimingProbe;
        /// <summary>Dürbünde mi (WhileScoped süzgeci için).</summary>
        public Func<bool> ScopedProbe;
        /// <summary>Dürbün yakınlaştırması (MinZoom süzgeci için).</summary>
        public Func<float> ZoomProbe;

        public event Action<int, TutorialStepDef> StepStarted;
        /// <summary>(indeks, adım, ödül kazanıldı mı).</summary>
        public event Action<int, TutorialStepDef, bool> StepFinished;
        public event Action Completed;

        public int StepCount => _steps.Count;
        public int CurrentIndex { get; private set; } = -1;
        public bool IsRunning => _started && !IsCompleted;
        public bool IsCompleted { get; private set; }
        public float StepElapsed { get; private set; }
        public int TotalXp { get; private set; }
        public int CompletedSteps { get; private set; }
        public TutorialStepDef CurrentStep => CurrentIndex >= 0 && CurrentIndex < _steps.Count ? _steps[CurrentIndex] : null;

        /// <summary>0..1 genel ilerleme.</summary>
        public float Progress => _steps.Count == 0 ? 1f : Math.Min(1f, (float)Math.Max(0, CurrentIndex) / _steps.Count);

        public void Start()
        {
            if (_started || _disposed)
                return;
            _started = true;
            if (_steps.Count == 0)
            {
                Finish();
                return;
            }
            BeginStep(0);
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f)
                return;
            StepElapsed += deltaSeconds;
            var step = CurrentStep;
            if (step != null && step.TimeLimitSeconds > 0f && StepElapsed >= step.TimeLimitSeconds)
                EndStep(false);
        }

        /// <summary>Geçerli adımı ödülsüz atlar.</summary>
        public void SkipStep()
        {
            if (IsRunning)
                EndStep(false);
        }

        /// <summary>Tüm eğitimi bitirir.</summary>
        public void SkipAll()
        {
            if (!_started)
                _started = true;
            if (!IsCompleted)
                Finish();
        }

        public void Signal(string key, string qualifier = null) => Signal(key, qualifier, default);

        public void Signal(string key, string qualifier, TutorialSignalData data)
        {
            if (!IsRunning || string.IsNullOrEmpty(key))
                return;
            var step = CurrentStep;
            for (var i = 0; i < step.Conditions.Count; i++)
            {
                var c = step.Conditions[i];
                if (!string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrEmpty(c.Qualifier)
                    && !string.Equals(c.Qualifier, qualifier, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!PassesFilters(c, data))
                    continue;
                _counts[i]++;
            }
            if (IsStepSatisfied(step))
                EndStep(true);
        }

        private bool PassesFilters(TutorialCondition c, TutorialSignalData d)
        {
            if (c.IsKill >= 0 && (!d.HasKill || d.IsKill != (c.IsKill == 1)))
                return false;
            if (c.IsImpact >= 0 && (!d.HasImpact || d.IsImpact != (c.IsImpact == 1)))
                return false;
            if (c.MinRadius > 0f && (!d.HasRadius || d.Radius < c.MinRadius))
                return false;
            if (c.MinSeconds > 0f && d.Seconds < c.MinSeconds)
                return false;
            if (c.WhileAiming && !(AimingProbe != null && AimingProbe()))
                return false;
            if (c.WhileScoped && !(ScopedProbe != null && ScopedProbe()))
                return false;
            if (c.MinZoom > 0f && !(ZoomProbe != null && ZoomProbe() >= c.MinZoom))
                return false;
            return true;
        }

        /// <summary>Geçerli adımın sinyal bekleyen anahtarı bu mu (sunum sinyalleri için ucuz süzgeç).</summary>
        public bool Wants(string key)
        {
            var step = CurrentStep;
            if (!IsRunning || step == null)
                return false;
            foreach (var c in step.Conditions)
                if (string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private bool IsStepSatisfied(TutorialStepDef step)
        {
            if (step.Conditions.Count == 0)
                return false;
            for (var i = 0; i < step.Conditions.Count; i++)
            {
                var ok = _counts[i] >= Math.Max(1, step.Conditions[i].Count);
                if (step.AnyOf && ok)
                    return true;
                if (!step.AnyOf && !ok)
                    return false;
            }
            return !step.AnyOf;
        }

        private void BeginStep(int index)
        {
            CurrentIndex = index;
            StepElapsed = 0f;
            var step = _steps[index];
            _counts = new int[step.Conditions.Count];
            StepStarted?.Invoke(index, step);
        }

        private void EndStep(bool success)
        {
            var step = CurrentStep;
            var index = CurrentIndex;
            if (success)
            {
                CompletedSteps++;
                TotalXp += Math.Max(0, step.RewardXp);
            }
            StepFinished?.Invoke(index, step, success);
            if (index + 1 >= _steps.Count)
                Finish();
            else
                BeginStep(index + 1);
        }

        private void Finish()
        {
            IsCompleted = true;
            CurrentIndex = _steps.Count;
            Completed?.Invoke();
        }

        private void OnFired(WeaponFiredEvent e) => Signal("WeaponFiredEvent");
        private void OnHit(HitConfirmedEvent e) => Signal("HitConfirmedEvent", null, TutorialSignalData.Kill(e.IsKill));
        private void OnReloadStarted(WeaponReloadStartedEvent e) => Signal("WeaponReloadStartedEvent");
        private void OnReloaded(WeaponReloadedEvent e) => Signal("WeaponReloadedEvent");
        private void OnExplosion(ExplosionEvent e) => Signal("ExplosionEvent", null, TutorialSignalData.Blast(e.Radius));
        private void OnLoot(LootPickedUpEvent e) => Signal("LootPickedUpEvent");
        private void OnOrder(SquadOrderIssuedEvent e) => Signal("SquadOrderIssuedEvent", e.Order.ToString());
        private void OnArtillery(ArtilleryStrikeEvent e) => Signal("ArtilleryStrikeEvent", null, TutorialSignalData.Strike(e.IsImpact));

        private void OnItemUsed(ItemUsedEvent e)
        {
            if (e.Started)
                Signal("ItemUsedEvent", e.ItemId + ":started");
            if (e.Completed)
                Signal("ItemUsedEvent", e.ItemId + ":completed");
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_bus == null)
                return;
            _bus.Unsubscribe<WeaponFiredEvent>(OnFired);
            _bus.Unsubscribe<HitConfirmedEvent>(OnHit);
            _bus.Unsubscribe<WeaponReloadStartedEvent>(OnReloadStarted);
            _bus.Unsubscribe<WeaponReloadedEvent>(OnReloaded);
            _bus.Unsubscribe<ExplosionEvent>(OnExplosion);
            _bus.Unsubscribe<ItemUsedEvent>(OnItemUsed);
            _bus.Unsubscribe<LootPickedUpEvent>(OnLoot);
            _bus.Unsubscribe<SquadOrderIssuedEvent>(OnOrder);
            _bus.Unsubscribe<ArtilleryStrikeEvent>(OnArtillery);
        }
    }
}
