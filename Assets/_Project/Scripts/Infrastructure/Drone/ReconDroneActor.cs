using Project.Application.Services;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Drone
{
    /// <summary>Küçük prosedürel keşif İHA'sı: hedefe 80 m irtifada uçar, görev süresince çevresini tarar.</summary>
    public sealed class ReconDroneActor : MonoBehaviour
    {
        private const float Speed = 45f;
        private const float ScanInterval = 0.5f;

        private int _team;
        private Vector3 _target;
        private float _remaining;
        private float _nextScan;
        private bool _arrived;
        private AudioSource _loop;
        private Transform _rotor;

        public static ReconDroneActor Spawn(int team, Vector3 origin, Vector3 target, float duration)
        {
            var go = new GameObject("ReconDrone");
            var actor = go.AddComponent<ReconDroneActor>();
            actor._team = team;
            actor._target = new Vector3(target.x, ReconDroneService.DefaultAltitude + Mathf.Max(0f, target.y), target.z);
            actor._remaining = duration;
            go.transform.position = origin + Vector3.up * 2f;
            actor.BuildModel();
            try
            {
                actor._loop = GameAudio.StartLoop(SoundId.HelicopterRotor, go.transform, 0.35f, true, 150f);
            }
            catch (System.Exception)
            {
                actor._loop = null;
            }

            return actor;
        }

        private void BuildModel()
        {
            var mat = MaterialLibrary.Lit(new Color(0.12f, 0.13f, 0.12f), 0.3f, 0.4f);
            Part(PrimitiveType.Cube, "Body", Vector3.zero, new Vector3(0.7f, 0.2f, 1.1f), mat, transform);
            _rotor = new GameObject("Rotors").transform;
            _rotor.SetParent(transform, false);
            var offs = new[] { new Vector3(0.6f, 0.1f, 0.6f), new Vector3(-0.6f, 0.1f, 0.6f), new Vector3(0.6f, 0.1f, -0.6f), new Vector3(-0.6f, 0.1f, -0.6f) };
            for (var i = 0; i < offs.Length; i++)
            {
                Part(PrimitiveType.Cube, "Arm" + i, offs[i] * 0.5f, new Vector3(0.1f, 0.06f, 0.1f), mat, transform);
                Part(PrimitiveType.Cylinder, "Rotor" + i, offs[i], new Vector3(0.55f, 0.01f, 0.55f), mat, _rotor);
            }

            transform.localScale = Vector3.one * 1.6f;
        }

        private static void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            var col = p.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            var r = p.GetComponent<Renderer>();
            if (r != null && mat != null)
                r.sharedMaterial = mat;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_rotor != null)
                _rotor.Rotate(0f, 1800f * dt, 0f, Space.Self);

            var pos = transform.position;
            var to = _target - pos;
            if (!_arrived)
            {
                if (to.magnitude <= 2f)
                    _arrived = true;
                else
                {
                    transform.position = Vector3.MoveTowards(pos, _target, Speed * dt);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(to.x, 0f, to.z).sqrMagnitude > 0.01f ? new Vector3(to.x, 0f, to.z) : transform.forward), 4f * dt);
                }
            }
            else
            {
                _remaining -= dt;
                transform.position = _target + new Vector3(Mathf.Sin(Time.time * 0.8f) * 3f, 0f, Mathf.Cos(Time.time * 0.8f) * 3f);
                if (_remaining <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                if (_arrived)
                    ReconDroneSystem.Scan(_team, transform.position);
            }
        }

        private void OnDestroy()
        {
            if (_loop != null)
            {
                try { GameAudio.StopLoop(_loop); } catch (System.Exception) { }
            }
        }
    }
}
