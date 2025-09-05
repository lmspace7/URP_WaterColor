using Kino.Aqua.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Aqua 이펙트 Render Graph 경로용 패스.
/// 기존 Execute 경로 대신 RecordRenderGraph에서 블릿을 수행
/// </summary>
sealed class AquaEffectRGPass : ScriptableRenderPass
{
    public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
    {
        var cameraData = context.Get<UniversalCameraData>();
        var camera = cameraData.camera;

        var effect = camera.GetComponent<AquaEffect>();
        if (effect == null || effect.enabled == false)
            return;

        var resource = context.Get<UniversalResourceData>();
        if (resource.isActiveTargetBackBuffer == true)
            return;

        var source = resource.activeColorTexture;

        var desc = graph.GetTextureDesc(source);
        desc.name = "Aqua";
        desc.clearBuffer = false;
        desc.depthBufferBits = 0;

        var destination = graph.CreateTexture(desc);

        var blitParams = new RenderGraphUtils.BlitMaterialParameters(
            source,
            destination,
            effect.BlitMaterial,
            0
        );

        graph.AddBlitPass(
            blitParams,
            passName: "Aqua"
        );

        resource.cameraColor = destination;
    }
}

/// <summary>
/// Aqua 이펙트 Render Graph 대응 피처.
/// </summary>
public sealed class AquaEffectFeatureRG : ScriptableRendererFeature
{
    private AquaEffectRGPass _pass;

    public override void Create()
    {
        _pass = new AquaEffectRGPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData
    )
    {
        renderer.EnqueuePass(_pass);
    }
}

