using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/*
 * Blit Renderer Feature                                                https://github.com/Cyanilux/URP_BlitRenderFeature
 * ------------------------------------------------------------------------------------------------------------------------
 * Based on the Blit from the UniversalRenderingExamples
 * https://github.com/Unity-Technologies/UniversalRenderingExamples/tree/master/Assets/Scripts/Runtime/RenderPasses
 * 
 * Extended to allow for :
 * - Specific access to selecting a source and destination (via current camera's color / texture id / render texture object
 * - Automatic switching to using _AfterPostProcessTexture for After Rendering event, in order to correctly handle the blit after post processing is applied
 * - Setting a _InverseView matrix (cameraToWorldMatrix), for shaders that might need it to handle calculations from screen space to world.
 *     e.g. reconstruct world pos from depth : https://twitter.com/Cyanilux/status/1269353975058501636 
 * ------------------------------------------------------------------------------------------------------------------------
 * @Cyanilux
*/
public class Blit : ScriptableRendererFeature {

    public class BlitPass : ScriptableRenderPass {

        public Material blitMaterial = null;
        public FilterMode filterMode { get; set; }
        
        private BlitSettings settings;

        private RTHandle source { get; set; }
        private RTHandle destination { get; set; }

        RTHandle m_TemporaryColorTexture;
        RTHandle m_DestinationTexture;
        string m_ProfilerTag;

        public BlitPass(RenderPassEvent renderPassEvent, BlitSettings settings, string tag) {
            this.renderPassEvent = renderPassEvent;
            this.settings = settings;
            blitMaterial = settings.blitMaterial;
            m_ProfilerTag = tag;
        }

        public void Setup(RTHandle source, RTHandle destination) {
            this.source = source;
            this.destination = destination;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

            RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;

            if (settings.setInverseViewMatrix) {
                Shader.SetGlobalMatrix("_InverseView", renderingData.cameraData.camera.cameraToWorldMatrix);
            }

            if (settings.dstType == Target.TextureID) {
                RenderingUtils.ReAllocateIfNeeded(
                    ref m_DestinationTexture,
                    opaqueDesc,
                    filterMode,
                    TextureWrapMode.Clamp,
                    name: settings.dstTextureId
                );
            }

            //Debug.Log($"src = {source},     dst = {destination} ");
            // Can't read and write to same color target, use a Temporary RTHandle
            RTHandle dstHandle = destination;
            if (settings.dstType == Target.TextureID && m_DestinationTexture != null) {
                dstHandle = m_DestinationTexture;
            }

            bool needsTemp = false;
            if (source == dstHandle) {
                needsTemp = true;
            }
            if (settings.srcType == settings.dstType && settings.srcType == Target.CameraColor) {
                needsTemp = true;
            }

            if (needsTemp) {
                RenderingUtils.ReAllocateIfNeeded(
                    ref m_TemporaryColorTexture,
                    opaqueDesc,
                    filterMode,
                    TextureWrapMode.Clamp,
                    name: "_TemporaryColorTexture"
                );
                Blit(cmd, source, m_TemporaryColorTexture, blitMaterial, settings.blitMaterialPassIndex);
                Blit(cmd, m_TemporaryColorTexture, dstHandle);
            } else {
                Blit(cmd, source, dstHandle, blitMaterial, settings.blitMaterialPassIndex);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public override void FrameCleanup(CommandBuffer cmd) {
            if (settings.dstType == Target.TextureID && m_DestinationTexture != null) {
                m_DestinationTexture.Release();
            }
            if (m_TemporaryColorTexture != null) {
                m_TemporaryColorTexture.Release();
            }
        }
    }

    [System.Serializable]
    public class BlitSettings {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;

        public Material blitMaterial = null;
        public int blitMaterialPassIndex = 0;
        public bool setInverseViewMatrix = false;

        public Target srcType = Target.CameraColor;
        public string srcTextureId = "_CameraColorTexture";
        public RenderTexture srcTextureObject;

        public Target dstType = Target.CameraColor;
        public string dstTextureId = "_BlitPassTexture";
        public RenderTexture dstTextureObject;
    }

    public enum Target {
        CameraColor,
        TextureID,
        RenderTextureObject
    }

    public BlitSettings settings = new BlitSettings();
    
    BlitPass blitPass;

    // Identifiers removed; using RTHandles directly

    public override void Create() {
        var passIndex = settings.blitMaterial != null ? settings.blitMaterial.passCount - 1 : 1;
        settings.blitMaterialPassIndex = Mathf.Clamp(settings.blitMaterialPassIndex, -1, passIndex);
        blitPass = new BlitPass(settings.Event, settings, name);

        if (settings.Event == RenderPassEvent.AfterRenderingPostProcessing) {
            Debug.LogWarning("Note that the \"After Rendering Post Processing\"'s Color target doesn't seem to work? (or might work, but doesn't contain the post processing) :( -- Use \"After Rendering\" instead!");
        }

    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {

        if (settings.blitMaterial == null) {
            Debug.LogWarningFormat("Missing Blit Material. {0} blit pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
            return;
        }

        if (settings.Event == RenderPassEvent.AfterRenderingPostProcessing) {
        } else if (settings.Event == RenderPassEvent.AfterRendering && renderingData.postProcessingEnabled) {
            // 이전에는 _AfterPostProcessTexture로 전환했지만, RTHandle 경로에서는 기본 카메라 컬러 핸들을 사용합니다.
        } else {
            // no-op
        }
        
        RTHandle src = renderer.cameraColorTargetHandle;
        if (settings.srcType == Target.RenderTextureObject && settings.srcTextureObject != null) {
            src = RTHandles.Alloc(settings.srcTextureObject);
        }
        RTHandle dest = renderer.cameraColorTargetHandle;
        if (settings.dstType == Target.RenderTextureObject && settings.dstTextureObject != null) {
            dest = RTHandles.Alloc(settings.dstTextureObject);
        }
        
        blitPass.Setup(src, dest);
        renderer.EnqueuePass(blitPass);
    }
}