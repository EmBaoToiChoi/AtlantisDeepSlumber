//
// Weather Maker for Unity
// (c) 2016 Digital Ruby, LLC
// Source code may be used for personal or commercial projects.
// Source code may NOT be redistributed or sold.
//

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DigitalRuby.WeatherMaker
{
    /// <summary>
    /// Menu commands to patch/unpatch Crest ocean shaders with WeatherMaker cloud shadow support.
    /// Menu items are grayed out when the Crest package is not installed.
    /// </summary>
    static class WeatherMakerCrestCloudShadows
    {
        const string k_EnableMenu  = "Window/Weather Maker/Integrations/Crest/Enable Crest Cloud Shadows";
        const string k_DisableMenu = "Window/Weather Maker/Integrations/Crest/Disable Crest Cloud Shadows";
        const string k_CrestPackageId = "com.waveharmonic.crest";

        // Marker used to detect whether the patch is already applied.
        const string k_PatchMarker = "CloudShadow.hlsl";

        // --- Fragment.hlsl injection anchors ---
        const string k_IncludeAnchor =
            "#include \"Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Fog.hlsl\"";
        const string k_IncludeLine =
            "#include \"Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/CloudShadow.hlsl\"";

        const string k_ShadowAnchor =
            "    shadow = 1.0 - shadow;";
        const string k_ShadowPatch =
            "\n" +
            "    // Apply cloud shadows from WeatherMaker.\n" +
            "    {\n" +
            "        half cloudShadow = SampleCloudShadow(i_PositionWS, g_Crest_PrimaryLightDirection);\n" +
            "        shadow *= cloudShadow;\n" +
            "    }";

        // --- Content written to Crest package on Enable ---
        static readonly string k_CloudShadowHlsl = string.Join("\n", new[]
        {
            "// Cloud shadow integration for WeatherMaker.",
            "// Samples the global cloud shadow texture and returns attenuation (1 = lit, 0 = fully shadowed).",
            "",
            "#ifndef CREST_WATER_CLOUD_SHADOW_H",
            "#define CREST_WATER_CLOUD_SHADOW_H",
            "",
            "uniform float _WeatherMakerCloudShadowEnabled;",
            "TEXTURE2D(_WeatherMakerCloudShadowTexture);",
            "SAMPLER(sampler_WeatherMakerCloudShadowTexture);",
            "uniform float3 _WeatherMakerCameraOriginOffset;",
            "",
            "half SampleCloudShadow(float3 positionWS, float3 lightDir)",
            "{",
            "    if (_WeatherMakerCloudShadowEnabled < 0.5)",
            "        return 1.0;",
            "",
            "    lightDir.y = max(0.05, lightDir.y);",
            "    float offsetMultiplier = max(0.0, positionWS.y) / max(0.001, lightDir.y);",
            "    float2 offset = lightDir.xz * offsetMultiplier;",
            "    float2 shadowUV = positionWS.xz - offset - _WeatherMakerCameraOriginOffset.xz;",
            "    shadowUV *= _ProjectionParams.w * 0.5;",
            "    shadowUV += 0.5;",
            "    return SAMPLE_TEXTURE2D_LOD(_WeatherMakerCloudShadowTexture, sampler_WeatherMakerCloudShadowTexture, shadowUV, 0.0).r;",
            "}",
            "",
            "#endif",
            ""
        });

        static readonly string k_RuntimeScript = string.Join("\n", new[]
        {
            "using UnityEngine;",
            "",
            "namespace WaveHarmonic.Crest",
            "{",
            "    public static class WeatherMakerCloudShadow",
            "    {",
            "        static readonly int s_EnabledId = Shader.PropertyToID(\"_WeatherMakerCloudShadowEnabled\");",
            "",
            "        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]",
            "        static void Init()",
            "        {",
            "            Shader.SetGlobalFloat(s_EnabledId, 0f);",
            "        }",
            "",
            "        public static void SetEnabled(bool enabled)",
            "        {",
            "            Shader.SetGlobalFloat(s_EnabledId, enabled ? 1f : 0f);",
            "        }",
            "    }",
            "}",
            ""
        });

        // --- Helpers ---

        static string CrestPackagePath
        {
            get
            {
                string path = Path.GetFullPath(Path.Combine("Packages", k_CrestPackageId));
                return Directory.Exists(path) ? path : null;
            }
        }

        static bool IsCrestInstalled() => CrestPackagePath != null;

        static string FragmentPath      => Path.Combine(CrestPackagePath, "Runtime", "Shaders", "Surface", "Fragment.hlsl");
        static string CloudShadowHlslPath => Path.Combine(CrestPackagePath, "Runtime", "Shaders", "Surface", "CloudShadow.hlsl");
        static string RuntimeScriptPath => Path.Combine(CrestPackagePath, "Runtime", "Scripts", "WeatherMakerCloudShadow.cs");

        static bool IsPatched()
        {
            if (!IsCrestInstalled()) return false;
            string fp = FragmentPath;
            return File.Exists(fp) && File.ReadAllText(fp).Contains(k_PatchMarker);
        }

        // --- Menu items ---

        [MenuItem(k_EnableMenu, false, priority = 90)]
        static void Enable()
        {
            string fragmentPath = FragmentPath;
            if (!File.Exists(fragmentPath))
            {
                Debug.LogError($"WeatherMaker: Cannot find Fragment.hlsl at {fragmentPath}.");
                return;
            }

            File.WriteAllText(CloudShadowHlslPath, k_CloudShadowHlsl);
            File.WriteAllText(RuntimeScriptPath,   k_RuntimeScript);

            string fragment = File.ReadAllText(fragmentPath);
            fragment = fragment.Replace(k_IncludeAnchor, k_IncludeAnchor + "\n" + k_IncludeLine);
            fragment = fragment.Replace(k_ShadowAnchor,  k_ShadowAnchor  + k_ShadowPatch);
            File.WriteAllText(fragmentPath, fragment);

            AssetDatabase.Refresh();
            Debug.Log("WeatherMaker: Crest cloud shadows enabled.");
        }

        [MenuItem(k_EnableMenu, true)]
        static bool EnableValidate() => IsCrestInstalled() && !IsPatched();

        [MenuItem(k_DisableMenu, false, priority = 91)]
        static void Disable()
        {
            string fragmentPath = FragmentPath;
            string fragment = File.ReadAllText(fragmentPath);
            fragment = fragment.Replace(k_IncludeLine + "\n", "");
            fragment = fragment.Replace(k_IncludeLine, "");
            fragment = fragment.Replace(k_ShadowPatch, "");
            File.WriteAllText(fragmentPath, fragment);

            DeleteWithMeta(CloudShadowHlslPath);
            DeleteWithMeta(RuntimeScriptPath);

            AssetDatabase.Refresh();
            Debug.Log("WeatherMaker: Crest cloud shadows disabled. Crest restored to pristine state.");
        }

        [MenuItem(k_DisableMenu, true)]
        static bool DisableValidate() => IsCrestInstalled() && IsPatched();

        static void DeleteWithMeta(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            string meta = path + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }
    }
}
