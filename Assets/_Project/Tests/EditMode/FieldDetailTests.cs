using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class FieldDetailTests
    {
        private static TerrainModel Model(int seed)
        {
            return TerrainModel.Build(MapLayout.CreateKuzgunVadisi(seed), seed, 129);
        }

        [Test]
        public void Plan_IsDeterministicBySeed()
        {
            var a = FieldPlan.Build(Model(7));
            var b = FieldPlan.Build(Model(7));
            Assert.AreEqual(a.Parcels.Count, b.Parcels.Count);
            for (var i = 0; i < a.Parcels.Count; i++)
            {
                Assert.AreEqual(a.Parcels[i].Center, b.Parcels[i].Center);
                Assert.AreEqual(a.Parcels[i].Kind, b.Parcels[i].Kind);
                Assert.AreEqual(a.Parcels[i].WallMask, b.Parcels[i].WallMask);
            }
        }

        [Test]
        public void Plan_ParcelsAvoidRoadsAndStayInside()
        {
            var model = Model(11);
            var plan = FieldPlan.Build(model);
            Assert.Greater(plan.Parcels.Count, 0, "köy çevresinde tarla var");
            foreach (var p in plan.Parcels)
            {
                Assert.Greater(model.EdgeDistance(p.Center.x, p.Center.y), 20f);
                Assert.GreaterOrEqual(model.SampleRoadEdge(p.Center.x, p.Center.y), 6f, "parsel merkezi yolda değil");
                Assert.Greater(p.InsideDistance(p.Center.x, p.Center.y), 5f);
            }
        }

        [Test]
        public void Parcel_LocalWorldRoundTrip()
        {
            var p = new FieldParcel { Center = new Vector2(10f, -4f), HalfX = 20f, HalfZ = 12f, Yaw = 0.7f };
            p.Init();
            var w = p.ToWorld(5f, -3f);
            p.ToLocal(w.x, w.y, out var lx, out var lz);
            Assert.AreEqual(5f, lx, 1e-3f);
            Assert.AreEqual(-3f, lz, 1e-3f);
        }

        [Test]
        public void RowRidge_AlternatesWithSpacing()
        {
            Assert.Greater(FieldDetailRules.RowRidge(FieldPlan.RowSpacing * 0.25f), 0.95f);
            Assert.Less(FieldDetailRules.RowRidge(FieldPlan.RowSpacing * 0.75f), 0.05f);
        }

        [Test]
        public void DirtRoadWear_GrassStripOnlyAtCenter()
        {
            FieldDetailRules.DirtRoadWear(-2.0f, 2.2f, 1f, out var centerGrass, out _);
            FieldDetailRules.DirtRoadWear(-0.2f, 2.2f, 1f, out var edgeGrass, out _);
            Assert.Greater(centerGrass, 0.5f);
            Assert.Less(edgeGrass, 0.05f);
        }

        [Test]
        public void ModulateDensity_PlowedClearsGrassStubbleAddsStubble()
        {
            var d = new float[VegetationPainter.TotalKindCount];
            d[(int)DetailKind.Grass] = 1f;
            FieldDetailRules.ModulateDensity(d, 1f, FieldKind.Plowed, 1f, 0f, float.PositiveInfinity, 0.5f);
            Assert.AreEqual(0f, d[(int)DetailKind.Grass], 1e-4f);
            Assert.AreEqual(0f, d[(int)DetailKind.Stubble], 1e-4f);

            d[(int)DetailKind.Grass] = 1f;
            FieldDetailRules.ModulateDensity(d, 1f, FieldKind.Stubble, 1f, 0f, float.PositiveInfinity, 0.5f);
            Assert.AreEqual(0f, d[(int)DetailKind.Grass], 1e-4f);
            Assert.Greater(d[(int)DetailKind.Stubble], 0.5f);
        }

        [Test]
        public void ModulateDensity_FlowersOnlyOffPathAndUntrampled()
        {
            var d = new float[VegetationPainter.TotalKindCount];
            d[(int)DetailKind.FlowerWhite] = 1f;
            FieldDetailRules.ModulateDensity(d, 0f, FieldKind.Fallow, 1f, 0f, 0.5f, 0.9f);
            Assert.AreEqual(0f, d[(int)DetailKind.FlowerWhite], 1e-4f, "yol kenarında çiçek yok");

            d[(int)DetailKind.FlowerWhite] = 1f;
            FieldDetailRules.ModulateDensity(d, 0f, FieldKind.Fallow, 1f, 1f, 40f, 0.9f);
            Assert.AreEqual(0f, d[(int)DetailKind.FlowerWhite], 1e-4f, "ezilmiş çimde çiçek yok");

            d[(int)DetailKind.FlowerWhite] = 1f;
            FieldDetailRules.ModulateDensity(d, 0f, FieldKind.Fallow, 1f, 0f, 40f, 0.9f);
            Assert.AreEqual(1f, d[(int)DetailKind.FlowerWhite], 1e-4f);
        }

        [Test]
        public void Trample_FadesOutwardFromSettlement()
        {
            var plan = new FieldPlan();
            plan.Trample.Add(new TrampleCircle { Center = Vector2.zero, InnerRadius = 40f, Width = 26f });
            Assert.AreEqual(1f, plan.TrampleAt(30f, 0f), 1e-3f);
            Assert.AreEqual(0f, plan.TrampleAt(100f, 0f), 1e-3f);
        }

        [Test]
        public void ColorBreakup_IsBounded()
        {
            Assert.LessOrEqual(Mathf.Abs(FieldDetailRules.ColorBreakup(5f, -5f)), 0.23f);
        }
    }
}
