using UnityEngine;
using UnityEngine.AI;

namespace Project.Presentation.Benchmark
{
    /// <summary>Benchmark nöbetçisi: çapa çevresinde uzun bekleyip kısa devriye adımları atar (NavMesh yoksa olduğu yerde durur).</summary>
    [DisallowMultipleComponent]
    public sealed class AaaBenchmarkPatrol : MonoBehaviour
    {
        private NavMeshAgent _agent;
        private Vector3 _anchor;
        private float _radius = 3f;
        private float _nextMove;
        private int _step;

        public void Initialize(NavMeshAgent agent, Vector3 anchor, float radius)
        {
            _agent = agent;
            _anchor = anchor;
            _radius = Mathf.Max(0.5f, radius);
            _nextMove = Time.time + 6f;
        }

        private void Update()
        {
            if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh || Time.time < _nextMove)
                return;

            _step++;
            var angle = _step * 2.1f;
            var target = _anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _radius;
            if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
            _nextMove = Time.time + 9f;
        }
    }
}
