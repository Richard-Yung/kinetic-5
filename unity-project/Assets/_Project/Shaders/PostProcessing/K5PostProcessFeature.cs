// ============================================================================
//  KINETICS 5 — Post-Processing Renderer Feature (URP)
//  Unity 6000.0 LTS — Task 2-f
// ----------------------------------------------------------------------------
//  ScriptableRendererFeature qui branche les passes K5PostProcess sur l'Universal
//  Renderer : pré-passe bloom (sélective sur pixels émissifs) puis composite
//  (bloom + chromatic aberration + vignette + film grain + LUT color grading).
//
//  Mode d'emploi :
//    1. Sélectionner l'Universal Renderer Data (menu > Assets > Create > URP).
//    2. Cliquer "Add Renderer Feature" → K5 Post Process Feature.
//    3. Glisser une LUT 32×32×32 (texture Filtering=Point, Compression=None).
//    4. Ajuster les curseurs ; le résultat est visible immédiatement en Scene.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KINETICS5.Shaders
{
    /// <summary>
    /// Feature URP branchant la chaîne de post-traitement KINETICS 5
    /// (bloom sélectif + chromatic aberration + vignette + film grain + LUT).
    /// </summary>
    [Serializable]
    public sealed class K5PostProcessSettings
    {
        [Header("Bloom sélectif")]
        [Tooltip("Active la passe de bloom (uniquement sur pixels émissifs).")]
        public bool BloomEnabled = true;
        [Range(0f, 4f)] public float BloomThreshold = 1.0f;
        [Range(0f, 1f)] public float BloomSoftKnee = 0.5f;
        [Range(0f, 4f)] public float BloomIntensity = 1.2f;

        [Header("Chromatic Aberration")]
        public bool ChromaticAberrationEnabled = true;
        [Range(0f, 0.01f)] public float ChromaticAberrationAmount = 0.0015f;

        [Header("Vignette")]
        public bool VignetteEnabled = true;
        public Color VignetteColor = new(0.02f, 0.024f, 0.05f, 1f);
        [Range(0.5f, 6f)] public float VignettePower = 2.2f;
        [Range(0f, 1f)] public float VignetteIntensity = 0.55f;

        [Header("Film Grain")]
        public bool GrainEnabled = true;
        [Range(0f, 0.25f)] public float GrainIntensity = 0.06f;
        [Range(0.5f, 30f)] public float GrainSpeed = 8f;

        [Header("LUT Color Grading")]
        public bool LutEnabled = true;
        [Range(0f, 1f)] public float LutContribution = 0.85f;
        [Tooltip("LUT 32×32×32 (Texture2D, Filtering=Point, Compression=None).")]
        public Texture2D LutTexture;
        public const float LutSize = 32f;

        [Header("Injection")]
        [Tooltip("Moment d'injection dans la frame URP.")]
        public RenderPassEvent InjectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    /// <summary>
    /// RendererFeature URP qui enregistre <see cref="K5PostProcessPass"/>.
    /// Ajoutable sur n'importe quel Universal Renderer Data.
    /// </summary>
    [DisallowMultipleRendererFeature("K5 Post Process")]
    public sealed class K5PostProcessFeature : ScriptableRendererFeature
    {
        [SerializeField] private K5PostProcessSettings _settings = new();
        public K5PostProcessSettings Settings => _settings;

        private K5PostProcessPass _pass;
        private Material _compositeMaterial;
        private Material _bloomMaterial;
        private RTHandle _bloomTex; // texture intermédiaire pour bloom (downsample ×2)

        /// <inheritdoc />
        public override void Create()
        {
            _pass = new K5PostProcessPass(_settings)
            {
                renderPassEvent = _settings.InjectionPoint
            };
            LoadMaterials();
        }

        private void LoadMaterials()
        {
            var compositeShader = Shader.Find("Hidden/KINETICS5/PostProcess");
            var bloomShader = Shader.Find("Hidden/KINETICS5/BloomPrePass");
            if (compositeShader != null)
                _compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            if (bloomShader != null)
                _bloomMaterial = CoreUtils.CreateEngineMaterial(bloomShader);
            _pass.SetMaterials(_compositeMaterial, _bloomMaterial);
        }

        /// <inheritdoc />
        public override void SetupRenderPasses(in ScriptableRenderContext ctx, in RenderingData renderingData)
        {
            // Alloue le RTHandle pour le bloom si nécessaire (une fois, taille = screen/2).
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.width  = Mathf.Max(1, desc.width  / 2);
            desc.height = Mathf.Max(1, desc.height / 2);
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref _bloomTex, desc, name: "_K5BloomTex");
            _pass.SetBloomTarget(_bloomTex);
        }

        /// <inheritdoc />
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_compositeMaterial == null || _bloomMaterial == null)
            {
                LoadMaterials();
                if (_compositeMaterial == null || _bloomMaterial == null) return;
            }
            // Active les mots-clés du shader composite selon les settings.
            SetKeyword(_compositeMaterial, "_K5_BLOOM",      _settings.BloomEnabled);
            SetKeyword(_compositeMaterial, "_K5_CA",         _settings.ChromaticAberrationEnabled);
            SetKeyword(_compositeMaterial, "_K5_VIGNETTE",   _settings.VignetteEnabled);
            SetKeyword(_compositeMaterial, "_K5_GRAIN",      _settings.GrainEnabled);
            SetKeyword(_compositeMaterial, "_K5_LUT",        _settings.LutEnabled && _settings.LutTexture != null);

            _compositeMaterial.SetFloat("_BloomThreshold",   _settings.BloomThreshold);
            _compositeMaterial.SetFloat("_BloomSoftKnee",    _settings.BloomSoftKnee);
            _compositeMaterial.SetFloat("_BloomIntensity",   _settings.BloomIntensity);
            _compositeMaterial.SetFloat("_CAAmount",         _settings.ChromaticAberrationAmount);
            _compositeMaterial.SetColor("_VignetteColor",    _settings.VignetteColor);
            _compositeMaterial.SetFloat("_VignettePower",    _settings.VignettePower);
            _compositeMaterial.SetFloat("_VignetteIntensity",_settings.VignetteIntensity);
            _compositeMaterial.SetFloat("_GrainIntensity",   _settings.GrainIntensity);
            _compositeMaterial.SetFloat("_GrainSpeed",       _settings.GrainSpeed);
            _compositeMaterial.SetFloat("_LutContribution",  _settings.LutContribution);
            _compositeMaterial.SetFloat("_LutSize",          K5PostProcessSettings.LutSize);
            if (_settings.LutTexture != null)
                _compositeMaterial.SetTexture(_LutTexID, _settings.LutTexture);

            _bloomMaterial.SetFloat("_BloomThreshold", _settings.BloomThreshold);
            _bloomMaterial.SetFloat("_BloomSoftKnee",  _settings.BloomSoftKnee);
            _bloomMaterial.SetFloat("_BloomIntensity", _settings.BloomIntensity);

            renderer.EnqueuePass(_pass);
        }

        private static readonly int _LutTexID = Shader.PropertyToID("_LutTex");

        private static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (mat == null) return;
            if (enabled) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (_bloomTex != null) { _bloomTex.Release(); _bloomTex = null; }
            if (_compositeMaterial != null) CoreUtils.Destroy(_compositeMaterial);
            if (_bloomMaterial != null) CoreUtils.Destroy(_bloomMaterial);
        }
    }

    /// <summary>
    /// Passe de rendu concrète : pré-passe bloom (downsample) + composite final.
    /// </summary>
    public sealed class K5PostProcessPass : ScriptableRenderPass
    {
        private readonly K5PostProcessSettings _settings;
        private Material _compositeMaterial;
        private Material _bloomMaterial;
        private RTHandle _bloomTex;
        private RTHandle _tempColor;

        private static readonly int _BloomTexID = Shader.PropertyToID("_BloomTex");

        public K5PostProcessPass(K5PostProcessSettings settings) { _settings = settings; }

        public void SetMaterials(Material composite, Material bloom)
        {
            _compositeMaterial = composite;
            _bloomMaterial = bloom;
        }

        public void SetBloomTarget(RTHandle bloomTex) { _bloomTex = bloomTex; }

        /// <inheritdoc />
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempColor, desc, name: "_K5TempColor");
        }

        /// <inheritdoc />
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_compositeMaterial == null) return;
            var cmd = CommandBufferPool.Get("K5 Post Process");

            var colorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // 1. Pré-passe bloom → _bloomTex.
            if (_settings.BloomEnabled && _bloomMaterial != null && _bloomTex != null)
            {
                Blitter.BlitCameraTexture(cmd, colorTarget, _bloomTex, _bloomMaterial, 0);
                _compositeMaterial.SetTexture(_BloomTexID, _bloomTex);
            }

            // 2. Composite → _tempColor.
            Blitter.BlitCameraTexture(cmd, colorTarget, _tempColor, _compositeMaterial, 0);
            // 3. Recopie vers la cible finale (pas de ping-pong supplémentaire en URP 17).
            Blitter.BlitCameraTexture(cmd, _tempColor, colorTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc />
        public override void OnCameraCleanup(CommandBuffer cmd) { /* _tempColor est réutilisé */ }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_tempColor != null) { _tempColor.Release(); _tempColor = null; }
        }
    }
}
