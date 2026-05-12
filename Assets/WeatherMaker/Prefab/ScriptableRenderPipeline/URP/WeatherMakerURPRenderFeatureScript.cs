#if UNITY_URP

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Linq;
using System;

namespace DigitalRuby.WeatherMaker
{
    public class WeatherMakerURPRenderFeatureScript : ScriptableRendererFeature
    {
        /// <summary>
        /// Renderer
        /// </summary>
        public ScriptableRenderer Renderer { get; private set; }

        public class ExecuteCommandBuffersPass : ScriptableRenderPass
        {
            private static readonly Dictionary<CameraEvent, RenderPassEvent> cameraEventToRenderPassEvent = new()
            {
                { CameraEvent.AfterDepthNormalsTexture, RenderPassEvent.AfterRenderingPrePasses },
                { CameraEvent.AfterDepthTexture, RenderPassEvent.AfterRenderingPrePasses },
                { CameraEvent.BeforeGBuffer, RenderPassEvent.BeforeRenderingGbuffer },
                { CameraEvent.AfterEverything, RenderPassEvent.AfterRendering },
                { CameraEvent.AfterFinalPass, RenderPassEvent.AfterRendering },
                { CameraEvent.AfterHaloAndLensFlares, RenderPassEvent.AfterRendering },
                { CameraEvent.AfterForwardAlpha, RenderPassEvent.BeforeRenderingPostProcessing },
                { CameraEvent.BeforeFinalPass, RenderPassEvent.BeforeRenderingPostProcessing },
                { CameraEvent.BeforeHaloAndLensFlares, RenderPassEvent.BeforeRenderingPostProcessing },
                { CameraEvent.BeforeImageEffects, RenderPassEvent.BeforeRenderingPostProcessing },
                { CameraEvent.AfterGBuffer, RenderPassEvent.AfterRenderingGbuffer },
                { CameraEvent.AfterForwardOpaque, RenderPassEvent.AfterRenderingOpaques },
                { CameraEvent.AfterImageEffectsOpaque, RenderPassEvent.AfterRenderingOpaques },
                { CameraEvent.BeforeImageEffectsOpaque, RenderPassEvent.AfterRenderingOpaques },
                { CameraEvent.AfterImageEffects, RenderPassEvent.AfterRenderingPostProcessing },
                { CameraEvent.AfterLighting, RenderPassEvent.AfterRenderingShadows },
                { CameraEvent.AfterReflections, RenderPassEvent.AfterRenderingShadows },
                { CameraEvent.AfterSkybox, RenderPassEvent.AfterRenderingSkybox },
                { CameraEvent.BeforeDepthNormalsTexture, RenderPassEvent.BeforeRenderingPrePasses },
                { CameraEvent.BeforeDepthTexture, RenderPassEvent.BeforeRenderingPrePasses },
                // Keep fog-like fullscreen effects before skybox to avoid transient one-frame sky fog bleed on rapid camera motion.
                { CameraEvent.BeforeForwardAlpha, RenderPassEvent.BeforeRenderingSkybox },
                { CameraEvent.BeforeForwardOpaque, RenderPassEvent.BeforeRenderingOpaques },
                { CameraEvent.BeforeLighting, RenderPassEvent.BeforeRenderingShadows },
                { CameraEvent.BeforeReflections, RenderPassEvent.BeforeRenderingShadows },
                { CameraEvent.BeforeSkybox, RenderPassEvent.BeforeRenderingSkybox }
            };

            private readonly WeatherMakerURPRenderFeatureScript feature;
            private readonly CameraEvent cameraEvent;
            private RTHandle colorTarget;
            private RTHandle depthTarget;

            public CameraEvent CameraEvent => cameraEvent;

            public ExecuteCommandBuffersPass(WeatherMakerURPRenderFeatureScript feature, CameraEvent evt) : base()
            {
                this.feature = feature;
                cameraEvent = evt;
                renderPassEvent = cameraEventToRenderPassEvent[evt];
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            private static void SetRenderTargets(CommandBuffer cmd, RTHandle color, RTHandle depth)
            {
                if (color != null && depth != null)
                {
                    cmd.SetRenderTarget(color, depth);
                }
                else if (color != null)
                {
                    cmd.SetRenderTarget(color);
                }
            }

            private static void ExecuteActions(CommandBuffer cmd, IReadOnlyList<System.Action<WeatherMakerCommandBufferContext>> actions,
                RTHandle colorHandle, RTHandle depthHandle, Camera camera)
            {
                // set thread-static context so static CameraTargetIdentifier()/SetCameraTarget() resolve to URP targets
                WeatherMakerFullScreenEffect.currentURPColorTarget = colorHandle;
                WeatherMakerFullScreenEffect.currentURPDepthTarget = depthHandle;

                try
                {
                    var ctx = new WeatherMakerCommandBufferContext
                    {
                        CommandBuffer = cmd,
                        ColorTarget = colorHandle,
                        DepthTarget = depthHandle,
                        Camera = camera
                    };
                    foreach (var action in actions)
                    {
                        action(ctx);
                    }
                }
                finally
                {
                    // always clear thread-static targets so scene/game camera state cannot leak across passes
                    WeatherMakerFullScreenEffect.currentURPColorTarget = null;
                    WeatherMakerFullScreenEffect.currentURPDepthTarget = null;
                }
            }

            [System.Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (feature.Renderer != null)
                {
                    colorTarget = feature.Renderer.cameraColorTargetHandle;
                    depthTarget = feature.Renderer.cameraDepthTargetHandle;
                    ConfigureTarget(colorTarget, depthTarget);
                }
            }

            [System.Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (WeatherMakerCommandBufferManagerScript.Instance == null)
                {
                    return;
                }

                Camera camera = renderingData.cameraData.camera;
                if (camera != null && camera.cameraType == CameraType.SceneView &&
                    (WeatherMakerScript.Instance == null || !WeatherMakerScript.Instance.AllowSceneCamera))
                {
                    return;
                }

                var actions = WeatherMakerCommandBufferManagerScript.Instance.GetCameraCommandBufferActions(camera, cameraEvent);
                if (actions == null || actions.Count == 0)
                {
                    return;
                }

                var cmd = new CommandBuffer();
                cmd.name = "WeatherMaker " + cameraEvent.ToString();
                SetRenderTargets(cmd, colorTarget, depthTarget);
                ExecuteActions(cmd, actions, colorTarget, depthTarget, camera);
                SetRenderTargets(cmd, colorTarget, depthTarget);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                cmd.Dispose();
            }

            private class PassData
            {
                public IReadOnlyList<System.Action<WeatherMakerCommandBufferContext>> actions;
                public TextureHandle colorTarget;
                public TextureHandle depthTarget;
                public Camera camera;
                public RTHandle colorRTHandle;
                public RTHandle depthRTHandle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (WeatherMakerCommandBufferManagerScript.Instance == null)
                {
                    return;
                }

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                if (resourceData == null || cameraData == null)
                    return;
                if (cameraData.camera != null && cameraData.camera.cameraType == CameraType.SceneView &&
                    (WeatherMakerScript.Instance == null || !WeatherMakerScript.Instance.AllowSceneCamera))
                    return;

                var actions = WeatherMakerCommandBufferManagerScript.Instance.GetCameraCommandBufferActions(cameraData.camera, cameraEvent);
                if (actions == null || actions.Count == 0)
                    return;

                using (var builder = renderGraph.AddUnsafePass<PassData>("WeatherMaker " + cameraEvent.ToString(), out var passData))
                {
                    passData.actions = actions;
                    passData.colorTarget = resourceData.activeColorTexture;
                    passData.depthTarget = resourceData.activeDepthTexture;
                    passData.camera = cameraData.camera;
                    passData.colorRTHandle = null; // converted inside SetRenderFunc where resource registry is available
                    passData.depthRTHandle = null;

                    if (resourceData.activeColorTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                    }
                    if (resourceData.activeDepthTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    }
                    if (resourceData.mainShadowsTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);
                    }
                    if (resourceData.additionalShadowsTexture.IsValid())
                    {
                        builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);
                    }
                    var gBuffer = resourceData.gBuffer;
                    if (gBuffer != null)
                    {
                        for (int i = 0; i < gBuffer.Length; i++)
                        {
                            if (gBuffer[i].IsValid())
                            {
                                builder.UseTexture(gBuffer[i], AccessFlags.Read);
                            }
                        }
                    }

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        try
                        {
                            // Convert TextureHandle → RTHandle inside SetRenderFunc where the resource registry is available
                            data.colorRTHandle = data.colorTarget.IsValid() ? (RTHandle)data.colorTarget : null;
                            data.depthRTHandle = data.depthTarget.IsValid() ? (RTHandle)data.depthTarget : null;

                            CommandBuffer nativeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                            if (data.colorRTHandle != null && data.depthRTHandle != null)
                            {
                                nativeCmd.SetRenderTarget(data.colorRTHandle, data.depthRTHandle);
                            }
                            else if (data.colorRTHandle != null)
                            {
                                nativeCmd.SetRenderTarget(data.colorRTHandle);
                            }

                            ExecuteActions(nativeCmd, data.actions, data.colorRTHandle, data.depthRTHandle, data.camera);

                            if (data.colorRTHandle != null)
                            {
                                if (data.depthRTHandle != null)
                                {
                                    nativeCmd.SetRenderTarget(data.colorRTHandle, data.depthRTHandle);
                                }
                                else
                                {
                                    nativeCmd.SetRenderTarget(data.colorRTHandle);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[WM-URP] Error in render pass ({data.camera?.name}): {ex.Message}\n{ex.StackTrace}");
                        }
                    });
                }
            }
        }

        private readonly Dictionary<CameraEvent, ExecuteCommandBuffersPass> commandBufferPassMap = new Dictionary<CameraEvent, ExecuteCommandBuffersPass>();

#pragma warning disable CS0618
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            this.Renderer = renderer;
            WeatherMakerFullScreenEffect.urpRenderer = this;

            if (WeatherMakerCommandBufferManagerScript.Instance == null)
            {
                return;
            }

            Camera camera = renderingData.cameraData.camera;
            if (camera != null && camera.cameraType == CameraType.SceneView &&
                (WeatherMakerScript.Instance == null || !WeatherMakerScript.Instance.AllowSceneCamera))
            {
                return;
            }

            // only enqueue passes that have registered actions for this camera
            foreach (var kvp in commandBufferPassMap)
            {
                var actions = WeatherMakerCommandBufferManagerScript.Instance.GetCameraCommandBufferActions(camera, kvp.Key);
                if (actions != null && actions.Count > 0)
                {
                    renderer.EnqueuePass(kvp.Value);
                }
            }
        }

        //[Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            this.Renderer = renderer;
            WeatherMakerFullScreenEffect.urpRenderer = this;
            base.SetupRenderPasses(renderer, renderingData);
        }
#pragma warning restore CS0618

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            base.OnCameraPreCull(renderer, cameraData);
        }

        public override void Create()
        {
            commandBufferPassMap.Clear();
            foreach (var e in System.Enum.GetValues(typeof(CameraEvent)))
            {
                var evt = (CameraEvent)e;
                commandBufferPassMap[evt] = new ExecuteCommandBuffersPass(this, evt);
            }
        }

        protected override void Dispose(bool disposing)
        {
            commandBufferPassMap.Clear();
            base.Dispose(disposing);
        }
    }
}

#endif
