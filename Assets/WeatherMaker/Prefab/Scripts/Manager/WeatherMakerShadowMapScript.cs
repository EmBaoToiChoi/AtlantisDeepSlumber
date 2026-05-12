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

using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;

using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalRuby.WeatherMaker
{
    /// <summary>
    /// Shadow map generator script, add to a dir light
    /// </summary>
    [RequireComponent(typeof(Light))]
    [ExecuteInEditMode]
    public class WeatherMakerShadowMapScript : MonoBehaviour
    {
        /// <summary>Optional material to add cloud shadows to the shadow map, null for no cloud shadows.</summary>
        [Tooltip("Optional material to add cloud shadows to the shadow map, null for no cloud shadows.")]
        public Material CloudShadowMaterial;

        /// <summary>Gaussian blur material.</summary>
        [Tooltip("Gaussian blur material.")]
        public Material BlurMaterial;

        private Light _light;

#if UNITY_URP

        // Forward and deferred require different injection points for screen-space shadow composition.
        private readonly CameraEvent urpEventScreenSpaceShadowsForward = CameraEvent.BeforeForwardOpaque;
        private readonly CameraEvent urpEventScreenSpaceShadowsDeferred = CameraEvent.BeforeGBuffer;

#endif

        private List<RenderTexture> _tempShadowBuffers;

        private struct CommandBufferNameCacheEntry
        {
            public string Prefix { get; set; }
            public Camera Camera { get; set; }
            public string Name { get; set; }
        }
        private List<CommandBufferNameCacheEntry> commandBufferNameCache = new List<CommandBufferNameCacheEntry>();

#if !UNITY_URP

        private CommandBuffer commandBufferDepthShadows;
        private Dictionary<Camera, CommandBuffer> commandBufferScreenSpaceShadows1 = new Dictionary<Camera, CommandBuffer>();
        private Dictionary<Camera, CommandBuffer> commandBufferScreenSpaceShadows2 = new Dictionary<Camera, CommandBuffer>();

        private void AddLightCommandBuffer(CommandBuffer commandBuffer, LightEvent lightEvent)
        {
            if (commandBuffer != null && _light != null)
            {
                RemoveLightCommandBuffer(commandBuffer, lightEvent);
                _light.AddCommandBuffer(lightEvent, commandBuffer);
            }
        }

        private void RemoveLightCommandBuffer(CommandBuffer commandBuffer, LightEvent evt)
        {
            if (commandBuffer != null && _light != null)
            {
                _light.RemoveCommandBuffer(evt, commandBuffer);
            }
        }

        /// <summary>
        /// Initiate cleanup of command buffers
        /// </summary>
        public void CleanupCommandBuffers()
        {
            RemoveLightCommandBuffer(commandBufferDepthShadows, LightEvent.AfterShadowMap);
            if (commandBufferDepthShadows != null)
            {
                commandBufferDepthShadows.Clear();
                commandBufferDepthShadows.Release();
                commandBufferDepthShadows = null;
            }
            if (WeatherMakerCommandBufferManagerScript.Instance != null)
            {
                foreach (var cb in commandBufferScreenSpaceShadows1.Values)
                {
                    RemoveLightCommandBuffer(cb, LightEvent.AfterScreenspaceMask);
                    WeatherMakerCommandBufferManagerScript.Instance.ReturnCommandBufferToPool(cb);
                }
                foreach (var cb in commandBufferScreenSpaceShadows2.Values)
                {
                    RemoveLightCommandBuffer(cb, LightEvent.AfterScreenspaceMask);
                    WeatherMakerCommandBufferManagerScript.Instance.ReturnCommandBufferToPool(cb);
                }
            }
            commandBufferScreenSpaceShadows1.Clear();
            commandBufferScreenSpaceShadows2.Clear();
        }

#endif

        private void AddScreenSpaceShadowsCommandBuffer(Camera camera)
        {
            if (CloudShadowMaterial != null &&
                _light != null &&
                _light.type == LightType.Directional &&
                _light.shadows != LightShadows.None &&
                WeatherMakerCommandBufferManagerScript.Instance != null &&
                WeatherMakerLightManagerScript.Instance != null &&
                WeatherMakerLightManagerScript.ScreenSpaceShadowMode != BuiltinShaderMode.Disabled &&
                WeatherMakerScript.Instance != null &&
                WeatherMakerScript.Instance.PerformanceProfile != null &&
                WeatherMakerScript.Instance.PerformanceProfile.VolumetricCloudShadowDownsampleScale != WeatherMakerDownsampleScale.Disabled &&
                WeatherMakerScript.Instance.PerformanceProfile.VolumetricCloudShadowSampleCount > 0)
            {

#if UNITY_URP

                if (!string.IsNullOrWhiteSpace(WeatherMakerLightManagerScript.Instance.ScreenSpaceShadowsRenderTextureName))
                {
                    CameraEvent urpEventScreenSpaceShadows = (camera.actualRenderingPath == RenderingPath.DeferredShading)
                        ? urpEventScreenSpaceShadowsDeferred
                        : urpEventScreenSpaceShadowsForward;

                    var ssTexName = WeatherMakerLightManagerScript.Instance.ScreenSpaceShadowsRenderTextureName;
                    var cloudMat = CloudShadowMaterial;
                    var blurMat = BlurMaterial;
                    var tempBuffer = GetTempShadowBuffer(1);
                    var scale = (int)WeatherMakerScript.Instance.PerformanceProfile.VolumetricCloudShadowDownsampleScale;

                    if (tempBuffer != null && cloudMat != null && blurMat != null)
                    {
                        WeatherMakerCommandBufferManagerScript.Instance.AddCameraCommandBufferAction((ctx) =>
                        {
                            var cmd = ctx.CommandBuffer;
                            cmd.GetTemporaryRT(WMS._MainTex, WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(scale, 0, 1, tempBuffer.format, tempBuffer.depth, camera));
                            WeatherMakerFullScreenEffect.Blit(cmd, BuiltinRenderTextureType.CameraTarget, WMS._MainTex, cloudMat, 0);
                            cmd.SetGlobalFloat(WMS._WeatherMakerAdjustFullScreenUVStereoDisable, 1.0f);
                            WeatherMakerFullScreenEffect.Blit(cmd, WMS._MainTex, tempBuffer, blurMat, 0);
                            cmd.SetGlobalFloat(WMS._WeatherMakerAdjustFullScreenUVStereoDisable, 0.0f);
                            cmd.ReleaseTemporaryRT(WMS._MainTex);
                            cmd.SetGlobalTexture(ssTexName, tempBuffer);
                            cmd.SetGlobalTexture(WMS._ScreenSpaceShadowmapTexture, tempBuffer);
                        }, camera, urpEventScreenSpaceShadows);
                    }
                }

#else

                commandBufferDepthShadows ??= new CommandBuffer
                {
                    name = "WeatherMakerShadowMapDepthShadowScript_" + gameObject.name
                };
                commandBufferDepthShadows.Clear();
                commandBufferDepthShadows.SetGlobalTexture(WMS._WeatherMakerShadowMapTexture, BuiltinRenderTextureType.CurrentActive);
                int shadowRes = _light.shadowCustomResolution > 0
                    ? _light.shadowCustomResolution
                    : (_light.shadowResolution == LightShadowResolution.FromQualitySettings ? QualitySettings.shadowResolution : (ShadowResolution)(int)_light.shadowResolution) switch
                    {
                        ShadowResolution.Low => 1024,
                        ShadowResolution.Medium => 2048,
                        ShadowResolution.High => 4096,
                        _ => 4096
                    };
                float invShadowRes = 1.0f / shadowRes;
                commandBufferDepthShadows.SetGlobalVector(WMS._WeatherMakerShadowMapTexture_TexelSize, new Vector4(invShadowRes, invShadowRes, shadowRes, shadowRes));
                AddLightCommandBuffer(commandBufferDepthShadows, LightEvent.AfterShadowMap);

                if (!commandBufferScreenSpaceShadows1.TryGetValue(camera, out var _commandBufferScreenSpaceShadows1))
                {
                    _commandBufferScreenSpaceShadows1 = WeatherMakerCommandBufferManagerScript.Instance.GetOrCreateCommandBuffer();
                    _commandBufferScreenSpaceShadows1.name = "WeatherMakerShadowMapScreensSpaceShadowScriptCloudShadows_" + camera.name;
                    commandBufferScreenSpaceShadows1[camera] = _commandBufferScreenSpaceShadows1;
                }
                _commandBufferScreenSpaceShadows1.Clear();
                if (!commandBufferScreenSpaceShadows2.TryGetValue(camera, out var _commandBufferScreenSpaceShadows2))
                {
                    _commandBufferScreenSpaceShadows2 = WeatherMakerCommandBufferManagerScript.Instance.GetOrCreateCommandBuffer();
                    _commandBufferScreenSpaceShadows2.name = "WeatherMakerShadowMapScreensSpaceShadowScriptBlur_" + camera.name;
                    commandBufferScreenSpaceShadows2[camera] = _commandBufferScreenSpaceShadows2;
                }
                _commandBufferScreenSpaceShadows2.Clear();

                if (!string.IsNullOrWhiteSpace(WeatherMakerLightManagerScript.Instance.ScreenSpaceShadowsRenderTextureName))
                {
                    // render screen space shadows with cloud shadows
                    var tempShadowBuffer2 = GetTempShadowBuffer(1);
                    _commandBufferScreenSpaceShadows1.SetGlobalTexture(WeatherMakerLightManagerScript.Instance.ScreenSpaceShadowsRenderTextureName, BuiltinRenderTextureType.CurrentActive);
                    _commandBufferScreenSpaceShadows1.SetGlobalTexture(WMS._ScreenSpaceShadowmapTexture, BuiltinRenderTextureType.CurrentActive);
                    WeatherMakerFullScreenEffect.Blit(_commandBufferScreenSpaceShadows1, BuiltinRenderTextureType.CurrentActive, tempShadowBuffer2, CloudShadowMaterial, 0);

                    // no concept of stereo here, so disable the adjustment
                    _commandBufferScreenSpaceShadows2.SetGlobalFloat(WMS._WeatherMakerAdjustFullScreenUVStereoDisable, 1.0f);

                    // render the cloud shadowed screen space shadow map with a blur to smooth out the cloud shadows, additively blended on top of the original screen space shadow map
                    WeatherMakerFullScreenEffect.Blit(_commandBufferScreenSpaceShadows2, tempShadowBuffer2, BuiltinRenderTextureType.CurrentActive, BlurMaterial, 0);
                    _commandBufferScreenSpaceShadows2.SetGlobalTexture(WeatherMakerLightManagerScript.Instance.ScreenSpaceShadowsRenderTextureName, BuiltinRenderTextureType.CurrentActive);
                    _commandBufferScreenSpaceShadows2.SetGlobalTexture(WMS._ScreenSpaceShadowmapTexture, BuiltinRenderTextureType.CurrentActive);

                    // must be set back to 0 after the blit
                    _commandBufferScreenSpaceShadows2.SetGlobalFloat(WMS._WeatherMakerAdjustFullScreenUVStereoDisable, 0.0f);

                    AddLightCommandBuffer(_commandBufferScreenSpaceShadows1, LightEvent.AfterScreenspaceMask);
                    AddLightCommandBuffer(_commandBufferScreenSpaceShadows2, LightEvent.AfterScreenspaceMask);
                }

#endif

            }
        }

        private void Update()
        {
            // ensure that the any shader using cloud shadows knows the correct cloud shadow parameters
            if (CloudShadowMaterial != null)
            {
                Shader.SetGlobalFloat(WMS._CloudShadowMapAdder, CloudShadowMaterial.GetFloat(WMS._CloudShadowMapAdder));
                Shader.SetGlobalFloat(WMS._CloudShadowMapMultiplier, CloudShadowMaterial.GetFloat(WMS._CloudShadowMapMultiplier));
                Shader.SetGlobalFloat(WMS._CloudShadowMapPower, CloudShadowMaterial.GetFloat(WMS._CloudShadowMapPower));
                Shader.SetGlobalFloat(WMS._WeatherMakerCloudVolumetricShadowDither, CloudShadowMaterial.GetFloat(WMS._WeatherMakerCloudVolumetricShadowDither));
                Shader.SetGlobalTexture(WMS._WeatherMakerCloudShadowDetailTexture, CloudShadowMaterial.GetTexture(WMS._WeatherMakerCloudShadowDetailTexture));
                Shader.SetGlobalFloat(WMS._WeatherMakerCloudShadowDetailScale, CloudShadowMaterial.GetFloat(WMS._WeatherMakerCloudShadowDetailScale));
                Shader.SetGlobalFloat(WMS._WeatherMakerCloudShadowDetailIntensity, CloudShadowMaterial.GetFloat(WMS._WeatherMakerCloudShadowDetailIntensity));
                Shader.SetGlobalFloat(WMS._WeatherMakerCloudShadowDetailFalloff, CloudShadowMaterial.GetFloat(WMS._WeatherMakerCloudShadowDetailFalloff));
                Shader.SetGlobalFloat(WMS._WeatherMakerCloudShadowDistanceFade, CloudShadowMaterial.GetFloat(WMS._WeatherMakerCloudShadowDistanceFade));
                CloudShadowMaterial.SetFloat(WMS._BlendOp, (float)BlendOp.Add);
                CloudShadowMaterial.SetFloat(WMS._SrcBlendMode, (float)BlendMode.One);
                CloudShadowMaterial.SetFloat(WMS._DstBlendMode, (float)BlendMode.Zero);
                BlurMaterial.SetFloat(WMS._BlendOp, (float)BlendOp.Add);
                BlurMaterial.SetFloat(WMS._SrcBlendMode, (float)BlendMode.One);
                BlurMaterial.SetFloat(WMS._DstBlendMode, (float)BlendMode.Zero);
            }
        }

        private void OnEnable()
        {
            _tempShadowBuffers = new List<RenderTexture>();
            _light = GetComponent<Light>();
            if (WeatherMakerCommandBufferManagerScript.Instance != null)
            {
                WeatherMakerCommandBufferManagerScript.Instance.RegisterPreCull(CameraPreCull, this);
                WeatherMakerCommandBufferManagerScript.Instance.RegisterPreRender(CameraPreRender, this);
                WeatherMakerCommandBufferManagerScript.Instance.RegisterPostRender(CameraPostRender, this);
            }
        }

        private void OnDisable()
        {

#if !UNITY_URP
            CleanupCommandBuffers();
#endif

        }

        private void OnDestroy()
        {
            if (WeatherMakerCommandBufferManagerScript.Instance != null)
            {
                WeatherMakerCommandBufferManagerScript.Instance.UnregisterPreCull(this);
                WeatherMakerCommandBufferManagerScript.Instance.UnregisterPreRender(this);
                WeatherMakerCommandBufferManagerScript.Instance.UnregisterPostRender(this);
            }
        }

        private RenderTexture PushTempShadowBuffer(Camera camera)
        {
            int scale = (int)WeatherMakerScript.Instance.PerformanceProfile.VolumetricCloudShadowDownsampleScale;
            RenderTextureFormat format = RenderTextureFormat.R8;
            return PushTempShadowBuffer(camera, scale, format);
        }

        private RenderTexture PushTempShadowBuffer(Camera camera, int scale, RenderTextureFormat format)
        {
            RenderTexture tempShadowBuffer = RenderTexture.GetTemporary(WeatherMakerFullScreenEffect.GetRenderTextureDescriptor(scale, 0, 1,
                format, 0, camera));
            tempShadowBuffer.wrapMode = TextureWrapMode.Clamp;
            tempShadowBuffer.filterMode = FilterMode.Bilinear;
            _tempShadowBuffers.Add(tempShadowBuffer);
            return tempShadowBuffer;
        }

        private RenderTexture GetTempShadowBuffer(int history = 1)
        {
            if (_tempShadowBuffers.Count == 0)
            {
                return null;
            }
            return _tempShadowBuffers[_tempShadowBuffers.Count - history];
        }

        private void PopTempShadowBuffer()
        {
            if (_tempShadowBuffers.Count == 0)
            {
                return;
            }
            RenderTexture tempShadowBuffer = _tempShadowBuffers[_tempShadowBuffers.Count - 1];
            _tempShadowBuffers.RemoveAt(_tempShadowBuffers.Count - 1);
            RenderTexture.ReleaseTemporary(tempShadowBuffer);
        }

        private void CameraPreCull(Camera camera)
        {
            if (WeatherMakerScript.Instance == null || WeatherMakerScript.Instance.PerformanceProfile == null)
            {
                return;
            }

            WeatherMakerCameraType cameraType = WeatherMakerScript.GetCameraType(camera);
            if ((WeatherMakerCommandBufferManagerScript.CameraStackCount == 1 && cameraType == WeatherMakerCameraType.Normal) ||
                ((cameraType == WeatherMakerCameraType.Reflection || cameraType == WeatherMakerCameraType.CubeMap)
                && WeatherMakerScript.Instance.PerformanceProfile.VolumetricCloudReflectionShadows))
            {
                PushTempShadowBuffer(camera);
                AddScreenSpaceShadowsCommandBuffer(camera);
            }

        }

        private void CameraPreRender(Camera camera)
        {
        }

        private void CameraPostRender(Camera camera)
        {
            if (WeatherMakerScript.Instance == null || WeatherMakerScript.Instance.PerformanceProfile == null)
            {
                return;
            }

            WeatherMakerCameraType cameraType = WeatherMakerScript.GetCameraType(camera);
            if ((WeatherMakerCommandBufferManagerScript.CameraStackCount == 0 && cameraType == WeatherMakerCameraType.Normal) ||
                ((cameraType == WeatherMakerCameraType.Reflection || cameraType == WeatherMakerCameraType.CubeMap)
                && WeatherMakerScript.Instance.PerformanceProfile.VolumetricCloudReflectionShadows))
            {
                PopTempShadowBuffer();

#if UNITY_URP

                // remove all actions for this camera's shadow events
                // they will be re-added next frame in CameraPreCull
                if (WeatherMakerCommandBufferManagerScript.Instance != null)
                {
                    // clear all actions for these camera events for this camera
                    var ssActionsForward = WeatherMakerCommandBufferManagerScript.Instance.GetCameraCommandBufferActions(camera, urpEventScreenSpaceShadowsForward);
                    if (ssActionsForward is List<System.Action<WeatherMakerCommandBufferContext>> ssForwardList)
                    {
                        ssForwardList.Clear();
                    }
                    var ssActionsDeferred = WeatherMakerCommandBufferManagerScript.Instance.GetCameraCommandBufferActions(camera, urpEventScreenSpaceShadowsDeferred);
                    if (ssActionsDeferred is List<System.Action<WeatherMakerCommandBufferContext>> ssDeferredList)
                    {
                        ssDeferredList.Clear();
                    }
                }

#else

                RemoveLightCommandBuffer(commandBufferDepthShadows, LightEvent.AfterShadowMap);
                if (commandBufferScreenSpaceShadows1.TryGetValue(camera, out var _commandBufferScreenSpaceShadows1))
                {
                    RemoveLightCommandBuffer(_commandBufferScreenSpaceShadows1, LightEvent.AfterScreenspaceMask);
                }
                if (commandBufferScreenSpaceShadows2.TryGetValue(camera, out var _commandBufferScreenSpaceShadows2))
                {
                    RemoveLightCommandBuffer(_commandBufferScreenSpaceShadows2, LightEvent.AfterScreenspaceMask);
                }

#endif

            }
        }
    }
}
