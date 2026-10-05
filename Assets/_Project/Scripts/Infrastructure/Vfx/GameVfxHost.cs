using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// "[GameVfx]" kök nesnesindeki (DontDestroyOnLoad) güncelleyici: kısa ışıkları söndürür, mermi izlerini ilerletir,
    /// kamerayı izler. Tüm durum <see cref="GameVfx"/> içindedir; bu bileşen yalnızca Unity yaşam döngüsünü taşır.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class GameVfxHost : MonoBehaviour
    {
        private void LateUpdate()
        {
            GameVfx.HostLateUpdate(this, Time.deltaTime);
        }

        private void OnDestroy()
        {
            GameVfx.HostDestroyed(this);
        }
    }
}
