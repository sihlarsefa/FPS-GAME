namespace Project.Core.Interfaces
{
    public interface IFirstPersonCamera
    {
        float Pitch { get; }
        void ApplyLook(float pitchDelta, float yawDelta);
        void SetSensitivity(float sensitivity);

        /// <summary>Silah sekmesi: yukarı (pitch) ve yatay (yaw) derece.</summary>
        void AddRecoil(float pitchDegrees, float yawDegrees);

        void SetFieldOfView(float fieldOfView);
        void Shake(float intensity, float durationSeconds);
    }
}
