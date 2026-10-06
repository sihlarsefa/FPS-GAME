using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Audio
{
    /// <summary>Telsiz konu sırası: büyük değer önce konuşur (temas > yaralı > bomba > hareket > durum).</summary>
    public enum RadioTopic
    {
        Status = 0,
        Movement = 1,
        Bomb = 2,
        Wounded = 3,
        Contact = 4
    }

    /// <summary>
    /// Telsiz disiplini (saf mantık): konu sırası, kategori başına en az 8 sn tekrar aralığı, tim komutanı önceliği,
    /// hat meşgulken kısa kuyruk (+ "hat meşgul" bip sınırı) ve 200 m üstü çağrı işareti.
    /// </summary>
    public static class RadioDisciplineRules
    {
        public const float MinRepeatSeconds = 8f;
        public const float FarCallsignMeters = 200f;
        public const float QueueMaxAgeSeconds = 5f;
        public const int QueueCapacity = 3;
        public const float BusyBeepGap = 2.5f;
        public const string CallsignBase = "Kuzgun";

        public static RadioTopic TopicOf(string category)
        {
            if (string.IsNullOrEmpty(category))
                return RadioTopic.Status;
            if (category.StartsWith("contact", StringComparison.Ordinal) || category == "wiped")
                return RadioTopic.Contact;
            if (category.StartsWith("wound", StringComparison.Ordinal) || category == "allydown")
                return RadioTopic.Wounded;
            if (category.StartsWith("arty", StringComparison.Ordinal) || category.StartsWith("grenade", StringComparison.Ordinal))
                return RadioTopic.Bomb;
            if (category == "order" || category == "command" || category == "zone")
                return RadioTopic.Movement;
            return RadioTopic.Status;
        }

        /// <summary>Konunun taban önceliği (temas araya girer); çağıranın verdiği öncelikten asla düşük değil. Komutan +1 (üst sınır Critical).</summary>
        public static RadioPriority PriorityFor(RadioTopic topic, RadioPriority requested, bool commander)
        {
            RadioPriority floor;
            switch (topic)
            {
                case RadioTopic.Contact: floor = RadioPriority.High; break;
                case RadioTopic.Wounded:
                case RadioTopic.Bomb: floor = RadioPriority.Normal; break;
                default: floor = RadioPriority.Low; break;
            }

            var p = (RadioPriority)Math.Max((int)floor, (int)requested);
            if (commander && p < RadioPriority.Critical)
                p = (RadioPriority)((int)p + 1);
            return p;
        }

        /// <summary>Aynı kategori en az 8 sn sonra tekrar eder; oyuncunun emir onayı (order) kısa kalır.</summary>
        public static float EffectiveCooldown(string category, float requested) =>
            category == "order" ? requested : Math.Max(requested, MinRepeatSeconds);

        public static bool NeedsCallsign(float distanceMeters) => distanceMeters > FarCallsignMeters;

        public static string Callsign(int number) => CallsignBase + "-" + Math.Max(1, number);

        /// <summary>"Kuzgun-2, Kuzgun-1... metin" — önce çağrılan, sonra çağıran.</summary>
        public static string WithCallsign(string text, int callerNumber, int calledNumber) =>
            Callsign(calledNumber) + ", " + Callsign(callerNumber) + "... " + (text ?? string.Empty);

        public static bool ShouldBusyBeep(float now, float lastBeepAt) => now - lastBeepAt >= BusyBeepGap;

        /// <summary>Kuyruk sıralaması: yüksek öncelik, sonra komutan, sonra konu, sonra eski olan.</summary>
        public static int Compare(RadioQueued a, RadioQueued b)
        {
            var c = ((int)b.Priority).CompareTo((int)a.Priority);
            if (c != 0) return c;
            c = b.Commander.CompareTo(a.Commander);
            if (c != 0) return c;
            c = ((int)b.Topic).CompareTo((int)a.Topic);
            if (c != 0) return c;
            return a.EnqueuedAt.CompareTo(b.EnqueuedAt);
        }
    }

    /// <summary>Kuyruktaki konuşma isteğinin sıralama alanları.</summary>
    public readonly struct RadioQueued
    {
        public readonly RadioPriority Priority;
        public readonly RadioTopic Topic;
        public readonly bool Commander;
        public readonly float EnqueuedAt;

        public RadioQueued(RadioPriority priority, RadioTopic topic, bool commander, float enqueuedAt)
        {
            Priority = priority;
            Topic = topic;
            Commander = commander;
            EnqueuedAt = enqueuedAt;
        }
    }

    /// <summary>Hat meşgulken bekleyen en çok 3 istek; 5 sn'den eskiler düşer, dolunca en zayıf atılır.</summary>
    public sealed class RadioDisciplineQueue<T>
    {
        private readonly List<KeyValuePair<RadioQueued, T>> _items = new List<KeyValuePair<RadioQueued, T>>(RadioDisciplineRules.QueueCapacity + 1);

        public int Count => _items.Count;

        /// <summary>Kabul edildiyse true (dolu ve en zayıftan da zayıfsa false).</summary>
        public bool Enqueue(RadioQueued meta, T payload)
        {
            Prune(meta.EnqueuedAt);
            _items.Add(new KeyValuePair<RadioQueued, T>(meta, payload));
            _items.Sort((a, b) => RadioDisciplineRules.Compare(a.Key, b.Key));
            if (_items.Count <= RadioDisciplineRules.QueueCapacity)
                return true;

            var dropped = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            return !ReferenceEquals(dropped.Value, payload) && !EqualityComparer<T>.Default.Equals(dropped.Value, payload);
        }

        public bool TryDequeue(float now, out T payload)
        {
            Prune(now);
            if (_items.Count == 0)
            {
                payload = default;
                return false;
            }

            payload = _items[0].Value;
            _items.RemoveAt(0);
            return true;
        }

        public void RemoveWhere(Predicate<T> match) => _items.RemoveAll(i => match(i.Value));

        public void Clear() => _items.Clear();

        private void Prune(float now) =>
            _items.RemoveAll(i => now - i.Key.EnqueuedAt > RadioDisciplineRules.QueueMaxAgeSeconds);
    }
}
