using NUnit.Framework;
using Project.Application.Services;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    public sealed class SoldierPreviewLogicTests
    {
        [Test]
        public void ClampZoom_StaysInRange()
        {
            Assert.AreEqual(0.8f, SoldierPreviewLogic.ClampZoom(0.1f), 1e-4f);
            Assert.AreEqual(2.5f, SoldierPreviewLogic.ClampZoom(9f), 1e-4f);
            Assert.AreEqual(1f, SoldierPreviewLogic.ClampZoom(float.NaN), 1e-4f);
        }

        [Test]
        public void ApplyScroll_ZoomsInAndClamps()
        {
            Assert.Greater(SoldierPreviewLogic.ApplyScroll(1f, 1f), 1f);
            Assert.Less(SoldierPreviewLogic.ApplyScroll(1f, -1f), 1f);
            Assert.AreEqual(2.5f, SoldierPreviewLogic.ApplyScroll(2.4f, 100f), 1e-4f);
            Assert.AreEqual(0.8f, SoldierPreviewLogic.ApplyScroll(0.9f, -100f), 1e-4f);
        }

        [Test]
        public void CameraDistance_ShrinksWithZoom()
        {
            Assert.AreEqual(2.5f, SoldierPreviewLogic.CameraDistance(5f, 2f), 1e-4f);
        }

        [Test]
        public void QuickCamoIds_FiltersSlotDedupesAndCaps()
        {
            var items = new[]
            {
                new CosmeticDefinition { id = "camo_a", slot = CosmeticsService.SlotCamo },
                new CosmeticDefinition { id = "beret_x", slot = CosmeticsService.SlotBeret },
                new CosmeticDefinition { id = "camo_a", slot = CosmeticsService.SlotCamo },
                new CosmeticDefinition { id = "camo_b", slot = CosmeticsService.SlotCamo },
                new CosmeticDefinition { id = "camo_c", slot = CosmeticsService.SlotCamo }
            };
            var ids = SoldierPreviewLogic.QuickCamoIds(items, 2);
            CollectionAssert.AreEqual(new[] { "camo_a", "camo_b" }, ids);
            Assert.AreEqual(0, SoldierPreviewLogic.QuickCamoIds(null, 3).Count);
        }
    }
}
