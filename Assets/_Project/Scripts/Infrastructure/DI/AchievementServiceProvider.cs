using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.DI
{
    /// <summary>
    /// Paylaşılan <see cref="AchievementService"/> örneği: Resources/Progression/achievements.json okunur, ilerleme
    /// verilen depoya yazılır. GameSession ve kompozisyon kökü aynı örneği kullanır.
    /// </summary>
    public static class AchievementServiceProvider
    {
        [Serializable]
        private sealed class Root
        {
            public List<AchievementDefinition> achievements;
        }

        private static AchievementService _shared;

        public static AchievementService Shared => _shared;

        public static AchievementService GetOrCreate(ISettingsStore store)
        {
            if (_shared != null)
                return _shared;

            var service = new AchievementService(store, LoadDefinitions());
            try { service.Load(); }
            catch (Exception e) { Debug.LogException(e); }
            _shared = service;
            return service;
        }

        public static void ResetShared() => _shared = null;

        private static List<AchievementDefinition> LoadDefinitions()
        {
            try
            {
                var asset = Resources.Load<TextAsset>("Progression/achievements");
                if (asset == null)
                    return new List<AchievementDefinition>();
                var root = JsonUtility.FromJson<Root>(asset.text);
                return root?.achievements ?? new List<AchievementDefinition>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new List<AchievementDefinition>();
            }
        }
    }
}
