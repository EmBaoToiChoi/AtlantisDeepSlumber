//
// Weather Maker for Unity
// (c) 2016 Digital Ruby, LLC
// Source code may be used for personal or commercial projects.
// Source code may NOT be redistributed or sold.
// 
// *** A NOTE ABOUT PIRACY ***
// 
// If you got this asset from a pirate site, please consider buying it from the Unity asset store at https://assetstore.unity.com/packages/slug/60955?aid=1011lGnL. This asset is only legally available from the Unity Asset Store.
// 
// I'm a single indie dev supporting my family by spending hundreds and thousands of hours on this and other assets. It's very offensive, rude and just plain evil to steal when I (and many others) put so much hard work into the software.
// 
// Thank you.
//
// *** END NOTE ABOUT PIRACY ***
//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalRuby.WeatherMaker
{
    internal sealed class WeatherMakerDepthDownsampleManager : System.IDisposable
    {
        private sealed class CameraDepthState
        {
            public RenderTexture Half;
            public RenderTexture Quarter;
            public RenderTexture Eighth;
            public CommandBuffer CommandBuffer;
        }

        private readonly WeatherMakerCommandBufferManagerScript owner;
        private readonly HashSet<Camera> requests = new HashSet<Camera>();
        private readonly Dictionary<Camera, CameraDepthState> cameraStates = new Dictionary<Camera, CameraDepthState>();
#if UNITY_URP
        private readonly Dictionary<Camera, System.Action<WeatherMakerCommandBufferContext>> cameraActions = new Dictionary<Camera, System.Action<WeatherMakerCommandBufferContext>>();
#endif

        public WeatherMakerDepthDownsampleManager(WeatherMakerCommandBufferManagerScript owner)
        {
            this.owner = owner;
        }

        public void Request(Camera camera)
        {
            if (camera != null)
            {
                requests.Add(camera);
            }
        }

        public bool IsActive(Camera camera)
        {
            return (camera != null && cameraStates.ContainsKey(camera));
        }

        public void BindGlobals(CommandBuffer commandBuffer, Camera camera)
        {
            if (camera == null || !cameraStates.TryGetValue(camera, out CameraDepthState state) || state == null ||
                state.Half == null || state.Quarter == null || state.Eighth == null)
            {
                return;
            }

            RenderTextureDescriptor desc2 = state.Half.descriptor;
            RenderTextureDescriptor desc4 = state.Quarter.descriptor;
            RenderTextureDescriptor desc8 = state.Eighth.descriptor;
            SetSharedDepthGlobals(state, desc2, desc4, desc8, commandBuffer, commandBuffer != null);
        }

        public void SetupForCamera(Camera camera, Material depthDownsampleMaterial)
        {
            if (camera == null)
            {
                return;
            }

            bool needsDepthDownsample = (requests.Contains(camera) && depthDownsampleMaterial != null);
            if (!needsDepthDownsample)
            {
                ReleaseForCamera(camera);
                return;
            }

            if (!cameraStates.TryGetValue(camera, out CameraDepthState state) || state == null)
            {
                state = new CameraDepthState();
                cameraStates[camera] = state;
            }

            EnsureTargets(state, camera);
#if UNITY_URP
            cameraActions.TryGetValue(camera, out var oldAction);
            System.Action<WeatherMakerCommandBufferContext> newAction = (ctx) =>
            {
                if (ctx?.CommandBuffer == null || ctx.Camera == null || !cameraStates.TryGetValue(ctx.Camera, out CameraDepthState ctxState))
                {
                    return;
                }
                BuildCommands(ctx.CommandBuffer, ctx.Camera, depthDownsampleMaterial, ctxState, false, true);
            };
            cameraActions[camera] = newAction;
            owner.ReplaceCameraCommandBufferAction(oldAction, newAction, camera, CameraEvent.AfterDepthTexture);
#else
            if (state.CommandBuffer == null)
            {
                state.CommandBuffer = owner.GetOrCreateCommandBuffer();
            }
            BuildCommands(state.CommandBuffer, camera, depthDownsampleMaterial, state, true, true);
            var evt = camera.actualRenderingPath == RenderingPath.DeferredShading ? CameraEvent.BeforeReflections : CameraEvent.AfterDepthTexture;
            owner.ReplaceCameraCommandBuffer(state.CommandBuffer, camera, evt);
#endif
        }

        public void PostRender(Camera camera)
        {
            // Only clear the request flag. Keep RTs and command buffers alive so they
            // can be reused next frame without costly create/destroy churn.
            // SetupForCamera already calls ReleaseForCamera when a camera stops
            // requesting downsampling, and EnsureTargets handles resolution changes.
            if (camera != null)
            {
                requests.Remove(camera);
            }
        }

        public void ReleaseForCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

#if UNITY_URP
            if (cameraActions.TryGetValue(camera, out var action))
            {
                owner.RemoveCameraCommandBufferAction(action, camera, CameraEvent.AfterDepthTexture);
                cameraActions.Remove(camera);
            }
#else
            if (cameraStates.TryGetValue(camera, out CameraDepthState state) && state?.CommandBuffer != null)
            {
                owner.RemoveCameraCommandBuffer(state.CommandBuffer, camera, CameraEvent.AfterDepthTexture);
                owner.RemoveCameraCommandBuffer(state.CommandBuffer, camera, CameraEvent.BeforeReflections);
                state.CommandBuffer.Clear();
            }
#endif

            ReleaseTargets(camera);
            requests.Remove(camera);
        }

        public void CleanupDeadCameras()
        {
            var dead = new List<Camera>();
            foreach (var kv in cameraStates)
            {
                if (kv.Key == null)
                {
                    dead.Add(kv.Key);
                }
            }
            foreach (Camera camera in dead)
            {
                ReleaseForCamera(camera);
            }
        }

        public void Dispose()
        {
            var cameras = new List<Camera>(cameraStates.Keys);
            foreach (Camera camera in cameras)
            {
                ReleaseForCamera(camera);
            }

            cameraStates.Clear();
            requests.Clear();

#if UNITY_URP
            cameraActions.Clear();
#endif
        }

        private static bool IsTargetInvalid(RenderTexture tex, RenderTextureDescriptor desc)
        {
            return tex == null || tex.width != desc.width || tex.height != desc.height || tex.format != desc.colorFormat;
        }

        private static RenderTexture CreateDepthTarget(string name, RenderTextureDescriptor desc)
        {
            RenderTexture tex = WeatherMakerFullScreenEffect.CreateRenderTexture(desc, false, FilterMode.Point, TextureWrapMode.Clamp, false);
            tex.name = name;
            return tex;
        }

        private void EnsureTargets(CameraDepthState state, Camera camera)
        {
            RenderTextureDescriptor desc2 = WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(2, 1, 1, RenderTextureFormat.RFloat, 0, camera);
            RenderTextureDescriptor desc4 = WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(4, 1, 1, RenderTextureFormat.RFloat, 0, camera);
            RenderTextureDescriptor desc8 = WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(8, 1, 1, RenderTextureFormat.RFloat, 0, camera);
            desc2.msaaSamples = 1;
            desc4.msaaSamples = 1;
            desc8.msaaSamples = 1;
            desc2.sRGB = false;
            desc4.sRGB = false;
            desc8.sRGB = false;
            desc2.depthBufferBits = 0;
            desc4.depthBufferBits = 0;
            desc8.depthBufferBits = 0;

            if (IsTargetInvalid(state.Half, desc2))
            {
                state.Half = WeatherMakerFullScreenEffect.DestroyRenderTexture(state.Half);
                state.Half = CreateDepthTarget("WeatherMakerDepthHalf_" + camera.name, desc2);
            }
            if (IsTargetInvalid(state.Quarter, desc4))
            {
                state.Quarter = WeatherMakerFullScreenEffect.DestroyRenderTexture(state.Quarter);
                state.Quarter = CreateDepthTarget("WeatherMakerDepthQuarter_" + camera.name, desc4);
            }
            if (IsTargetInvalid(state.Eighth, desc8))
            {
                state.Eighth = WeatherMakerFullScreenEffect.DestroyRenderTexture(state.Eighth);
                state.Eighth = CreateDepthTarget("WeatherMakerDepthEighth_" + camera.name, desc8);
            }
        }

        private void ReleaseTargets(Camera camera)
        {
            if (!cameraStates.TryGetValue(camera, out CameraDepthState state) || state == null)
            {
                return;
            }

            state.Half = WeatherMakerFullScreenEffect.DestroyRenderTexture(state.Half);
            state.Quarter = WeatherMakerFullScreenEffect.DestroyRenderTexture(state.Quarter);
            state.Eighth = WeatherMakerFullScreenEffect.DestroyRenderTexture(state.Eighth);

#if !UNITY_URP
            if (state.CommandBuffer != null)
            {
                state.CommandBuffer.Clear();
                owner.ReturnCommandBufferToPool(state.CommandBuffer);
                state.CommandBuffer = null;
            }
#endif
            cameraStates.Remove(camera);
        }

        private static void BuildCommands(CommandBuffer commandBuffer, Camera camera, Material depthDownsampleMaterial,
            CameraDepthState state, bool clearFirst, bool useCommandBufferGlobals)
        {
            if (commandBuffer == null || camera == null || depthDownsampleMaterial == null || state == null ||
                state.Half == null || state.Quarter == null || state.Eighth == null)
            {
                return;
            }

            if (clearFirst)
            {
                commandBuffer.Clear();
                commandBuffer.name = "WeatherMakerSharedDepthDownsample_" + camera.name;
            }

            RenderTextureDescriptor desc1 = WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(1, 1, 1, RenderTextureFormat.RFloat, 0, camera);
            RenderTextureDescriptor desc2 = state.Half.descriptor;
            RenderTextureDescriptor desc4 = state.Quarter.descriptor;
            RenderTextureDescriptor desc8 = state.Eighth.descriptor;
            SetSharedDepthGlobals(state, desc2, desc4, desc8, commandBuffer, useCommandBufferGlobals);

            commandBuffer.SetGlobalVector(WMS._DepthTexelSizeSource, new Vector4(1.0f / desc1.width, 1.0f / desc1.height, desc1.width, desc1.height));
            commandBuffer.SetGlobalVector(WMS._DepthTexelSizeDest, new Vector4(1.0f / desc2.width, 1.0f / desc2.height, desc2.width, desc2.height));
            WeatherMakerFullScreenEffect.Blit(commandBuffer, WeatherMakerFullScreenEffect.CameraTargetIdentifier(), state.Half, depthDownsampleMaterial, 1);

            commandBuffer.SetGlobalVector(WMS._DepthTexelSizeSource, new Vector4(1.0f / desc2.width, 1.0f / desc2.height, desc2.width, desc2.height));
            commandBuffer.SetGlobalVector(WMS._DepthTexelSizeDest, new Vector4(1.0f / desc4.width, 1.0f / desc4.height, desc4.width, desc4.height));
            WeatherMakerFullScreenEffect.Blit(commandBuffer, WeatherMakerFullScreenEffect.CameraTargetIdentifier(), state.Quarter, depthDownsampleMaterial, 2);

            commandBuffer.SetGlobalVector(WMS._DepthTexelSizeSource, new Vector4(1.0f / desc4.width, 1.0f / desc4.height, desc4.width, desc4.height));
            commandBuffer.SetGlobalVector(WMS._DepthTexelSizeDest, new Vector4(1.0f / desc8.width, 1.0f / desc8.height, desc8.width, desc8.height));
            WeatherMakerFullScreenEffect.Blit(commandBuffer, WeatherMakerFullScreenEffect.CameraTargetIdentifier(), state.Eighth, depthDownsampleMaterial, 3);
            WeatherMakerFullScreenEffect.SetCameraTarget(commandBuffer);
        }

        private static void SetSharedDepthGlobals(CameraDepthState state, RenderTextureDescriptor desc2, RenderTextureDescriptor desc4,
            RenderTextureDescriptor desc8, CommandBuffer commandBuffer, bool useCommandBufferGlobals)
        {
            Vector4 st = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
            Vector4 halfTexelSize = new Vector4(1.0f / desc2.width, 1.0f / desc2.height, desc2.width, desc2.height);
            Vector4 quarterTexelSize = new Vector4(1.0f / desc4.width, 1.0f / desc4.height, desc4.width, desc4.height);
            Vector4 eighthTexelSize = new Vector4(1.0f / desc8.width, 1.0f / desc8.height, desc8.width, desc8.height);
            if (useCommandBufferGlobals && commandBuffer != null)
            {
                commandBuffer.SetGlobalTexture(WMS._CameraDepthTextureHalf, state.Half);
                commandBuffer.SetGlobalTexture(WMS._CameraDepthTextureQuarter, state.Quarter);
                commandBuffer.SetGlobalTexture(WMS._CameraDepthTextureEighth, state.Eighth);
                commandBuffer.SetGlobalVector(WMS._CameraDepthTextureHalfST, st);
                commandBuffer.SetGlobalVector(WMS._CameraDepthTextureQuarterST, st);
                commandBuffer.SetGlobalVector(WMS._CameraDepthTextureEighthST, st);
                commandBuffer.SetGlobalVector(WMS._CameraDepthTextureHalfTexelSize, halfTexelSize);
                commandBuffer.SetGlobalVector(WMS._CameraDepthTextureQuarterTexelSize, quarterTexelSize);
                commandBuffer.SetGlobalVector(WMS._CameraDepthTextureEighthTexelSize, eighthTexelSize);
            }
            else
            {
                Shader.SetGlobalTexture(WMS._CameraDepthTextureHalf, state.Half);
                Shader.SetGlobalTexture(WMS._CameraDepthTextureQuarter, state.Quarter);
                Shader.SetGlobalTexture(WMS._CameraDepthTextureEighth, state.Eighth);
                Shader.SetGlobalVector(WMS._CameraDepthTextureHalfST, st);
                Shader.SetGlobalVector(WMS._CameraDepthTextureQuarterST, st);
                Shader.SetGlobalVector(WMS._CameraDepthTextureEighthST, st);
                Shader.SetGlobalVector(WMS._CameraDepthTextureHalfTexelSize, halfTexelSize);
                Shader.SetGlobalVector(WMS._CameraDepthTextureQuarterTexelSize, quarterTexelSize);
                Shader.SetGlobalVector(WMS._CameraDepthTextureEighthTexelSize, eighthTexelSize);
            }
        }
    }
}
