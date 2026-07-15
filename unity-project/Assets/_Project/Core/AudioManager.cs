using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
#if KINETICS_FMOD
using FMODUnity;
using FMOD;
#endif

namespace KINETICS5.Core
{
    /// <summary>
    /// Catégories de volume (bus de mixage).
    /// </summary>
    public enum AudioBus { Master, Music, Sfx, Voice }

    /// <summary>
    /// Gestionnaire audio de KINETICS 5.
    /// - Wrapper FMOD (compilation conditionnelle #if KINETICS_FMOD) avec fallback AudioSource.
    /// - Crossfade BGM entre thèmes (menu, mission, victoire).
    /// - SFX one-shot avec variation de pitch, voices poolées (max 32 pour mobile).
    /// - Audio 3D spatial pour les explosions / impacts.
    /// - Catégories de volume persistées dans SaveSystem (Master/Music/SFX/Voice).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        /// <summary>Instance globale.</summary>
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AudioManager]");
                    _instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Configuration mobile")]
        [Tooltip("Nombre maximal de voices SFX simultanées (mobile low-end).")]
        [Range(8, 64)][SerializeField] private int _maxConcurrentSfx = 32;
        [Tooltip("Variation de pitch aléatoire pour les one-shots (centièmes).")]
        [Range(0f, 0.2f)][SerializeField] private float _pitchJitter = 0.05f;

        [Header("Sources fallback (sans FMOD)")]
        [Tooltip("Source audio dédiée BGM (boucle).")]
        [SerializeField] private AudioSource _musicSourceA;
        [SerializeField] private AudioSource _musicSourceB;
        [Tooltip("Source audio dédiée voix.")]
        [SerializeField] private AudioSource _voiceSource;
        [Tooltip("Prefab AudioSource pour SFX poolées.")]
        [SerializeField] private AudioSource _sfxPrefab;

        [Header("Volumes initiaux")]
        [Range(0f, 1f)][SerializeField] private float _master = 1f;
        [Range(0f, 1f)][SerializeField] private float _music = 0.8f;
        [Range(0f, 1f)][SerializeField] private float _sfx = 1f;
        [Range(0f, 1f)][SerializeField] private float _voice = 1f;

        // Pool de sources SFX (Stack<AudioSource>) : pas de GC après warmup.
        private readonly Stack<AudioSource> _sfxPool = new(32);
        private readonly List<AudioSource> _activeSfx = new(32);
        private AudioListener _listener;
        private AudioSource _currentMusicSource;
        private AudioSource _nextMusicSource;

        // Volumes effectifs appliqués (bus).
        private float _busMaster, _busMusic, _busSfx, _busVoice;

#if KINETICS_FMOD
        private FMOD.Studio.Bus _fmodMaster, _fmodMusic, _fmodSfx, _fmodVoice;
#endif

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureListener();
            EnsureSources();
            _busMaster = _master; _busMusic = _music; _busSfx = _sfx; _busVoice = _voice;

#if KINETICS_FMOD
            try
            {
                RuntimeManager.LoadBank("Master");
                _fmodMaster = RuntimeManager.GetBus("bus:/Master");
                _fmodMusic = RuntimeManager.GetBus("bus:/Music");
                _fmodSfx = RuntimeManager.GetBus("bus:/SFX");
                _fmodVoice = RuntimeManager.GetBus("bus:/Voice");
            }
            catch (Exception ex) { Debug.LogWarning($"[AudioManager] FMOD init partielle: {ex}"); }
#endif
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            for (int i = 0; i < _activeSfx.Count; i++)
            {
                if (_activeSfx[i] != null) Destroy(_activeSfx[i].gameObject);
            }
            _activeSfx.Clear(); _sfxPool.Clear();
            _instance = null;
        }

        private void Update()
        {
            // Recyclage des sources SFX terminées (zéro allocation: for-loop + swap-remove).
            for (int i = _activeSfx.Count - 1; i >= 0; i--)
            {
                var src = _activeSfx[i];
                if (src == null) { _activeSfx.RemoveAt(i); continue; }
                if (!src.isPlaying)
                {
                    src.clip = null;
                    _activeSfx.RemoveAt(i);
                    _sfxPool.Push(src);
                }
            }
        }

        // --- Volumes & bus ---

        /// <summary>Définit le volume d'une catégorie (0..1).</summary>
        public void SetBusVolume(AudioBus bus, float volume)
        {
            volume = Mathf.Clamp01(volume);
            switch (bus)
            {
                case AudioBus.Master: _busMaster = volume; break;
                case AudioBus.Music: _busMusic = volume; break;
                case AudioBus.Sfx: _busSfx = volume; break;
                case AudioBus.Voice: _busVoice = volume; break;
            }
            ApplyVolumes();
        }

        public float GetBusVolume(AudioBus bus) => bus switch
        {
            AudioBus.Master => _busMaster,
            AudioBus.Music => _busMusic,
            AudioBus.Sfx => _busSfx,
            AudioBus.Voice => _busVoice,
            _ => 0f
        };

        private void ApplyVolumes()
        {
            AudioListener.volume = _busMaster;
            if (_musicSourceA) _musicSourceA.volume = _busMusic;
            if (_musicSourceB) _musicSourceB.volume = _busMusic;
            if (_voiceSource) _voiceSource.volume = _busVoice;
#if KINETICS_FMOD
            _fmodMaster?.setVolume(_busMaster);
            _fmodMusic?.setVolume(_busMusic);
            _fmodSfx?.setVolume(_busSfx);
            _fmodVoice?.setVolume(_busVoice);
#endif
        }

        // --- BGM avec crossfade ---

        /// <summary>Joue un BGM avec crossfade. clip null = arrêt.</summary>
        public async UniTask PlayMusicAsync(AudioClip clip, float crossfade = 1.5f)
        {
            if (clip == null) { await StopMusic(crossfade); return; }
            if (_currentMusicSource != null && _currentMusicSource.clip == clip) return;
            var next = _nextMusicSource ?? (_currentMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA);
            next.clip = clip;
            next.volume = 0f;
            next.loop = true;
            next.Play();
            var t = 0f;
            while (t < crossfade)
            {
                t += Time.unscaledDeltaTime;
                float k = t / crossfade;
                if (_currentMusicSource != null) _currentMusicSource.volume = _busMusic * (1f - k);
                next.volume = _busMusic * k;
                await UniTask.Yield();
            }
            if (_currentMusicSource != null) _currentMusicSource.Stop();
            _currentMusicSource = next;
        }

        /// <summary>Arrête le BGM en fondu.</summary>
        public async UniTask StopMusic(float crossfade = 1f)
        {
            if (_currentMusicSource == null) return;
            var t = 0f;
            while (t < crossfade && _currentMusicSource != null)
            {
                t += Time.unscaledDeltaTime;
                _currentMusicSource.volume = _busMusic * (1f - t / crossfade);
                await UniTask.Yield();
            }
            if (_currentMusicSource != null) { _currentMusicSource.Stop(); _currentMusicSource = null; }
        }

        // --- SFX one-shot ---

        /// <summary>Joue un SFX à position 3D (spatialisation si AudioListener proche).</summary>
        public void PlaySfx(AudioClip clip, Vector3? worldPos = null, float volume = 1f, float pitchBase = 1f)
        {
            if (clip == null) return;
            if (_activeSfx.Count >= _maxConcurrentSfx)
            {
                // Limite mobile: ignore le son le plus ancien en recyclant la première source.
                var oldest = _activeSfx[0];
                oldest.Stop(); oldest.clip = null;
                _activeSfx.RemoveAt(0);
                _sfxPool.Push(oldest);
            }
            var src = _sfxPool.Count > 0 ? _sfxPool.Pop() : Instantiate(_sfxPrefab, transform);
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * _busSfx;
            src.pitch = Mathf.Max(0.1f, pitchBase + UnityEngine.Random.Range(-_pitchJitter, _pitchJitter));
            src.spatialBlend = worldPos.HasValue ? 1f : 0f;
            if (worldPos.HasValue) src.transform.position = worldPos.Value;
            src.Play();
            _activeSfx.Add(src);
        }

        /// <summary>Joue un son de voix (non spatialisé, prioritaire).</summary>
        public void PlayVoice(AudioClip clip, float volume = 1f)
        {
            if (_voiceSource == null || clip == null) return;
            _voiceSource.clip = clip;
            _voiceSource.volume = Mathf.Clamp01(volume) * _busVoice;
            _voiceSource.Play();
        }

#if KINETICS_FMOD
        /// <summary>Joue un événement FMOD par chemin (ex: "event:/SFX/Weapon/Shot_Rifle").</summary>
        public void PlayFmodOneShot(string eventPath, Vector3 worldPos)
        {
            try { RuntimeManager.PlayOneShot(eventPath, worldPos); }
            catch (Exception ex) { Debug.LogWarning($"[AudioManager] FMOD one-shot échec {eventPath}: {ex}"); }
        }

        /// <summary>Joue un événement FMOD attaché à un transform (suivi 3D).</summary>
        public FMOD.Studio.EventInstance PlayFmodAttached(string eventPath, Transform follow)
        {
            try
            {
                var inst = RuntimeManager.CreateInstance(eventPath);
                RuntimeManager.AttachInstanceToGameObject(inst, follow);
                inst.start();
                return inst;
            }
            catch (Exception ex) { Debug.LogWarning($"[AudioManager] FMOD attached échec {eventPath}: {ex}"); return default; }
        }
#endif

        // --- Setup interne ---

        private void EnsureListener()
        {
            if (FindObjectOfType<AudioListener>() == null)
            {
                var go = new GameObject("[AudioListener]");
                go.transform.SetParent(transform, false);
                _listener = go.AddComponent<AudioListener>();
            }
            else _listener = FindObjectOfType<AudioListener>();
        }

        private void EnsureSources()
        {
            if (_musicSourceA == null)
            {
                _musicSourceA = CreateChildSource("MusicA");
                _musicSourceB = CreateChildSource("MusicB");
                _voiceSource = CreateChildSource("Voice");
            }
            _currentMusicSource = _musicSourceA;
            _nextMusicSource = _musicSourceB;

            if (_sfxPrefab == null)
            {
                var go = new GameObject("SfxPrefab");
                go.transform.SetParent(transform, false);
                _sfxPrefab = go.AddComponent<AudioSource>();
                _sfxPrefab.playOnAwake = false;
                _sfxPrefab.spatialize = true;
                _sfxPrefab.rolloffMode = AudioRolloffMode.Linear;
                _sfxPrefab.minDistance = 1f;
                _sfxPrefab.maxDistance = 30f;
            }
            // Pré-chauffe du pool SFX.
            for (int i = 0; i < 8; i++)
            {
                var src = Instantiate(_sfxPrefab, transform);
                src.gameObject.SetActive(false);
                _sfxPool.Push(src);
            }
        }

        private AudioSource CreateChildSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            return src;
        }
    }
}
