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

#if UNITY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace DigitalRuby.WeatherMaker
{
    public partial class WeatherMakerFullScreenEffect
    {
#if UNITY_URP
        /// <summary>
        /// Tracks the previously registered action per camera so we can remove only our own action.
        /// </summary>
        private readonly Dictionary<Camera, System.Action<WeatherMakerCommandBufferContext>> previousActions =
            new Dictionary<Camera, System.Action<WeatherMakerCommandBufferContext>>();

        internal static WeatherMakerURPRenderFeatureScript urpRenderer;

        [System.ThreadStatic]
        internal static RTHandle currentURPColorTarget;

        [System.ThreadStatic]
        internal static RTHandle currentURPDepthTarget;
#endif
    }
}
