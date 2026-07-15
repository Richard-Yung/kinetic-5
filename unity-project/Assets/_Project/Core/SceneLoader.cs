using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
#if KINETICS_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace KINETICS5.Core
{
    /// <summary>
    /// Chargeur de scènes additif basé sur Addressables.
    /// - Charge les missions additivement par-dessus une scène de base persistante.
    /// - Décharge la mission précédente APRÈS le chargement de la nouvelle (sans frame blanche).
    /// - Transition en fondu via DOTween (CanvasGroup plein écran).
    /// - Callback de progression pour la barre de chargement du Mission Loading Screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        private static SceneLoader _instance;
        /// <summary>Instance globale.</summary>
        public static SceneLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SceneLoader]");
                    _instance = go.AddComponent<SceneLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Référence scène de base")]
        [Tooltip("Nom de la scène de base persistante (chargée au boot, jamais déchargée).")]
        [SerializeField] private string _baseSceneName = "Base";

        [Header("Transition")]
        [Tooltip("CanvasGroup plein écran (image noire) pour le fondu. Si null, créé automatiquement.")]
        [SerializeField] private CanvasGroup _fadeOverlay;
        [Tooltip("Durée du fondu entrant/sortant en secondes.")]
        [Min(0.05f)][SerializeField] private float _fadeDuration = 0.4f;

        [Header("Addressables")]
        [Tooltip("Préfixe des clés Addressables pour les scènes de mission.")]
        [SerializeField] private string _missionKeyPrefix = "Mission_";

        /// <summary>Progression 0..1 du chargement en cours.</summary>
        public float CurrentProgress { get; private set; }
        /// <summary>Indique si un chargement est en cours.</summary>
        public bool IsLoading { get; private set; }

        private string _loadedMissionId;
#if KINETICS_ADDRESSABLES
        private SceneInstance _loadedMissionScene;
#endif
        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (_fadeOverlay == null) CreateDefaultFadeOverlay();
            _fadeOverlay.alpha = 0f;
            _fadeOverlay.blocksRaycasts = false;
            _fadeOverlay.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (_instance == this) _instance = null;
        }

        /// <summary>Callback de progression : <paramref name="progress"/> est 0..1.</summary>
        public event Action<float> ProgressChanged;

        /// <summary>
        /// Charge une mission de façon additive. Gère le fondu et décharge la mission précédente.
        /// </summary>
        public async UniTask LoadMissionAsync(string missionId, IProgress<float> progress = null)
        {
            if (IsLoading) { Debug.LogWarning("[SceneLoader] Chargement déjà en cours."); return; }
            IsLoading = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            try
            {
                CurrentProgress = 0f;
                ProgressChanged?.Invoke(0f);
                progress?.Report(0f);

                await FadeOutAsync(token); // fondu vers noir
                Report(0.1f, progress);

                // Décharge l'ancienne mission si présente.
                if (!string.IsNullOrEmpty(_loadedMissionId))
                {
                    await UnloadMissionSceneAsync();
                    Report(0.3f, progress);
                }

                // Charge la nouvelle mission additive.
                await LoadMissionSceneAsync(missionId, progress, token);
                _loadedMissionId = missionId;
                Report(1f, progress);

                await FadeInAsync(token); // fondu vers transparent
                GameManager.Instance?.OnMissionLoaded();
            }
            catch (OperationCanceledException) { /* OK */ }
            catch (Exception ex) { Debug.LogError($"[SceneLoader] Échec chargement {missionId}: {ex}"); }
            finally
            {
                IsLoading = false;
                _cts?.Dispose(); _cts = null;
            }
        }

        /// <summary>Décharge la mission courante (si présente).</summary>
        public async UniTask UnloadMissionAsync()
        {
            if (string.IsNullOrEmpty(_loadedMissionId)) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            try
            {
                await FadeOutAsync(token);
                await UnloadMissionSceneAsync();
                _loadedMissionId = null;
                await FadeInAsync(token);
            }
            catch (OperationCanceledException) { /* OK */ }
            catch (Exception ex) { Debug.LogError($"[SceneLoader] Échec déchargement: {ex}"); }
            finally { _cts?.Dispose(); _cts = null; }
        }

        // --- Implémentation interne ---

        private async UniTask LoadMissionSceneAsync(string missionId, IProgress<float> progress, CancellationToken token)
        {
#if KINETICS_ADDRESSABLES
            var key = $"{_missionKeyPrefix}{missionId}";
            var handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: true);
            while (!handle.IsDone)
            {
                if (token.IsCancellationRequested) { Addressables.Release(handle); throw new OperationCanceledException(token); }
                Report(0.3f + handle.PercentComplete * 0.65f, progress);
                await UniTask.Yield(token);
            }
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _loadedMissionScene = handle.Result;
                Report(0.95f, progress);
                await _loadedMissionScene.ActivateAsync().ToUniTask(cancellationToken: token);
            }
            else
            {
                throw new Exception($"Addressables échec chargement {key}: {handle.OperationException?.Message}");
            }
#else
            // Fallback SceneManager (si Addressables désactivé): build settings scene.
            Report(0.5f, progress);
            var op = SceneManager.LoadSceneAsync(missionId, LoadSceneMode.Additive);
            while (!op.isDone) { Report(0.3f + op.progress * 0.65f, progress); await UniTask.Yield(token); }
            Report(0.95f, progress);
#endif
        }

        private async UniTask UnloadMissionSceneAsync()
        {
#if KINETICS_ADDRESSABLES
            if (_loadedMissionScene.Scene.IsValid())
            {
                var handle = Addressables.UnloadSceneAsync(_loadedMissionScene, autoReleaseHandle: true);
                while (!handle.IsDone) await UniTask.Yield();
            }
#else
            if (!string.IsNullOrEmpty(_loadedMissionId))
            {
                var op = SceneManager.UnloadSceneAsync(_loadedMissionId);
                while (!op.isDone) await UniTask.Yield();
            }
#endif
        }

        private async UniTask FadeOutAsync(CancellationToken token)
        {
            _fadeOverlay.gameObject.SetActive(true);
            _fadeOverlay.blocksRaycasts = true;
            await _fadeOverlay.DOFade(1f, _fadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
        }

        private async UniTask FadeInAsync(CancellationToken token)
        {
            await _fadeOverlay.DOFade(0f, _fadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
            _fadeOverlay.blocksRaycasts = false;
            _fadeOverlay.gameObject.SetActive(false);
        }

        private void Report(float value, IProgress<float> external)
        {
            CurrentProgress = Mathf.Clamp01(value);
            ProgressChanged?.Invoke(CurrentProgress);
            external?.Report(CurrentProgress);
        }

        private void CreateDefaultFadeOverlay()
        {
            var go = new GameObject("[FadeOverlay]");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            var image = new GameObject("Black").AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.02f, 0.02f, 0.04f, 0f);
            image.rectTransform.SetParent(go.transform, false);
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            _fadeOverlay = go.AddComponent<CanvasGroup>();
        }
    }
}
