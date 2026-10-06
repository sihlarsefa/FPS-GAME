// Compile-check stub: RenderGraph + ScriptableRenderPass API surface (signatures from URP 17.6 / Core 17.6). NOT shipped.
// Derlenir: setup_deps.sh (Universal stub DLL'ine UrpStub.cs ile birlikte). Yalnız derleme denetimi içindir.
using System;
using System.Collections.Generic;
namespace UnityEngine.Rendering
{
    public class RTHandle { public RenderTexture rt => null; public void Release() { } public static implicit operator Texture(RTHandle h) => null; }
    public class ProfilingSampler { public ProfilingSampler(string name) { } }
    public abstract class ContextItem { public abstract void Reset(); }
    public class ContextContainer : IDisposable
    {
        public T Get<T>() where T : ContextItem, new() => throw null;
        public bool Contains<T>() where T : ContextItem, new() => throw null;
        public void Dispose() { }
    }
    public class RasterCommandBuffer
    {
        public void SetGlobalVector(int nameID, Vector4 value) { }
        public void SetGlobalFloat(int nameID, float value) { }
        public void SetGlobalMatrix(int nameID, Matrix4x4 value) { }
        public void SetGlobalTexture(int nameID, RTHandle value) { }
        public void SetKeyword(in GlobalKeyword keyword, bool value) { }
        public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount, int instanceCount, MaterialPropertyBlock properties) { }
        public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount) { }
    }
    public static class Blitter
    {
        public static void BlitTexture(RasterCommandBuffer cmd, RTHandle source, Vector4 scaleBias, Material material, int pass) { }
        public static void BlitTexture(RasterCommandBuffer cmd, Vector4 scaleBias, Material material, int pass) { }
    }
}
namespace UnityEngine.Rendering.RenderGraphModule
{
    [Flags]
    public enum AccessFlags { None = 0, Read = 1, Write = 2, Discard = 4, WriteAll = Write | Discard, ReadWrite = Read | Write }
    public readonly struct TextureHandle
    {
        public bool IsValid() => false;
        public static implicit operator RTHandle(in TextureHandle texture) => null;
        public static implicit operator Texture(in TextureHandle texture) => null;
    }
    public struct TextureDesc
    {
        public string name; public int width, height; public Experimental.Rendering.GraphicsFormat format; public FilterMode filterMode; public TextureWrapMode wrapMode; public bool clearBuffer;
        public TextureDesc(int width, int height, bool dynamicResolution = false, bool xrReady = false) { name = null; this.width = width; this.height = height; format = default; filterMode = default; wrapMode = default; clearBuffer = false; }
    }
    public struct RasterGraphContext { public RasterCommandBuffer cmd; }
    public delegate void BaseRenderFunc<PassData, ContextType>(PassData data, ContextType renderGraphContext) where PassData : class, new();
    public interface IRasterRenderGraphBuilder : IDisposable
    {
        void UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read);
        void SetGlobalTextureAfterPass(in TextureHandle input, int propertyId);
        void AllowPassCulling(bool value);
        void AllowGlobalStateModification(bool value);
        void SetRenderAttachment(TextureHandle tex, int index, AccessFlags flags = AccessFlags.Write);
        void SetRenderAttachmentDepth(TextureHandle tex, AccessFlags flags = AccessFlags.ReadWrite);
        void SetRenderFunc<PassData>(BaseRenderFunc<PassData, RasterGraphContext> renderFunc) where PassData : class, new();
    }
    public class RenderGraph
    {
        public TextureHandle CreateTexture(in TextureDesc desc) => default;
        public TextureHandle ImportTexture(RTHandle rt) => default;
        public IRasterRenderGraphBuilder AddRasterRenderPass<PassData>(string passName, out PassData passData, ProfilingSampler sampler) where PassData : class, new() => throw null;
        public IRasterRenderGraphBuilder AddRasterRenderPass<PassData>(string passName, out PassData passData) where PassData : class, new() => throw null;
    }
}
namespace UnityEngine.Rendering.Universal
{
    using UnityEngine.Rendering.RenderGraphModule;
    public enum RenderPassEvent
    {
        BeforeRendering = 0, BeforeRenderingShadows = 50, AfterRenderingShadows = 100, BeforeRenderingPrePasses = 150, AfterRenderingPrePasses = 200,
        BeforeRenderingGbuffer = 210, AfterRenderingGbuffer = 220, BeforeRenderingDeferredLights = 230, AfterRenderingDeferredLights = 240,
        BeforeRenderingOpaques = 250, AfterRenderingOpaques = 300, BeforeRenderingSkybox = 350, AfterRenderingSkybox = 400,
        BeforeRenderingTransparents = 450, AfterRenderingTransparents = 500, BeforeRenderingPostProcessing = 550, AfterRenderingPostProcessing = 600, AfterRendering = 1000
    }
    [Flags]
    public enum ScriptableRenderPassInput { None = 0, Depth = 1, Normal = 2, Color = 4, Motion = 8 }
    public abstract partial class ScriptableRenderPass
    {
        public RenderPassEvent renderPassEvent { get; set; }
        public ProfilingSampler profilingSampler { get; set; }
        public void ConfigureInput(ScriptableRenderPassInput passInput) { }
        public virtual void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) { }
    }
    public static class RenderingUtils
    {
        public static bool ReAllocateHandleIfNeeded(ref RTHandle handle, in RenderTextureDescriptor descriptor, FilterMode filterMode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Repeat, int anisoLevel = 1, float mipMapBias = 0, string name = "") => false;
    }
    public abstract class ScriptableRenderer { public void EnqueuePass(ScriptableRenderPass pass) { } }
    public struct CameraData
    {
        public Camera camera => null;
        public CameraType cameraType => CameraType.Game;
    }
    public struct RenderingData { internal ContextContainer frameData; public CameraData cameraData => default; }
    public class UniversalCameraData : ContextItem
    {
        public Camera camera;
        public RenderTextureDescriptor cameraTargetDescriptor;
        public CameraType cameraType;
        public bool isPreviewCamera => false;
        public CameraRenderType renderType;
        public override void Reset() { }
    }
    public class UniversalRenderingData : ContextItem { public bool writesSmoothnessToDepthNormalsAlpha { get; internal set; } public override void Reset() { } }
    public class UniversalResourceData : ContextItem
    {
        public TextureHandle activeColorTexture => default;
        public TextureHandle activeDepthTexture => default;
        public TextureHandle mainShadowsTexture => default;
        public TextureHandle cameraDepthTexture => default;
        public TextureHandle cameraNormalsTexture => default;
        public TextureHandle cameraOpaqueTexture => default;
        public override void Reset() { }
    }
    public partial class UniversalRenderer : ScriptableRenderer
    {
        public static TextureHandle CreateRenderGraphTexture(RenderGraph renderGraph, RenderTextureDescriptor desc, string name, bool clear, FilterMode filterMode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Clamp) => default;
    }
}
