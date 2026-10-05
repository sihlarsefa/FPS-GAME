using System;
using Project.Infrastructure.Input;
using Project.Infrastructure.Player;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// ESKİ prototip oyuncu sunucusu. Yerini <see cref="PlayerController"/> aldı; yalnızca eski sahneler/kurulumlar
    /// derlenmeye devam etsin diye tutulur. Hiçbir şey yapmaz.
    /// </summary>
    [Obsolete("PlayerController.Create(PlayerSpawnArgs) kullanın.")]
    [AddComponentMenu("")]
    public sealed class FirstPersonPlayerPresenter : MonoBehaviour
    {
        // Eski kurulum kodu bu alanları yansıma ile doldurur; tipleri gevşek tutuldu.
        [SerializeField] private CharacterControllerMotor motor;
        [SerializeField] private Component cameraController;
        [SerializeField] private UnityInputReader inputReader;
        [SerializeField] private Component healthComponent;
        [SerializeField] private Component zoneDamageController;

        public CharacterControllerMotor Motor => motor;
        public UnityInputReader InputReader => inputReader;
    }
}
