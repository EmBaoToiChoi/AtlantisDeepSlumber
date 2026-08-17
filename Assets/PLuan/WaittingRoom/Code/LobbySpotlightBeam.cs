using System.Collections;
using UnityEngine;

/// <summary>
/// Mr. Bean Style Spotlight Beam for Character Selection in Lobby / Waiting Room.
/// Creates a clean overhead heavenly spotlight that illuminates the character,
/// with a subtle, non-intrusive background halo, soft floor glow, and shimmering dust motes.
/// </summary>
public class LobbySpotlightBeam : MonoBehaviour
{
    [Header("Target & Positioning")]
    [Tooltip("Target slot or character position")]
    public Transform slotTransform;
    [Tooltip("Height of the spotlight source above the ground")]
    public float beamHeight = 8.5f;
    [Tooltip("Radius at the top of the beam")]
    public float topRadius = 0.35f;
    [Tooltip("Radius at the base of the beam where it hits the ground")]
    public float bottomRadius = 1.6f;

    [Header("Lighting Settings")]
    [Tooltip("Base intensity for the real-time Unity Spotlight that illuminates the character")]
    public float lightIntensity = 1.8f;
    [Tooltip("Spot angle of the real-time light")]
    public float spotAngle = 38f;
    [Tooltip("Color tint of the light beam (Slot 4 Arthur Warm Gold)")]
    public Color beamColor = new Color(1.0f, 0.88f, 0.42f, 0.4f); // Vàng kim Slot 4
    [Tooltip("Use specific color themes for each character")]
    public bool useCharacterTheming = false;

    [Header("Visual Components")]
    private Light _spotLight;
    private GameObject _volumetricConeObj;
    private MeshRenderer _coneRenderer;
    private Material _coneMaterial;
    private GameObject _groundDiscObj;
    private MeshRenderer _groundRenderer;
    private Material _groundMaterial;
    private ParticleSystem _dustParticles;

    // Animation & State
    private Coroutine _animCoroutine;
    private bool _isActive = false;
    private float _currentAlpha = 0f;
    private int _currentCharacterId = -1;
    private float _pulseTimer = 0f;

    // Tông màu vàng kim hoàng gia Slot 4 (Arthur) - duy nhất 1 màu cho mọi tướng
    public static readonly Color SingleGoldColor = new Color(1.0f, 0.88f, 0.42f, 0.4f);

    private void Awake()
    {
        BuildComponentsIfNeeded();
    }

    private void Start()
    {
        BuildComponentsIfNeeded();
        // Hide by default until activated
        ApplyVisualState(0f, 0f, 0f);
    }

    private void Update()
    {
        if (_isActive && _currentAlpha > 0.01f)
        {
            // Gentle breathing pulse for a living cinematic light beam
            _pulseTimer += Time.deltaTime * 1.8f;
            float breathe = 1.0f + Mathf.Sin(_pulseTimer) * 0.04f;

            if (_spotLight != null)
            {
                _spotLight.intensity = lightIntensity * _currentAlpha * breathe;
            }

            if (_coneMaterial != null)
            {
                _coneMaterial.SetFloat("_Intensity", 0.32f * _currentAlpha * breathe);
            }

            if (_groundMaterial != null)
            {
                _groundMaterial.SetFloat("_Intensity", 0.35f * _currentAlpha * breathe);
            }
        }
    }

    /// <summary>
    /// Builds all 3D meshes, light sources, materials, and particle systems procedurally.
    /// </summary>
    public void BuildComponentsIfNeeded()
    {
        if (_spotLight != null && _volumetricConeObj != null) return;

        Vector3 rootPos = slotTransform != null ? slotTransform.position : transform.position;

        // 1. CREATE REALTIME SPOTLIGHT (Illuminates character clearly from above)
        GameObject lightObj = new GameObject("MrBean_SpotlightSource");
        lightObj.transform.SetParent(transform);
        lightObj.transform.position = rootPos + Vector3.up * beamHeight;
        lightObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Point straight down

        _spotLight = lightObj.AddComponent<Light>();
        _spotLight.type = LightType.Spot;
        _spotLight.spotAngle = spotAngle;
        _spotLight.innerSpotAngle = spotAngle * 0.65f;
        _spotLight.range = beamHeight + 3.5f;
        _spotLight.color = new Color(beamColor.r, beamColor.g, beamColor.b, 1f);
        _spotLight.intensity = 0f;
        _spotLight.shadows = LightShadows.Soft;
        _spotLight.shadowStrength = 0.5f;

        // 2. CREATE VOLUMETRIC CONE MESH (Cull Front: acts as a background halo, never obscures front of character)
        _volumetricConeObj = new GameObject("MrBean_VolumetricCone");
        _volumetricConeObj.transform.SetParent(transform);
        _volumetricConeObj.transform.position = rootPos + Vector3.up * beamHeight;
        _volumetricConeObj.transform.rotation = Quaternion.identity;

        MeshFilter coneFilter = _volumetricConeObj.AddComponent<MeshFilter>();
        coneFilter.sharedMesh = GenerateConeMesh(topRadius, bottomRadius, beamHeight, 36);

        _coneRenderer = _volumetricConeObj.AddComponent<MeshRenderer>();
        
        Material loadedBeamMat = Resources.Load<Material>("M_LobbySpotlightBeam");
        if (loadedBeamMat != null)
        {
            _coneMaterial = new Material(loadedBeamMat);
        }
        else
        {
            Shader beamShader = Shader.Find("Custom/VolumetricSpotlightBeam");
            if (beamShader == null) beamShader = Shader.Find("Mobile/Particles/Additive");
            if (beamShader == null) beamShader = Shader.Find("Particles/Standard Unlit");
            if (beamShader == null) beamShader = Shader.Find("Sprites/Default");
            _coneMaterial = new Material(beamShader);
        }

        _coneMaterial.SetColor("_Color", SingleGoldColor);
        _coneMaterial.SetFloat("_Intensity", 0.32f);
        _coneMaterial.SetFloat("_TopFade", 0.15f);
        _coneMaterial.SetFloat("_BottomFade", 0.35f);
        _coneMaterial.SetFloat("_RimPower", 1.5f);
        _coneMaterial.SetFloat("_CoreGlow", 0.08f);
        _coneMaterial.SetFloat("_RimWeight", 0.45f);
        _coneRenderer.material = _coneMaterial;
        _coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _coneRenderer.receiveShadows = false;

        // 3. CREATE GROUND LIGHT DISC (Soft subtle pool of light on the floor)
        _groundDiscObj = new GameObject("MrBean_GroundDisc");
        _groundDiscObj.transform.SetParent(transform);
        _groundDiscObj.transform.position = rootPos + Vector3.up * 0.02f; // Just above ground
        _groundDiscObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        MeshFilter discFilter = _groundDiscObj.AddComponent<MeshFilter>();
        discFilter.sharedMesh = GenerateDiscMesh(bottomRadius * 1.05f, 36);

        _groundRenderer = _groundDiscObj.AddComponent<MeshRenderer>();
        
        Material loadedDiscMat = Resources.Load<Material>("M_LobbyGroundDisc");
        if (loadedDiscMat != null)
        {
            _groundMaterial = new Material(loadedDiscMat);
        }
        else
        {
            Shader discShader = Shader.Find("Custom/LobbyGroundLightDisc");
            if (discShader == null) discShader = _coneMaterial != null ? _coneMaterial.shader : Shader.Find("Mobile/Particles/Additive");
            _groundMaterial = new Material(discShader);
        }

        _groundMaterial.SetColor("_Color", SingleGoldColor);
        _groundMaterial.SetFloat("_Intensity", 0.35f);
        _groundRenderer.material = _groundMaterial;
        _groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _groundRenderer.receiveShadows = false;

        // 4. CREATE DUST PARTICLES (Floating subtle sparkles)
        CreateDustParticles(rootPos);
    }

    private void CreateDustParticles(Vector3 rootPos)
    {
        GameObject dustObj = new GameObject("MrBean_DustSparkles");
        dustObj.transform.SetParent(transform);
        dustObj.transform.position = rootPos + Vector3.up * (beamHeight * 0.5f);
        dustObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        _dustParticles = dustObj.AddComponent<ParticleSystem>();
        var main = _dustParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 3.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new Color(1f, 0.98f, 0.85f, 0.45f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = _dustParticles.emission;
        emission.rateOverTime = 10f;

        var shape = _dustParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = topRadius * 1.1f;
        shape.length = beamHeight * 0.85f;

        var colorOverLifetime = _dustParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.25f), new GradientAlphaKey(0.5f, 0.75f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var noise = _dustParticles.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.2f;

        var pRenderer = dustObj.GetComponent<ParticleSystemRenderer>();
        if (_coneMaterial != null)
        {
            pRenderer.material = _coneMaterial;
        }
    }

    /// <summary>
    /// Triggers the dramatic "Mr. Bean Spotlight Drop" animation from above.
    /// </summary>
    public void PlayBeamDrop(int characterId = -1)
    {
        BuildComponentsIfNeeded();
        _isActive = true;
        _currentCharacterId = characterId;

        Color targetColor = GetThemeColor(characterId);
        SetColor(targetColor);

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(BeamDropAnimationCoroutine());
    }

    /// <summary>
    /// Sets the beam active or inactive with a smooth fade.
    /// </summary>
    public void SetBeamActive(bool active, int characterId = -1)
    {
        BuildComponentsIfNeeded();
        _isActive = active;
        _currentCharacterId = characterId;

        if (active)
        {
            Color targetColor = GetThemeColor(characterId);
            SetColor(targetColor);
        }

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(FadeBeamCoroutine(active ? 1.0f : 0.0f));
    }

    private Color GetThemeColor(int characterId)
    {
        // Luôn trả về 1 màu vàng kim hoàng gia của Slot 4 (Arthur) cho tất cả tướng
        return SingleGoldColor;
    }

    public void SetColor(Color col)
    {
        beamColor = SingleGoldColor;
        if (_spotLight != null) _spotLight.color = new Color(SingleGoldColor.r, SingleGoldColor.g, SingleGoldColor.b, 1f);
        if (_coneMaterial != null) _coneMaterial.SetColor("_Color", SingleGoldColor);
        if (_groundMaterial != null) _groundMaterial.SetColor("_Color", SingleGoldColor);
    }

    private IEnumerator BeamDropAnimationCoroutine()
    {
        if (_dustParticles != null && !_dustParticles.isPlaying)
        {
            _dustParticles.Play();
        }

        float duration = 0.32f;
        float elapsed = 0f;

        ApplyVisualState(0.05f, 0.05f, 0.05f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float scaleY = Mathf.Sin(t * Mathf.PI * 0.5f);
            float flareIntensity = Mathf.Lerp(1.3f, 1.0f, t);
            float discScale = Mathf.SmoothStep(0.2f, 1.0f, t);

            ApplyVisualState(scaleY, discScale, flareIntensity);
            yield return null;
        }

        _currentAlpha = 1.0f;
        ApplyVisualState(1.0f, 1.0f, 1.0f);
        _animCoroutine = null;
    }

    private IEnumerator FadeBeamCoroutine(float targetAlpha)
    {
        float startAlpha = _currentAlpha;
        float elapsed = 0f;
        float duration = targetAlpha > startAlpha ? 0.28f : 0.35f;

        if (targetAlpha > 0.01f && _dustParticles != null && !_dustParticles.isPlaying)
        {
            _dustParticles.Play();
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            ApplyVisualState(_currentAlpha, _currentAlpha, _currentAlpha);
            yield return null;
        }

        _currentAlpha = targetAlpha;
        ApplyVisualState(_currentAlpha, _currentAlpha, _currentAlpha);

        if (_currentAlpha <= 0.01f && _dustParticles != null && _dustParticles.isPlaying)
        {
            _dustParticles.Stop();
        }

        _animCoroutine = null;
    }

    private void ApplyVisualState(float beamProgress, float discProgress, float intensityMultiplier)
    {
        _currentAlpha = Mathf.Clamp01(beamProgress);

        if (_volumetricConeObj != null)
        {
            _volumetricConeObj.transform.localScale = new Vector3(1f, Mathf.Max(0.001f, beamProgress), 1f);
            if (_coneRenderer != null) _coneRenderer.enabled = beamProgress > 0.01f;
        }

        if (_groundDiscObj != null)
        {
            _groundDiscObj.transform.localScale = new Vector3(discProgress, discProgress, 1f);
            if (_groundRenderer != null) _groundRenderer.enabled = discProgress > 0.01f;
        }

        if (_spotLight != null)
        {
            _spotLight.intensity = lightIntensity * _currentAlpha * intensityMultiplier;
            _spotLight.enabled = _spotLight.intensity > 0.01f;
        }
    }

    // ==========================================
    // PROCEDURAL MESH GENERATION HELPERS
    // ==========================================

    private Mesh GenerateConeMesh(float rTop, float rBottom, float height, int segments)
    {
        Mesh mesh = new Mesh { name = "Procedural_Volumetric_Cone" };

        int vertCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];
        int[] triangles = new int[segments * 6];

        float angleStep = (Mathf.PI * 2f) / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Top vertex (Apex ring)
            int topIdx = i * 2;
            vertices[topIdx] = new Vector3(cos * rTop, 0f, sin * rTop);
            normals[topIdx] = new Vector3(cos, 0.2f, sin).normalized;
            uvs[topIdx] = new Vector2((float)i / segments, 0.0f);
            colors[topIdx] = new Color(1f, 1f, 1f, 0.25f);

            // Bottom vertex (Base ring)
            int botIdx = i * 2 + 1;
            vertices[botIdx] = new Vector3(cos * rBottom, -height, sin * rBottom);
            normals[botIdx] = new Vector3(cos, 0.2f, sin).normalized;
            uvs[botIdx] = new Vector2((float)i / segments, 1.0f);
            colors[botIdx] = new Color(1f, 1f, 1f, 0.7f);
        }

        int triIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            int top1 = i * 2;
            int bot1 = i * 2 + 1;
            int top2 = (i + 1) * 2;
            int bot2 = (i + 1) * 2 + 1;

            triangles[triIdx++] = top1;
            triangles[triIdx++] = bot1;
            triangles[triIdx++] = top2;

            triangles[triIdx++] = top2;
            triangles[triIdx++] = bot1;
            triangles[triIdx++] = bot2;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private Mesh GenerateDiscMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh { name = "Procedural_Ground_Disc" };

        int vertCount = segments + 2;
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.forward;
        uvs[0] = new Vector2(0.5f, 0.5f);
        colors[0] = Color.white;

        float angleStep = (Mathf.PI * 2f) / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            int idx = i + 1;
            vertices[idx] = new Vector3(cos * radius, sin * radius, 0f);
            normals[idx] = Vector3.forward;
            uvs[idx] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
            colors[idx] = Color.white;
        }

        int triIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            triangles[triIdx++] = 0;
            triangles[triIdx++] = i + 1;
            triangles[triIdx++] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }
}
