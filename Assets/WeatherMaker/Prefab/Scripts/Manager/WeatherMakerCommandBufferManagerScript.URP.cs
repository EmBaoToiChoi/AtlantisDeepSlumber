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

#if UNITY_URP

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace DigitalRuby.WeatherMaker
{
    public partial class WeatherMakerCommandBufferManagerScript
    {
        private readonly Dictionary<Camera, Dictionary<CameraEvent, List<CommandBuffer>>> cameraCommandBuffers = new();
        private readonly Dictionary<Camera, Dictionary<CameraEvent, List<System.Action<WeatherMakerCommandBufferContext>>>> cameraCommandBufferActions = new();
        private ScriptableRenderContext currentUrpContext;

        private void RegisterRenderCallbacks()
        {
            RenderPipelineManager.beginCameraRendering += CameraBeginRendering;
            RenderPipelineManager.endCameraRendering += CameraEndRendering;
            RenderPipelineManager.beginContextRendering += CameraBeginContextRendering;
            RenderPipelineManager.endContextRendering += CameraEndContextRendering;
        }

        private void UnregisterRenderCallbacks()
        {
            RenderPipelineManager.beginCameraRendering -= CameraBeginRendering;
            RenderPipelineManager.endCameraRendering -= CameraEndRendering;
            RenderPipelineManager.beginContextRendering -= CameraBeginContextRendering;
            RenderPipelineManager.endContextRendering -= CameraEndContextRendering;
        }

        private void DetachCameraCommandBuffer(WeatherMakerCommandBuffer commandBuffer)
        {
            // URP command buffers are tracked and executed via render-feature actions.
            RemoveCameraCommandBufferInternal(commandBuffer.CommandBuffer, null, commandBuffer.RenderQueue);
        }

        private void RenderCameraInternal(Camera camera)
        {
            CameraPreCull(camera);
            CameraPreRender(camera);
            UnityEngine.Rendering.Universal.UniversalRenderPipeline.SubmitRenderRequest(
                camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest());
            CameraPostRender(camera);
        }

        private void AddCameraCommandBufferInternal(CommandBuffer commandBuffer, Camera camera, CameraEvent cameraEvent)
        {
            if (!cameraCommandBuffers.TryGetValue(camera, out var dict))
            {
                dict = new Dictionary<CameraEvent, List<CommandBuffer>>();
                cameraCommandBuffers[camera] = dict;
            }
            if (!dict.TryGetValue(cameraEvent, out var list))
            {
                list = new List<CommandBuffer>();
                dict[cameraEvent] = list;
            }
            if (!list.Contains(commandBuffer))
            {
                list.Add(commandBuffer);
            }
        }

        private void RemoveCameraCommandBufferInternal(CommandBuffer commandBuffer, Camera camera, CameraEvent cameraEvent)
        {
            if (camera is null)
            {
                foreach (var dict in cameraCommandBuffers.Values)
                {
                    if (dict.TryGetValue(cameraEvent, out var list))
                    {
                        list.Remove(commandBuffer);
                    }
                }
            }
            else if (cameraCommandBuffers.TryGetValue(camera, out var dict) &&
                dict.TryGetValue(cameraEvent, out var cameraEventCommands))
            {
                cameraEventCommands.Remove(commandBuffer);
                if (cameraEventCommands.Count == 0)
                {
                    dict.Remove(cameraEvent);
                }
            }
        }

        private IReadOnlyList<CommandBuffer> GetCameraCommandBuffersInternal(Camera camera, CameraEvent cameraEvent)
        {
            if (cameraCommandBuffers.TryGetValue(camera, out var dict) &&
                dict.TryGetValue(cameraEvent, out var list))
            {
                return list;
            }
            return System.Array.Empty<CommandBuffer>();
        }

        private void RemoveEmptyCommandBuffersInternal(Camera camera)
        {
        }

        private void CameraBeginRendering(ScriptableRenderContext context, Camera camera)
        {
            CameraPreCull(camera);
            CameraPreRender(camera);
        }

        private void CameraEndRendering(ScriptableRenderContext context, Camera camera)
        {
            CameraPostRender(camera);
        }

        private void CameraBeginContextRendering(ScriptableRenderContext ctx, List<Camera> cameras)
        {
            currentUrpContext = ctx;
        }

        private void CameraEndContextRendering(ScriptableRenderContext ctx, List<Camera> cameras)
        {
        }

        private void CameraBeginFrameRendering(ScriptableRenderContext ctx, Camera[] cameras)
        {
            currentUrpContext = ctx;
        }

        private void CameraEndFrameRendering(ScriptableRenderContext ctx, Camera[] cameras)
        {
        }

        /// <summary>
        /// Add an action to be executed during URP render pass for a camera at a specific event.<br/>
        /// The action receives a WeatherMakerCommandBufferContext with the correct URP render targets.
        /// </summary>
        /// <param name="action">Action to execute during render pass</param>
        /// <param name="camera">Camera</param>
        /// <param name="cameraEvent">Camera event</param>
        public void AddCameraCommandBufferAction(System.Action<WeatherMakerCommandBufferContext> action, Camera camera, CameraEvent cameraEvent)
        {
            if (!cameraCommandBufferActions.TryGetValue(camera, out var dict))
            {
                dict = new Dictionary<CameraEvent, List<System.Action<WeatherMakerCommandBufferContext>>>();
                cameraCommandBufferActions[camera] = dict;
            }
            if (!dict.TryGetValue(cameraEvent, out var list))
            {
                list = new List<System.Action<WeatherMakerCommandBufferContext>>();
                dict[cameraEvent] = list;
            }
            if (!list.Contains(action))
            {
                list.Add(action);
            }
        }

        /// <summary>
        /// Remove an action from a camera
        /// </summary>
        /// <param name="action">Action to remove</param>
        /// <param name="camera">Camera, null to remove from all cameras</param>
        /// <param name="cameraEvent">Camera event</param>
        public void RemoveCameraCommandBufferAction(System.Action<WeatherMakerCommandBufferContext> action, Camera camera, CameraEvent cameraEvent)
        {
            if (camera is null)
            {
                foreach (var dict in cameraCommandBufferActions.Values)
                {
                    if (dict.TryGetValue(cameraEvent, out var list))
                    {
                        list.Remove(action);
                    }
                }
            }
            else if (cameraCommandBufferActions.TryGetValue(camera, out var dict) &&
                dict.TryGetValue(cameraEvent, out var list))
            {
                list.Remove(action);
                if (list.Count == 0)
                {
                    dict.Remove(cameraEvent);
                }
            }
        }

        /// <summary>
        /// Replace camera action atomically for a camera/event pair.
        /// </summary>
        /// <param name="oldAction">Old action, null to skip removal</param>
        /// <param name="newAction">New action, null to skip add</param>
        /// <param name="camera">Camera</param>
        /// <param name="cameraEvent">Camera event</param>
        public void ReplaceCameraCommandBufferAction(System.Action<WeatherMakerCommandBufferContext> oldAction,
            System.Action<WeatherMakerCommandBufferContext> newAction, Camera camera, CameraEvent cameraEvent)
        {
            if (oldAction != null)
            {
                RemoveCameraCommandBufferAction(oldAction, camera, cameraEvent);
            }
            if (newAction != null)
            {
                AddCameraCommandBufferAction(newAction, camera, cameraEvent);
            }
        }

        /// <summary>
        /// Get command buffer actions for a camera at a specific event
        /// </summary>
        /// <param name="camera">Camera</param>
        /// <param name="cameraEvent">Camera event</param>
        /// <returns>List of actions or empty</returns>
        public IReadOnlyList<System.Action<WeatherMakerCommandBufferContext>> GetCameraCommandBufferActions(Camera camera, CameraEvent cameraEvent)
        {
            if (cameraCommandBufferActions.TryGetValue(camera, out var dict) &&
                dict.TryGetValue(cameraEvent, out var list))
            {
                return list;
            }
            return System.Array.Empty<System.Action<WeatherMakerCommandBufferContext>>();
        }
    }
}

#endif
