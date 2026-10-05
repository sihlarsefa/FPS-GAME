using System.Collections.Generic;
using Project.Infrastructure.Input;
using Project.Presentation.Player;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tam ekran harita / envanter açıkken imleç ve oyun girdisi yönetimi. İlk sahip açıldığında imleç durumu ve
    /// <see cref="UnityInputReader.GameplayEnabled"/> saklanır, imleç serbest bırakılır ve oyun girdisi (ateş, bakış,
    /// hareket) kapatılır — böylece düğmelere tıklamak silahı ateşlemez. Son sahip kapandığında önceki durum geri yüklenir
    /// (oyun duraklatılmışsa imleç kilitlenmez; oyuncu ölmüşse oyun girdisi açılmaz).
    /// Diğer modüller (pres-player imleç kilidi, duraklatma menüsü) <see cref="IsAnyOpen"/> ile sorgulayabilir.
    /// </summary>
    public static class MapOverlayInput
    {
        private static readonly List<object> Owners = new List<object>(2);
        private static CursorLockMode _savedLockState = CursorLockMode.Locked;
        private static bool _savedVisible;
        private static UnityInputReader _reader;
        private static bool _savedGameplay = true;
        private static IPlayerHudSource _player;

        /// <summary>Harita ya da envanter gibi imleç isteyen bir katman açık mı?</summary>
        public static bool IsAnyOpen => Owners.Count > 0;

        /// <summary>Katman açılırken çağrılır (aynı sahip iki kez sayılmaz).</summary>
        public static void Acquire(object owner, IPlayerHudSource player)
        {
            if (owner == null || Owners.Contains(owner))
                return;

            if (Owners.Count == 0)
            {
                _savedLockState = Cursor.lockState;
                _savedVisible = Cursor.visible;
                _player = player;
                _reader = ResolveReader(player);
                if (_reader != null)
                {
                    _savedGameplay = _reader.GameplayEnabled;
                    _reader.GameplayEnabled = false;
                }
            }

            Owners.Add(owner);
            ApplyUnlocked();
        }

        /// <summary>Katman kapanırken çağrılır. Son sahip de kapanınca önceki durum geri yüklenir.</summary>
        public static void Release(object owner)
        {
            if (owner == null || !Owners.Remove(owner) || Owners.Count > 0)
                return;

            var playerDead = IsPlayerDead(_player);
            if (_reader != null)
                _reader.GameplayEnabled = _savedGameplay && !playerDead;

            _reader = null;
            _player = null;

            // Duraklatma menüsü açıksa (zaman durmuş) imleç serbest kalmalı; onu menü yönetir.
            var paused = Time.timeScale <= 0f;
            if (!paused && !playerDead)
            {
                Cursor.lockState = _savedLockState;
                Cursor.visible = _savedVisible;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Açıkken her kare çağrılır: başka bir sistem imleci yeniden kilitlediyse serbest bırakır, oyun girdisini kapalı tutar.
        /// </summary>
        public static void Maintain()
        {
            if (Owners.Count == 0)
                return;

            ApplyUnlocked();
            if (_reader != null && _reader.GameplayEnabled)
                _reader.GameplayEnabled = false;
        }

        private static void ApplyUnlocked()
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        private static UnityInputReader ResolveReader(IPlayerHudSource player)
        {
            if (player is PlayerController controller && controller != null)
            {
                var input = controller.Input;
                if (input != null)
                    return input;
            }

            if (player is Component component && component != null)
                return component.GetComponentInChildren<UnityInputReader>();

            return null;
        }

        private static bool IsPlayerDead(IPlayerHudSource player)
        {
            if (player == null || (player is Object unityObject && unityObject == null))
                return false;

            try
            {
                return player.IsDead;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Owners.Clear();
            _reader = null;
            _player = null;
            _savedGameplay = true;
            _savedLockState = CursorLockMode.Locked;
            _savedVisible = false;
        }
    }
}
