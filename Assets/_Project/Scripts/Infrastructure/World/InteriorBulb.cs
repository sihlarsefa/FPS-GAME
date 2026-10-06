using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Tavandaki çıplak ampul: sıcak nokta ışık, yalnızca gece ve izleyiciye yakınken yanar (0,75 sn'de bir örnekleme).</summary>
    public sealed class InteriorBulb : MonoBehaviour
    {
        private Light _light;
        private float _next;
        private bool _on;

        public static InteriorBulb Attach(GameObject go)
        {
            var b = go.AddComponent<InteriorBulb>();
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.48f);
            l.range = 5f;
            l.intensity = 1.25f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            b._light = l;
            b._next = Random.value * 0.75f;
            return b;
        }

        private void Update()
        {
            if (Time.time < _next || _light == null)
                return;
            _next = Time.time + 0.75f;
            var sun = RenderSettings.sun;
            var cam = Camera.main;
            var night = sun != null && cam != null && InteriorDecorPlan.BulbOn(sun.transform.forward.y, Vector3.Distance(cam.transform.position, transform.position));
            if (night == _on)
                return;
            _on = night;
            _light.enabled = night;
        }
    }
}
