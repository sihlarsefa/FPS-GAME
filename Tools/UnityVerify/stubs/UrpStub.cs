// Compile-check stub of com.unity.render-pipelines.universal (signatures copied from 17.6.0 sources). NOT shipped.
using System.Collections.Generic;
namespace UnityEngine.Rendering.Universal
{
    public enum CameraRenderType { Base, Overlay }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public enum TonemappingMode { None, Neutral, ACES, Custom }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { public TonemappingModeParameter(TonemappingMode value, bool overrideState = false) : base(value, overrideState) { } }
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderShadows { get; set; }
        public CameraRenderType renderType { get; set; }
        public List<Camera> cameraStack { get; }
        public bool clearDepth { get; }
        public bool requiresDepthTexture { get; set; }
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
    }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => throw null; }
    public class PostProcessData : ScriptableObject { }
    public abstract class ScriptableRendererFeature : ScriptableObject, System.IDisposable
    {
        public bool isActive => true; public void SetActive(bool active) { }
        public abstract void Create();
        public abstract void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData);
        public void Dispose() { }
        protected virtual void Dispose(bool disposing) { }
    }
    public abstract partial class ScriptableRendererData : ScriptableObject { public List<ScriptableRendererFeature> rendererFeatures => null; public void SetDirty() { } }
    public partial class UniversalRendererData : ScriptableRendererData { public PostProcessData postProcessData = null; }
    public partial class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static readonly string packagePath = "Packages/com.unity.render-pipelines.universal";
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => throw null;
        protected override RenderPipeline CreatePipeline() => throw null;
        public bool supportsCameraDepthTexture { get; set; }
        public bool supportsCameraOpaqueTexture { get; set; }
        public bool supportsHDR { get; set; }
        public int msaaSampleCount { get; set; }
        public float renderScale { get; set; }
        public int mainLightShadowmapResolution { get; set; }
        public int maxAdditionalLightsCount { get; set; }
        public float shadowDistance { get; set; }
        public int shadowCascadeCount { get; set; }
        public bool supportsSoftShadows { get; set; }
        public bool useSRPBatcher { get; set; }
    }
    public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);
    }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None); }
    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure = new FloatParameter(0f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);
        public ColorParameter colorFilter = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);
    }
    public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color = new ColorParameter(Color.black, false, false, true);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);
    }

    public enum DepthOfFieldMode { Off, Gaussian, Bokeh }
    public sealed class DepthOfFieldModeParameter : VolumeParameter<DepthOfFieldMode> { public DepthOfFieldModeParameter(DepthOfFieldMode value, bool overrideState = false) : base(value, overrideState) { } }
    public sealed class DepthOfField : VolumeComponent
    {
        public DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(DepthOfFieldMode.Off);
        public MinFloatParameter gaussianStart = new MinFloatParameter(10f, 0f);
        public MinFloatParameter gaussianEnd = new MinFloatParameter(30f, 0f);
        public ClampedFloatParameter gaussianMaxRadius = new ClampedFloatParameter(1f, 0.5f, 1.5f);
        public BoolParameter highQualitySampling = new BoolParameter(false);
    }
    public enum MotionBlurMode { CameraOnly, CameraAndObjects }
    public enum MotionBlurQuality { Low, Medium, High }
    public sealed class MotionBlurModeParameter : VolumeParameter<MotionBlurMode> { public MotionBlurModeParameter(MotionBlurMode value, bool overrideState = false) : base(value, overrideState) { } }
    public sealed class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality> { public MotionBlurQualityParameter(MotionBlurQuality value, bool overrideState = false) : base(value, overrideState) { } }
    public sealed class MotionBlur : VolumeComponent
    {
        public MotionBlurModeParameter mode = new MotionBlurModeParameter(MotionBlurMode.CameraOnly);
        public MotionBlurQualityParameter quality = new MotionBlurQualityParameter(MotionBlurQuality.Low);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter clamp = new ClampedFloatParameter(0.05f, 0f, 0.2f);
    }
    public enum FilmGrainLookup { Thin1, Thin2, Medium1, Medium2, Medium3, Medium4, Medium5, Medium6, Large01, Large02, Custom }
    public sealed class FilmGrainLookupParameter : VolumeParameter<FilmGrainLookup> { public FilmGrainLookupParameter(FilmGrainLookup value, bool overrideState = false) : base(value, overrideState) { } }
    public sealed class FilmGrain : VolumeComponent
    {
        public FilmGrainLookupParameter type = new FilmGrainLookupParameter(FilmGrainLookup.Thin1);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter response = new ClampedFloatParameter(0.8f, 0f, 1f);
    }
    public sealed class ChromaticAberration : VolumeComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); }
    public sealed class WhiteBalance : VolumeComponent
    {
        public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100f, 100f);
        public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100f, 100f);
    }
    public sealed class ShadowsMidtonesHighlights : VolumeComponent
    {
        public Vector4Parameter shadows = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));
        public Vector4Parameter midtones = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));
        public Vector4Parameter highlights = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));
    }
}
