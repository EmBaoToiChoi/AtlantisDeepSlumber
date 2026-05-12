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

#if !UNITY_URP

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalRuby.WeatherMaker
{
    public partial class WeatherMakerCommandBufferManagerScript
    {
        private void RegisterRenderCallbacks()
        {
            Camera.onPreCull += CameraPreCull;
            Camera.onPreRender += CameraPreRender;
            Camera.onPostRender += CameraPostRender;
        }

        private void UnregisterRenderCallbacks()
        {
            Camera.onPreCull -= CameraPreCull;
            Camera.onPreRender -= CameraPreRender;
            Camera.onPostRender -= CameraPostRender;
        }

        private void DetachCameraCommandBuffer(WeatherMakerCommandBuffer commandBuffer)
        {
            commandBuffer.Camera.RemoveCommandBuffer(commandBuffer.RenderQueue, commandBuffer.CommandBuffer);
        }

        private void RenderCameraInternal(Camera camera)
        {
            camera.Render();
        }

        private void AddCameraCommandBufferInternal(CommandBuffer commandBuffer, Camera camera, CameraEvent cameraEvent)
        {
            camera.RemoveCommandBuffer(cameraEvent, commandBuffer);
            camera.AddCommandBuffer(cameraEvent, commandBuffer);
        }

        private void RemoveCameraCommandBufferInternal(CommandBuffer commandBuffer, Camera camera, CameraEvent cameraEvent)
        {
            camera.RemoveCommandBuffer(cameraEvent, commandBuffer);
        }

        private IReadOnlyList<CommandBuffer> GetCameraCommandBuffersInternal(Camera camera, CameraEvent cameraEvent)
        {
            return camera.GetCommandBuffers(cameraEvent);
        }

        private void RemoveEmptyCommandBuffersInternal(Camera camera)
        {
            if ((commandBufferCheckTimer += Time.deltaTime) < 1.0f)
            {
                return;
            }

            commandBufferCheckTimer = 0.0f;
            foreach (CameraEvent evt in System.Enum.GetValues(typeof(CameraEvent)))
            {
                CommandBuffer[] cmdBuffers = camera.GetCommandBuffers(evt);
                foreach (CommandBuffer cmdBuffer in cmdBuffers)
                {
                    if (cmdBuffer.sizeInBytes == 0)
                    {
                        camera.RemoveCommandBuffer(evt, cmdBuffer);
                    }
                }
            }
        }
    }
}

#endif
