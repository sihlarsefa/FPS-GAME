// Compile-check stub of com.unity.render-pipelines.core (signatures copied from 17.6.0 sources). NOT shipped.
using System;
using System.Collections.Generic;
namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter { public bool overrideState; }
    public class VolumeParameter<T> : VolumeParameter
    {
        protected T m_Value;
        public VolumeParameter() { }
        protected VolumeParameter(T value, bool overrideState) { m_Value = value; this.overrideState = overrideState; }
        public virtual T value { get => m_Value; set => m_Value = value; }
        public void Override(T x) { overrideState = true; m_Value = x; }
    }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool value, bool overrideState = false) : base(value, overrideState) { } }
    public class FloatParameter : VolumeParameter<float> { public FloatParameter(float value, bool overrideState = false) : base(value, overrideState) { } }
    public class MinFloatParameter : FloatParameter { public float min; public MinFloatParameter(float value, float min, bool overrideState = false) : base(value, overrideState) { this.min = min; } }
    public class ClampedFloatParameter : FloatParameter { public float min, max; public ClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, overrideState) { this.min = min; this.max = max; } }
    public class Vector4Parameter : VolumeParameter<Vector4> { public Vector4Parameter(Vector4 value, bool overrideState = false) : base(value, overrideState) { } }
    public class ColorParameter : VolumeParameter<Color>
    {
        public ColorParameter(Color value, bool overrideState = false) : base(value, overrideState) { }
        public ColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper, bool overrideState = false) : base(value, overrideState) { }
    }
    public class VolumeComponent : ScriptableObject { public bool active = true; }
    public sealed class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public T Add<T>(bool overrides = false) where T : VolumeComponent => throw null;
        public VolumeComponent Add(Type type, bool overrides = false) => throw null;
        public bool TryGet<T>(out T component) where T : VolumeComponent => throw null;
        public bool Has<T>() where T : VolumeComponent => throw null;
    }
    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority = 0f;
        public float blendDistance = 0f;
        public float weight = 1f;
        public VolumeProfile sharedProfile = null;
        public VolumeProfile profile { get; set; }
    }
}
