using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
// Commented out since project uses Built-in Render Pipeline (BiRP) instead of URP
// using UnityEngine.Rendering.Universal;

namespace PlayDecalVFX
{
[ExecuteAlways]
public class PlayVFX : MonoBehaviour
{
    // Commented out DecalProjector since it is URP-specific
    // private DecalProjector decal;
    private Material mat;
    private VisualEffect effect;

    private float anticipation;
    private float dissapation;
    private AnimationCurve showIndicatorCurve;
    private AnimationCurve dissolveCurve;
    private float startTime;
    private float lifetime;

    private bool effectPlayed = false;
    // Start is called before the first frame update
    void Start()
    {
        //Assign variables
        effect = gameObject.GetComponent<VisualEffect>();
        effect.Play();
        
        // Commented out DecalProjector URP references
        // decal = gameObject.GetComponentInChildren<DecalProjector>();
        // if (decal != null) mat = decal.material;
        
        // Safely fetch float properties from VFX Graph with default fallbacks
        anticipation = effect.HasFloat("Anticipation") ? effect.GetFloat("Anticipation") : 1.0f;
        dissapation = effect.HasFloat("Dissapation") ? effect.GetFloat("Dissapation") : 0.0f;
        
        // Prevent division by zero
        if (anticipation <= 0f) anticipation = 1.0f;

        startTime = Time.time;
        
        showIndicatorCurve = effect.HasAnimationCurve("IndicatorCurve") ? effect.GetAnimationCurve("IndicatorCurve") : null;
        dissolveCurve = effect.HasAnimationCurve("DissolveCurve") ? effect.GetAnimationCurve("DissolveCurve") : null;

        //Apply properties to decal shader
        if (mat != null)
        {
            if (effect.HasTexture("IndicatorTexture")) mat.SetTexture("_Texture2D", effect.GetTexture("IndicatorTexture"));
            if (effect.HasVector4("BrightColor")) mat.SetColor("_BrightColor", effect.GetVector4("BrightColor"));
            if (effect.HasVector4("DarkColor")) mat.SetColor("_DarkColor", effect.GetVector4("DarkColor"));
            if (effect.HasBool("UseLUT")) mat.SetInt("_UseLUT", effect.GetBool("UseLUT") ? 1 : 0);
            if (effect.HasTexture("LUT")) mat.SetTexture("_LUT", effect.GetTexture("LUT"));
        }

        //Apply diameter to decal
        // if (decal != null) decal.size = new Vector3(effect.GetFloat("Diameter") * 1.15f, effect.GetFloat("Diameter") * 1.15f, effect.GetFloat("Diameter") * 1.15f);

    }

    // Update is called once per frame
    void Update()
    {
        //calc lifetime depending on effect
        float denom = (effect.name == "PlantHealDecalPrefab(Clone)") ? (anticipation + dissapation) : anticipation;
        if (denom <= 0f) denom = 1.0f;
        
        lifetime = (Time.time - startTime) * (1f / denom);
        
        //Animate Properties
        if (mat != null)
        {
            if (showIndicatorCurve != null) mat.SetFloat("_SHowIndicator", showIndicatorCurve.Evaluate(lifetime));
            if (dissolveCurve != null) mat.SetFloat("_Dissolve", dissolveCurve.Evaluate(lifetime));
        }


        // Register PlayStart
        if (effect.aliveParticleCount > 0 && !effectPlayed)
        {
            effectPlayed = true;
        }
        //Delete Game Object after Playing
        if (effect.aliveParticleCount == 0 && effectPlayed && lifetime > anticipation + dissapation)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }
        
        
    }
}
}
