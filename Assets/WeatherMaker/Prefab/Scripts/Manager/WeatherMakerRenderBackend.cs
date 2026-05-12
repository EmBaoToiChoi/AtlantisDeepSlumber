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

namespace DigitalRuby.WeatherMaker
{
    /// <summary>
    /// Rendering backend used by Weather Maker.
    /// </summary>
    public enum WeatherMakerRenderBackend
    {
        Builtin,
        URP
    }

    /// <summary>
    /// Render backend helpers for capability and routing checks.
    /// </summary>
    public static class WeatherMakerRenderBackendUtility
    {
        /// <summary>
        /// Active compile-time render backend.
        /// </summary>
        public static WeatherMakerRenderBackend Backend
        {
            get
            {
#if UNITY_URP
                return WeatherMakerRenderBackend.URP;
#else
                return WeatherMakerRenderBackend.Builtin;
#endif
            }
        }

        /// <summary>
        /// Whether URP backend code is active.
        /// </summary>
        public static bool IsURP => Backend == WeatherMakerRenderBackend.URP;
    }
}
