using System;
using System.Collections.Generic;

namespace Project.Application.Dialogue
{
    public readonly struct DialogueRequest
    {
        public readonly string LineId;
        public readonly string Category;
        public readonly DialogueChannel Channel;
        public readonly DialoguePriority Priority;
        public readonly float Duration;
        public readonly int SpeakerId;
        public readonly float Cooldown;
        public readonly object Payload;

        public DialogueRequest(string lineId, string category, DialogueChannel channel, DialoguePriority priority, float duration,
            int speakerId, float cooldown, object payload = null)
        {
            LineId = lineId;
            Category = category;
            Channel = channel;
            Priority = priority;
            Duration = duration;
            SpeakerId = speakerId;
            Cooldown = cooldown;
            Payload = payload;
        }
    }

    public enum ArbiterResult
    {
        Rejected = 0,
        Play = 1,
        Queued = 2
    }

    /// <summary>
    /// Replik hakemi: öncelik + kanal kapasitesi (telsiz 1 hat, bağırma 2) + kategori/konuşmacı beklemesi + sıra.
    /// High ve Critical replikler KESİLMEZ; yalnız daha düşük öncelikli (Normal ve altı) replikler araya girilerek
    /// kesilebilir. Meşgulse High+ sıraya alınır (süresi dolmadan çalar). Ducking hedefi aktif repliklerden gelir.
    /// Saf mantık (zaman dışarıdan verilir).
    /// </summary>
    public sealed class DialogueArbiter
    {
        public const int RadioSlots = 1;
        public const int ShoutSlots = 2;
        public const int QueueCapacity = 4;
        public const float QueueMaxWait = 3f;
        public const float ChatterQuietGap = 3f;
        public const float NormalQuietGap = 0.5f;

        private struct Active
        {
            public DialogueRequest Request;
            public float EndAt;
        }

        private struct Queued
        {
            public DialogueRequest Request;
            public float ExpireAt;
        }

        private readonly List<Active> _active = new List<Active>(4);
        private readonly List<Queued> _queue = new List<Queued>(4);
        private readonly Dictionary<string, float> _categoryReady = new Dictionary<string, float>(16);
        private float _lastRadioEnd = -999f;

        /// <summary>Son çağrıda araya girilip kesilen repliklerin konuşmacı kimlikleri (çalmayı durdurmak için).</summary>
        public readonly List<int> Preempted = new List<int>(2);

        public int ActiveCount => _active.Count;
        public int QueuedCount => _queue.Count;

        public static bool IsUncuttable(DialoguePriority p) => p >= DialoguePriority.High;

        public ArbiterResult Request(float now, DialogueRequest request)
        {
            Preempted.Clear();
            Prune(now);

            if (!string.IsNullOrEmpty(request.Category) && _categoryReady.TryGetValue(request.Category, out var ready) && now < ready)
                return ArbiterResult.Rejected;

            var result = TryPlace(now, request, true);
            if (result == ArbiterResult.Play)
            {
                Commit(now, request);
                return ArbiterResult.Play;
            }

            if (result == ArbiterResult.Queued && request.Priority >= DialoguePriority.High && _queue.Count < QueueCapacity && !QueueHasCategory(request.Category))
            {
                _queue.Add(new Queued { Request = request, ExpireAt = now + QueueMaxWait });
                return ArbiterResult.Queued;
            }

            return ArbiterResult.Rejected;
        }

        /// <summary>Sıradaki replik oynatılabilirse true (en yüksek öncelik önce).</summary>
        public bool TryDequeue(float now, out DialogueRequest next)
        {
            Preempted.Clear();
            Prune(now);
            next = default;
            var best = -1;
            for (var i = 0; i < _queue.Count; i++)
            {
                if (best < 0 || _queue[i].Request.Priority > _queue[best].Request.Priority)
                    best = i;
            }

            if (best < 0)
                return false;

            var req = _queue[best].Request;
            if (TryPlace(now, req, false) != ArbiterResult.Play)
                return false;

            _queue.RemoveAt(best);
            Commit(now, req);
            next = req;
            return true;
        }

        /// <summary>Aktif repliklerden ducking hedef kazancı (1 = kısma yok).</summary>
        public float DuckTarget(float now)
        {
            var target = 1f;
            for (var i = 0; i < _active.Count; i++)
            {
                if (now >= _active[i].EndAt)
                    continue;
                var g = DuckFor(_active[i].Request.Priority);
                if (g < target)
                    target = g;
            }

            return target;
        }

        public static float DuckFor(DialoguePriority p)
        {
            switch (p)
            {
                case DialoguePriority.Critical: return 0.35f;
                case DialoguePriority.High: return 0.55f;
                case DialoguePriority.Normal: return 0.8f;
                default: return 1f;
            }
        }

        public bool IsSpeaking(int speakerId, float now)
        {
            for (var i = 0; i < _active.Count; i++)
                if (_active[i].Request.SpeakerId == speakerId && now < _active[i].EndAt)
                    return true;
            return false;
        }

        public bool ChannelBusy(DialogueChannel channel, float now)
        {
            var count = 0;
            for (var i = 0; i < _active.Count; i++)
                if (_active[i].Request.Channel == channel && now < _active[i].EndAt)
                    count++;
            return count >= Slots(channel);
        }

        /// <summary>Dış sistem (eski telsiz) hattı işgal ettiğinde.</summary>
        public void Reset()
        {
            _active.Clear();
            _queue.Clear();
            _categoryReady.Clear();
            Preempted.Clear();
            _lastRadioEnd = -999f;
        }

        private static int Slots(DialogueChannel c) => c == DialogueChannel.Radio ? RadioSlots : ShoutSlots;

        private ArbiterResult TryPlace(float now, DialogueRequest req, bool applyPreempt)
        {
            // Aynı asker aynı anda iki şey söylemez: kendi mevcut cümlesi kesilemezse bekler.
            for (var i = 0; i < _active.Count; i++)
            {
                if (_active[i].Request.SpeakerId != req.SpeakerId || now >= _active[i].EndAt)
                    continue;
                if (req.Priority > _active[i].Request.Priority && !IsUncuttable(_active[i].Request.Priority))
                {
                    if (applyPreempt)
                    {
                        Preempted.Add(_active[i].Request.SpeakerId);
                        _active.RemoveAt(i);
                    }

                    break;
                }

                return ArbiterResult.Queued;
            }

            if (req.Channel == DialogueChannel.Radio)
            {
                if (now >= ActiveRadioEnd(now))
                {
                    var gap = req.Priority <= DialoguePriority.Chatter ? ChatterQuietGap : req.Priority == DialoguePriority.Normal ? NormalQuietGap : 0f;
                    if (now < _lastRadioEnd + gap)
                        return req.Priority >= DialoguePriority.High ? ArbiterResult.Queued : ArbiterResult.Rejected;
                }
            }

            var used = 0;
            var lowest = -1;
            for (var i = 0; i < _active.Count; i++)
            {
                if (_active[i].Request.Channel != req.Channel || now >= _active[i].EndAt)
                    continue;
                used++;
                if (lowest < 0 || _active[i].Request.Priority < _active[lowest].Request.Priority)
                    lowest = i;
            }

            if (used < Slots(req.Channel))
                return ArbiterResult.Play;

            if (lowest >= 0 && req.Priority > _active[lowest].Request.Priority && !IsUncuttable(_active[lowest].Request.Priority))
            {
                if (applyPreempt)
                {
                    Preempted.Add(_active[lowest].Request.SpeakerId);
                    _active.RemoveAt(lowest);
                }

                return ArbiterResult.Play;
            }

            return req.Priority >= DialoguePriority.High ? ArbiterResult.Queued : ArbiterResult.Rejected;
        }

        private float ActiveRadioEnd(float now)
        {
            var end = -999f;
            for (var i = 0; i < _active.Count; i++)
                if (_active[i].Request.Channel == DialogueChannel.Radio && _active[i].EndAt > end)
                    end = _active[i].EndAt;
            return end;
        }

        private void Commit(float now, DialogueRequest req)
        {
            _active.Add(new Active { Request = req, EndAt = now + Math.Max(0.2f, req.Duration) });
            if (!string.IsNullOrEmpty(req.Category))
                _categoryReady[req.Category] = now + Math.Max(0f, req.Cooldown);
        }

        private bool QueueHasCategory(string category)
        {
            for (var i = 0; i < _queue.Count; i++)
                if (_queue[i].Request.Category == category)
                    return true;
            return false;
        }

        private void Prune(float now)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                if (now < _active[i].EndAt)
                    continue;
                if (_active[i].Request.Channel == DialogueChannel.Radio && _active[i].EndAt > _lastRadioEnd)
                    _lastRadioEnd = _active[i].EndAt;
                _active.RemoveAt(i);
            }

            for (var i = _queue.Count - 1; i >= 0; i--)
                if (now >= _queue[i].ExpireAt)
                    _queue.RemoveAt(i);
        }
    }

    /// <summary>Ducking yumuşatıcı: hızlı kısma (saldırı), yavaş geri dönüş (bırakma).</summary>
    public sealed class DuckSmoother
    {
        public const float AttackSeconds = 0.08f;
        public const float ReleaseSeconds = 0.6f;

        public float Gain { get; private set; } = 1f;

        public float Step(float dt, float target)
        {
            if (dt <= 0f)
                return Gain;
            var tau = target < Gain ? AttackSeconds : ReleaseSeconds;
            var k = 1f - (float)Math.Exp(-dt / tau);
            Gain += (target - Gain) * k;
            return Gain;
        }
    }
}
