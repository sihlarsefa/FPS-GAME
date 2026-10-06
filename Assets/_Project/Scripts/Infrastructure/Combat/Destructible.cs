using System.Collections.Generic;
using Project.Application.Services;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Yıkılabilir dünya parçası (pencere camı, çit, sandık, kapı). Mermi / patlama / hızlı araç çarpmasıyla HP düşer;
    /// HP bitince collider ve görsel kaldırılır, havuzlu enkaz parçaları saçılır ve kısa sürede kaybolur.
    /// Hasar hesapları <see cref="DestructionRules"/>'ta. NavMesh: bake'ten sonra kırılan parçalar harita yapısını
    /// bozmaz (NavMeshObstacle eklenmez); enkaz IgnoreRaycast katmanında olduğundan oyuncu/bot hareketini ve mermiyi etkilemez.
    /// </summary>
    public sealed class Destructible : MonoBehaviour
    {
        private const float PlayerHearDistance = 60f;

        private DestructibleKind _kind;
        private MaterialId _material;
        private float _hp = 1f;
        private bool _broken;

        private static readonly Collider[] OverlapBuffer = new Collider[64];

        public DestructibleKind Kind => _kind;
        public bool IsBroken => _broken;
        public float Hp => _hp;

        /// <summary>Nesneye Destructible ekler (varsa günceller). hp &lt;= 0 → tür varsayılanı.</summary>
        public static Destructible Mark(GameObject go, DestructibleKind kind, MaterialId material, float hp = 0f)
        {
            if (go == null)
                return null;
            var d = go.GetComponent<Destructible>();
            if (d == null)
                d = go.AddComponent<Destructible>();
            d._kind = kind;
            d._material = material;
            d._hp = hp > 0f ? hp : DestructionRules.DefaultHp(kind);
            d._broken = false;
            return d;
        }

        // ============================================================ Dış çağrılar

        /// <summary>Mermi isabeti. Parça kırıldıysa true (mermi içinden geçmeye devam eder).</summary>
        public static bool TryBulletHit(Collider collider, Vector3 point, Vector3 direction, float weaponDamage)
        {
            if (collider == null || !collider.TryGetComponent<Destructible>(out var d) || d._broken)
                return false;
            return d.Damage(DestructionRules.BulletDamage(d._kind, weaponDamage), point, direction);
        }

        /// <summary>Patlama: yarıçaptaki tüm yıkılabilirlere mesafeyle azalan hasar.</summary>
        public static void ExplodeAt(Vector3 position, float radius, float maxDamage)
        {
            if (radius <= 0f)
                return;
            var count = Physics.OverlapSphereNonAlloc(position, radius, OverlapBuffer, GameLayers.WorldMask,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var c = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (c == null || !c.TryGetComponent<Destructible>(out var d) || d._broken)
                    continue;
                var dist = Vector3.Distance(c.ClosestPoint(position), position);
                var dmg = DestructionRules.ExplosionDamage(maxDamage, radius, dist);
                if (dmg <= 0f)
                    continue;
                var dir = c.bounds.center - position;
                d.Damage(dmg, c.bounds.center, dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.up);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_broken)
                return;
            var body = collision.rigidbody;
            if (body == null || (body.gameObject.layer != GameLayers.Vehicle && collision.gameObject.layer != GameLayers.Vehicle))
                return;
            var dmg = DestructionRules.VehicleRamDamage(collision.relativeVelocity.magnitude, body.mass);
            if (dmg <= 0f)
                return;
            var contact = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Damage(dmg, contact, -collision.relativeVelocity.normalized);
        }

        // ============================================================ Hasar / kırılma

        private bool Damage(float amount, Vector3 point, Vector3 direction)
        {
            if (_broken || amount <= 0f)
                return false;
            _hp -= amount;
            if (_hp > 0f)
            {
                if (_kind == DestructibleKind.Wood)
                    GameVfx.Impact(point, -direction, SurfaceKind.Wood);
                return false;
            }

            Break(point, direction);
            return true;
        }

        private void Break(Vector3 point, Vector3 direction)
        {
            _broken = true;
            var col = GetComponent<Collider>();
            var bounds = col != null ? col.bounds : new Bounds(transform.position, Vector3.one * 0.3f);
            if (col != null)
                col.enabled = false;

            try
            {
                var mat = MaterialLibrary.Get(_material);
                var volume = bounds.size.x * bounds.size.y * bounds.size.z;
                var count = DestructionRules.ChunkCount(_kind, volume);
                DebrisPool.Spawn(bounds, point, direction, mat, _kind, count);
                if (_kind == DestructibleKind.Wood)
                    GameVfx.Dust(bounds.center, Mathf.Clamp(bounds.size.magnitude * 0.6f, 0.5f, 3f));
                else
                    GameVfx.Impact(point, -direction, SurfaceKind.Concrete);

                var glass = _kind == DestructibleKind.Glass;
                GameAudio.Play(glass ? SoundId.BulletImpactMetal : SoundId.BulletImpact, bounds.center,
                    glass ? 0.8f : 0.7f, glass ? Random.Range(1.5f, 1.9f) : Random.Range(0.55f, 0.75f), PlayerHearDistance);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            Destroy(gameObject);
        }

        // ============================================================ Enkaz havuzu

        /// <summary>Havuzlu enkaz parçaları: en fazla <see cref="MaxActive"/> aktif, aşılırsa en eskisi geri dönüştürülür.</summary>
        internal sealed class DebrisPool : MonoBehaviour
        {
            private const int MaxActive = 160;
            private const float GlassLife = 3.5f;
            private const float WoodLife = 6f;
            private const float FadeTime = 1f;

            private sealed class Chunk
            {
                public Transform T;
                public Rigidbody Body;
                public MeshRenderer Renderer;
                public Vector3 Scale;
                public float Age;
                public float Life;
            }

            private static DebrisPool _instance;
            private static readonly Stack<Chunk> Free = new Stack<Chunk>(64);
            private static readonly List<Chunk> Active = new List<Chunk>(MaxActive);

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetStatics()
            {
                _instance = null;
                Free.Clear();
                Active.Clear();
            }

            public static int ActiveCount => Active.Count;

            public static void Spawn(Bounds bounds, Vector3 hit, Vector3 dir, Material material, DestructibleKind kind, int count)
            {
                if (!EnsureInstance())
                    return;
                var glass = kind == DestructibleKind.Glass;
                for (var i = 0; i < count; i++)
                {
                    var chunk = Acquire();
                    var p = new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        Random.Range(bounds.min.y, bounds.max.y),
                        Random.Range(bounds.min.z, bounds.max.z));
                    var s = glass
                        ? new Vector3(Random.Range(0.05f, 0.2f), Random.Range(0.05f, 0.2f), 0.012f)
                        : new Vector3(Random.Range(0.08f, 0.3f), Random.Range(0.05f, 0.14f), Random.Range(0.05f, 0.14f));
                    chunk.Scale = s;
                    chunk.Age = 0f;
                    chunk.Life = (glass ? GlassLife : WoodLife) * Random.Range(0.8f, 1.2f);
                    var t = chunk.T;
                    t.SetPositionAndRotation(p, Random.rotationUniform);
                    t.localScale = s;
                    if (chunk.Renderer != null)
                        chunk.Renderer.sharedMaterial = material;
                    t.gameObject.SetActive(true);

                    var body = chunk.Body;
                    body.mass = glass ? 0.05f : 0.6f;
                    body.linearVelocity = (dir.normalized * Random.Range(1f, 4f)) + Random.insideUnitSphere * 1.8f + Vector3.up * 0.6f;
                    body.angularVelocity = Random.insideUnitSphere * 8f;
                    Active.Add(chunk);
                }
            }

            private static bool EnsureInstance()
            {
                if (_instance != null)
                    return true;
                var go = new GameObject("YikimEnkazi");
                _instance = go.AddComponent<DebrisPool>();
                return true;
            }

            private static Chunk Acquire()
            {
                if (Active.Count >= MaxActive)
                {
                    var oldest = Active[0];
                    Active.RemoveAt(0);
                    return oldest;
                }

                if (Free.Count > 0)
                    return Free.Pop();

                var go = new GameObject("Parca");
                go.layer = GameLayers.IgnoreRaycast;
                go.transform.SetParent(_instance.transform, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = StructureKit.UnitCube;
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<BoxCollider>();
                var rb = go.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                return new Chunk { T = go.transform, Body = rb, Renderer = mr };
            }

            private void Update()
            {
                var dt = Time.deltaTime;
                for (var i = Active.Count - 1; i >= 0; i--)
                {
                    var c = Active[i];
                    c.Age += dt;
                    var remaining = c.Life - c.Age;
                    if (remaining <= 0f)
                    {
                        c.T.gameObject.SetActive(false);
                        Active.RemoveAt(i);
                        Free.Push(c);
                    }
                    else if (remaining < FadeTime)
                    {
                        c.T.localScale = c.Scale * (remaining / FadeTime);
                    }
                }
            }
        }
    }
}
