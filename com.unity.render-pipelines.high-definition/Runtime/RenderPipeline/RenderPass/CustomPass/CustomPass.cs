using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System;

namespace UnityEngine.Rendering.HighDefinition
{
    /// <summary>
    /// Class that holds data and logic for the pass to be executed
    /// </summary>
    [System.Serializable]
    public abstract class CustomPass
    {
        public string         name = "Custom Pass";
        public bool           enabled = true;
        public TargetBuffer   targetColorBuffer;
        public TargetBuffer   targetDepthBuffer;
        public ClearFlag      clearFlags;
        public bool           passFoldout;

        [System.NonSerialized]
        bool            isSetup = false;
        bool            isExecuting = false;
        RenderTargets   currentRenderTarget;

        /// <summary>
        /// Mirror of the value in the CustomPassVolume where this custom pass is listed
        /// </summary>
        /// <value>The blend value that should be applied to the custom pass effect</value>
        protected float fadeValue { get; private set; }

        /// <summary>
        /// Used to select the target buffer when executing the custom pass
        /// </summary>
        public enum TargetBuffer
        {
            Camera,
            Custom,
        }

        /// <summary>
        /// Render Queue filters for the DrawRenderers custom pass 
        /// </summary>
        public enum RenderQueueType
        {
            OpaqueNoAlphaTest,
            OpaqueAlphaTest,
            AllOpaque,
            AfterPostProcessOpaque,
            PreRefraction,
            Transparent,
            LowTransparent,
            AllTransparent,
            AllTransparentWithLowRes,
            AfterPostProcessTransparent,
            All,
        }

        internal struct RenderTargets
        {
            public RTHandle cameraColorBuffer;
            public RTHandle cameraDepthBuffer;
            public RTHandle customColorBuffer;
            public RTHandle customDepthBuffer;
        }

        internal void ExecuteInternal(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera camera, CullingResults cullingResult, RenderTargets targets, float fadeValue)
        {
            this.fadeValue = fadeValue;

            if (!isSetup)
            {
                Setup(renderContext, cmd);
                isSetup = true;
            }

            SetCustomPassTarget(cmd, targets);

            currentRenderTarget = targets;
            isExecuting = true;
            Execute(renderContext, cmd, camera, cullingResult);
            isExecuting = false;
            
            // Set back the camera color buffer is we were using a custom buffer as target
            if (targetDepthBuffer != TargetBuffer.Camera)
                CoreUtils.SetRenderTarget(cmd, targets.cameraColorBuffer);
        }

        // Hack to cleanup the custom pass when it is unexpectedly destroyed, which happens every time you edit
        // the UI because of a bug with the SerializeReference attribute.
        ~CustomPass() { CleanupPassInternal(); }

        internal void CleanupPassInternal()
        {
            if (isSetup)
            {
                Cleanup();
                isSetup = false;
            }
        }

        void SetCustomPassTarget(CommandBuffer cmd, RenderTargets targets)
        {
            RTHandle colorBuffer = (targetColorBuffer == TargetBuffer.Custom) ? targets.customColorBuffer : targets.cameraColorBuffer;
            RTHandle depthBuffer = (targetDepthBuffer == TargetBuffer.Custom) ? targets.customDepthBuffer : targets.cameraDepthBuffer;
            CoreUtils.SetRenderTarget(cmd, colorBuffer, depthBuffer, clearFlags);
        }

        /// <summary>
        /// Called when your pass needs to be executed by a camera
        /// </summary>
        /// <param name="renderContext"></param>
        /// <param name="cmd"></param>
        /// <param name="camera"></param>
        /// <param name="cullingResult"></param>
        protected abstract void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult);

        /// <summary>
        /// Called before the first execution of the pass occurs.
        /// Allow you to allocate custom buffers.
        /// </summary>
        /// <param name="renderContext"></param>
        /// <param name="cmd"></param>
        protected virtual void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd) {}

        /// <summary>
        /// Called when HDRP is destroyed.
        /// Allow you to free custom buffers.
        /// </summary>
        protected virtual void Cleanup() {}

        /// <summary>
        /// Bind the camera color buffer as the current render target
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="bindDepth">if true we bind the camera depth buffer in addition to the color</param>
        /// <param name="clearFlags"></param>
        protected void SetCameraRenderTarget(CommandBuffer cmd, bool bindDepth = true, ClearFlag clearFlags = ClearFlag.None)
        {
            if (!isExecuting)
                throw new Exception("SetCameraRenderTarget can only be called inside the CustomPass.Execute function");

            if (bindDepth)
                CoreUtils.SetRenderTarget(cmd, currentRenderTarget.cameraColorBuffer, currentRenderTarget.cameraDepthBuffer, clearFlags);
            else
                CoreUtils.SetRenderTarget(cmd, currentRenderTarget.cameraColorBuffer, clearFlags);
        }

        /// <summary>
        /// Bind the custom color buffer as the current render target
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="bindDepth">if true we bind the custom depth buffer in addition to the color</param>
        /// <param name="clearFlags"></param>
        protected void SetCustomRenderTarget(CommandBuffer cmd, bool bindDepth = true, ClearFlag clearFlags = ClearFlag.None)
        {
            if (!isExecuting)
                throw new Exception("SetCameraRenderTarget can only be called inside the CustomPass.Execute function");

            if (bindDepth)
                CoreUtils.SetRenderTarget(cmd, currentRenderTarget.customColorBuffer, currentRenderTarget.customDepthBuffer, clearFlags);
            else
                CoreUtils.SetRenderTarget(cmd, currentRenderTarget.customColorBuffer, clearFlags);
        }

        /// <summary>
        /// Get the current camera buffers
        /// </summary>
        /// <param name="colorBuffer">outputs the camera color buffer</param>
        /// <param name="depthBuffer">outputs the camera depth buffer</param>
        protected void GetCameraBuffers(out RTHandle colorBuffer, out RTHandle depthBuffer)
        {
            if (!isExecuting)
                throw new Exception("GetCameraBuffers can only be called inside the CustomPass.Execute function");

            colorBuffer = currentRenderTarget.cameraColorBuffer;
            depthBuffer = currentRenderTarget.cameraDepthBuffer;
        }

        /// <summary>
        /// Get the current custom buffers
        /// </summary>
        /// <param name="colorBuffer">outputs the custom color buffer</param>
        /// <param name="depthBuffer">outputs the custom depth buffer</param>
        protected void GetCustomBuffers(out RTHandle colorBuffer, out RTHandle depthBuffer)
        {
            if (!isExecuting)
                throw new Exception("GetCustomBuffers can only be called inside the CustomPass.Execute function");

            colorBuffer = currentRenderTarget.customColorBuffer;
            depthBuffer = currentRenderTarget.customDepthBuffer;
        }

        /// <summary>
        /// Returns the render queue range associated with the custom render queue type
        /// </summary>
        /// <returns></returns>
        protected RenderQueueRange GetRenderQueueRange(CustomPass.RenderQueueType type)
        {
            switch (type)
            {
                case CustomPass.RenderQueueType.OpaqueNoAlphaTest: return HDRenderQueue.k_RenderQueue_OpaqueNoAlphaTest;
                case CustomPass.RenderQueueType.OpaqueAlphaTest: return HDRenderQueue.k_RenderQueue_OpaqueAlphaTest;
                case CustomPass.RenderQueueType.AllOpaque: return HDRenderQueue.k_RenderQueue_AllOpaque;
                case CustomPass.RenderQueueType.AfterPostProcessOpaque: return HDRenderQueue.k_RenderQueue_AfterPostProcessOpaque;
                case CustomPass.RenderQueueType.PreRefraction: return HDRenderQueue.k_RenderQueue_PreRefraction;
                case CustomPass.RenderQueueType.Transparent: return HDRenderQueue.k_RenderQueue_Transparent;
                case CustomPass.RenderQueueType.LowTransparent: return HDRenderQueue.k_RenderQueue_LowTransparent;
                case CustomPass.RenderQueueType.AllTransparent: return HDRenderQueue.k_RenderQueue_AllTransparent;
                case CustomPass.RenderQueueType.AllTransparentWithLowRes: return HDRenderQueue.k_RenderQueue_AllTransparentWithLowRes;
                case CustomPass.RenderQueueType.AfterPostProcessTransparent: return HDRenderQueue.k_RenderQueue_AfterPostProcessTransparent;
                case CustomPass.RenderQueueType.All:
                default:
                    return HDRenderQueue.k_RenderQueue_All;
            }
        }

        /// <summary>
        /// Create a custom pass to execute a fullscreen pass
        /// </summary>
        /// <param name="fullScreenMaterial">The material to use for your fullscreen pass. It must have a shader based on the Custom Pass Fullscreen shader or equivalent</param>
        /// <param name="targetColorBuffer"></param>
        /// <param name="targetDepthBuffer"></param>
        /// <returns></returns>
        public static CustomPass CreateFullScreenPass(Material fullScreenMaterial, TargetBuffer targetColorBuffer = TargetBuffer.Camera,
            TargetBuffer targetDepthBuffer = TargetBuffer.Camera)
        {
            return new FullScreenCustomPass()
            {
                name = "FullScreen Pass",
                targetColorBuffer = targetColorBuffer,
                targetDepthBuffer = targetDepthBuffer,
                fullscreenPassMaterial = fullScreenMaterial,
            };
        }

        /// <summary>
        /// Create a Custom Pass to render objects
        /// </summary>
        /// <param name="queue">The render queue filter to select which object will be rendered</param>
        /// <param name="mask">The layer mask to select which layer(s) will be rendered</param>
        /// <param name="overrideMaterial">The replacement material to use when renering objects</param>
        /// <param name="overrideMaterialPassIndex">The pass to use in the override material</param>
        /// <param name="sorting">Sorting options when rendering objects</param>
        /// <param name="clearFlags">Clear options when the target buffers are bound. Before executing the pass</param>
        /// <param name="targetColorBuffer">Target Color buffer</param>
        /// <param name="targetDepthBuffer">Target Depth buffer. Note: It's also the buffer which will do the Depth Test</param>
        /// <returns></returns>
        public static CustomPass CreateDrawRenderersPass(RenderQueueType queue, LayerMask mask,
            Material overrideMaterial, int overrideMaterialPassIndex = 0, SortingCriteria sorting = SortingCriteria.CommonOpaque,
            ClearFlag clearFlags = ClearFlag.None, TargetBuffer targetColorBuffer = TargetBuffer.Camera,
            TargetBuffer targetDepthBuffer = TargetBuffer.Camera)
        {
            return new DrawRenderersCustomPass()
            {
                name = "DrawRenderers Pass",
                renderQueueType = queue,
                layerMask = mask,
                overrideMaterial = overrideMaterial,
                overrideMaterialPassIndex = overrideMaterialPassIndex,
                sortingCriteria = sorting,
                clearFlags = clearFlags,
                targetColorBuffer = targetColorBuffer,
                targetDepthBuffer = targetDepthBuffer,
            };
        }
    }
}
