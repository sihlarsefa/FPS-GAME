using NUnit.Framework;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Tests.EditMode.Sim;

namespace Project.Tests.EditMode
{
    public sealed class SelectedMapSettingsTests
    {
        [Test]
        public void Default_IsKuzgun() => Assert.AreEqual(MapCatalog.Kuzgun, new SettingsService(null).Current.SelectedMap);

        [Test]
        public void SelectedMap_RoundTripsThroughStore()
        {
            var store = new SimMemoryStore();
            new SettingsService(store).Modify(s => s.SelectedMap = MapCatalog.AyazGecidi);
            var reader = new SettingsService(store);
            reader.Load();
            Assert.AreEqual(MapCatalog.AyazGecidi, reader.Current.SelectedMap);
        }

        [Test]
        public void SelectedMap_InvalidFallsBackToKuzgun()
        {
            var s = SettingsService.Sanitize(new GameSettings { SelectedMap = "yok" });
            Assert.AreEqual(MapCatalog.Kuzgun, s.SelectedMap);
        }
    }
}
