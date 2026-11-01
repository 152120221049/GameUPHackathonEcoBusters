using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Eko-Mucit Merkezi Ses (Audio) ve Görsel Tatmin (Oomph / Game Feel / Juice) Yöneticisi.
/// - Harici ses dosyası gerektirmeden bellekte prosedürel olarak tok ve tatmin edici
///   ses efektleri (Vakum uğultusu, Kombo toplama pop sesi, İnşa/Tamir vuruşu, Eko-Arpej, Adım sesi)
///   ve huzurlu ambiyans müziği üretir (İstenirse Inspector'dan özel AudioClip de atanabilir).
/// - Ayarlar menüsündeki Master, Müzik, SFX ve Ekran Sarsıntısı (Screen Shake) ayarlarını canlı uygular.
/// - Dünya uzayında uçan metinler (+1 Metal Hurda, +40 Eko-Kredi, KOMBO!), Voxel parçacık patlamaları,
///   Elastik Squash & Stretch animasyonları ve Kamera Sarsıntısı / Zoom Punch efektlerini tetikler.
/// </summary>
public class GameAudioAndJuiceManager : MonoBehaviour
{
    public static GameAudioAndJuiceManager Instance { get; private set; }

    public const string PREF_MASTER_VOL = "MasterVol";
    public const string PREF_MUSIC_VOL = "MusicVol";
    public const string PREF_SFX_VOL = "SfxVol";
    public const string PREF_SHAKE_INTENSITY = "ShakeIntensity";

    [Header("--- SES KAYNAKLARI ---")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource vacuumLoopSource;
    [SerializeField] private AudioSource musicSource;

    [Header("--- ÖZEL SES KLİPLERİ (BOŞSA PROSEDÜREL ÜRETİLİR) ---")]
    [SerializeField] private AudioClip clipCollectPop;
    [SerializeField] private AudioClip clipVacuumLoop;
    [SerializeField] private AudioClip clipFootstep;
    [SerializeField] private AudioClip clipBuildPlace;
    [SerializeField] private AudioClip clipCraftSuccess;
    [SerializeField] private AudioClip clipPurifyOrRepair;
    [SerializeField] private AudioClip clipHarvest;
    [SerializeField] private AudioClip clipUiClick;
    [SerializeField] private AudioClip clipError;
    [SerializeField] private AudioClip clipAmbientMusic;
    [SerializeField] private AudioClip clipMainMenuMusic;
    [SerializeField] private AudioClip clipJump;

    // Canlı Ayar Değerleri
    public float MasterVolume { get; private set; } = 0.85f;
    public float MusicVolume { get; private set; } = 0.55f;
    public float SfxVolume { get; private set; } = 0.90f;
    public float ScreenShakeIntensity { get; private set; } = 1.0f;

    // Vakum Toplama Kombosu (Arka arkaya çektikçe perde/pitch yükselir -> Slime Rancher / Stardew hissi)
    private int collectComboCount = 0;
    private float lastCollectTime = -10f;
    private float lastFootstepTime = -10f;

    // Uçan metin ve efekt havuzu
    private readonly List<FloatingPopupItem> activePopups = new List<FloatingPopupItem>();

    private class FloatingPopupItem
    {
        public GameObject root;
        public TextMeshPro textMesh;
        public Vector3 velocity;
        public float elapsed;
        public float duration;
        public Color baseColor;
        public float initialScale;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance == null)
        {
            var existing = FindFirstObjectByType<GameAudioAndJuiceManager>();
            if (existing == null)
            {
                GameObject go = new GameObject("GameAudioAndJuiceManager");
                Instance = go.AddComponent<GameAudioAndJuiceManager>();
                DontDestroyOnLoad(go);
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedSettings();
        SetupAudioSources();
        SynthesizeProceduralAudioClips();
        ApplyAudioVolumes();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            PlayMainMenuMusic();
        }
        else
        {
            PlayGameplayMusic();
        }
    }

    private void Start()
    {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            PlayMainMenuMusic();
        }
        else
        {
            PlayGameplayMusic();
        }
    }

    public void PlayMainMenuMusic()
    {
        AudioClip clip = clipMainMenuMusic;
#if UNITY_EDITOR
        if (clip == null)
        {
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ArtAssets/Sounds/MainMenuMusic.wav");
            clipMainMenuMusic = clip;
        }
#endif
        if (clip == null) clip = clipAmbientMusic;

        if (musicSource != null && clip != null)
        {
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    public void PlayGameplayMusic()
    {
        AudioClip clip = clipAmbientMusic;
#if UNITY_EDITOR
        if (clip == null)
        {
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ArtAssets/Sounds/Background_Loop.wav");
            clipAmbientMusic = clip;
        }
#endif
        if (musicSource != null && clip != null)
        {
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    private void Update()
    {
        UpdateFloatingPopups();
    }

    #region Ayarlar ve Ses Seviyesi Yönetimi
    public void LoadSavedSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.85f);
        MusicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.55f);
        SfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.90f);
        ScreenShakeIntensity = PlayerPrefs.GetFloat(PREF_SHAKE_INTENSITY, 1.0f);
    }

    public void SetMasterVolume(float val)
    {
        MasterVolume = Mathf.Clamp01(val);
        PlayerPrefs.SetFloat(PREF_MASTER_VOL, MasterVolume);
        PlayerPrefs.Save();
        ApplyAudioVolumes();
    }

    public void SetMusicVolume(float val)
    {
        MusicVolume = Mathf.Clamp01(val);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOL, MusicVolume);
        PlayerPrefs.Save();
        ApplyAudioVolumes();
    }

    public void SetSfxVolume(float val)
    {
        SfxVolume = Mathf.Clamp01(val);
        PlayerPrefs.SetFloat(PREF_SFX_VOL, SfxVolume);
        PlayerPrefs.Save();
        ApplyAudioVolumes();
    }

    public void SetScreenShakeIntensity(float val)
    {
        ScreenShakeIntensity = Mathf.Clamp(val, 0f, 2f);
        PlayerPrefs.SetFloat(PREF_SHAKE_INTENSITY, ScreenShakeIntensity);
        PlayerPrefs.Save();
    }

    public void ApplyAudioVolumes()
    {
        AudioListener.volume = MasterVolume;
        if (musicSource != null)
        {
            musicSource.volume = MusicVolume * 0.42f;
        }
        if (sfxSource != null)
        {
            sfxSource.volume = SfxVolume;
        }
        if (vacuumLoopSource != null && vacuumLoopSource.isPlaying)
        {
            vacuumLoopSource.volume = SfxVolume * 0.45f;
        }
    }

    private void SetupAudioSources()
    {
        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFX_Source");
            sfxObj.transform.SetParent(transform, false);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        if (vacuumLoopSource == null)
        {
            GameObject vacObj = new GameObject("VacuumLoop_Source");
            vacObj.transform.SetParent(transform, false);
            vacuumLoopSource = vacObj.AddComponent<AudioSource>();
            vacuumLoopSource.playOnAwake = false;
            vacuumLoopSource.loop = true;
            vacuumLoopSource.spatialBlend = 0f;
        }

        if (musicSource == null)
        {
            GameObject musObj = new GameObject("AmbientMusic_Source");
            musObj.transform.SetParent(transform, false);
            musicSource = musObj.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }
    }
    #endregion

    #region Prosedürel Ses Sentezleyici & Disk Ses Yükleyici
    public void LoadSoundAssetsFromDisk()
    {
#if UNITY_EDITOR
        AudioClip LoadWav(string fileName)
        {
            string path = $"Assets/ArtAssets/Sounds/{fileName}.wav";
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        if (clipAmbientMusic == null) clipAmbientMusic = LoadWav("Background_Loop");
        if (clipMainMenuMusic == null) clipMainMenuMusic = LoadWav("MainMenuMusic");
        if (clipUiClick == null) clipUiClick = LoadWav("Button");
        if (clipCollectPop == null) clipCollectPop = LoadWav("Collect");
        if (clipHarvest == null) clipHarvest = LoadWav("Collect");
        if (clipBuildPlace == null) clipBuildPlace = LoadWav("Craft");
        if (clipCraftSuccess == null) clipCraftSuccess = LoadWav("Craft");
        if (clipPurifyOrRepair == null) clipPurifyOrRepair = LoadWav("Success");
        if (clipJump == null) clipJump = LoadWav("Jump");
#endif
    }

    private void SynthesizeProceduralAudioClips()
    {
        LoadSoundAssetsFromDisk();

        int sampleRate = 44100;

        if (clipCollectPop == null)
            clipCollectPop = CreateCollectPopClip(sampleRate);

        if (clipVacuumLoop == null)
        {
            clipVacuumLoop = CreateVacuumLoopClip(sampleRate);
            if (vacuumLoopSource != null) vacuumLoopSource.clip = clipVacuumLoop;
        }

        if (clipFootstep == null)
            clipFootstep = CreateFootstepClip(sampleRate);

        if (clipBuildPlace == null)
            clipBuildPlace = CreateBuildPlaceClip(sampleRate);

        if (clipCraftSuccess == null)
            clipCraftSuccess = CreateChordFanfareClip("SFX_CraftSuccess", sampleRate, new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.48f, true);

        if (clipPurifyOrRepair == null)
            clipPurifyOrRepair = CreateChordFanfareClip("SFX_PurifyRepair", sampleRate, new float[] { 440f, 554.37f, 659.25f, 880f, 1108.73f }, 0.68f, true);

        if (clipHarvest == null)
            clipHarvest = CreateHarvestClip(sampleRate);

        if (clipUiClick == null)
            clipUiClick = CreateUiClickClip(sampleRate);

        if (clipError == null)
            clipError = CreateErrorBuzzClip(sampleRate);

        if (clipAmbientMusic == null)
            clipAmbientMusic = CreateCozyAmbientMusicLoop(sampleRate);
    }

    private AudioClip CreateCollectPopClip(int sampleRate)
    {
        // Slime Rancher / Stardew tarzı "Bloop-Pop!" vakum yutma sesi
        int samples = Mathf.RoundToInt(sampleRate * 0.16f);
        float[] data = new float[samples];
        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            // Frekans 260 Hz'den 920 Hz'e hızla kayar (suction pop)
            float freq = Mathf.Lerp(260f, 940f, Mathf.Pow(t, 0.65f));
            phase += (freq * 2f * Mathf.PI) / sampleRate;

            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Pow(1f - t, 0.7f);
            float subPunch = Mathf.Sin(t * 110f * 2f * Mathf.PI) * Mathf.Exp(-t * 25f) * 0.45f;
            data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.65f + subPunch) * env, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("SFX_CollectPop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateVacuumLoopClip(int sampleRate)
    {
        // 1.0 saniyelik kesintisiz türbin + hava akımı uğultusu
        int samples = sampleRate;
        float[] data = new float[samples];
        System.Random rng = new System.Random(42);
        float lowPass = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float turbine = Mathf.Sin(t * 110f * 2f * Mathf.PI) * 0.22f
                          + Mathf.Sin(t * 220f * 2f * Mathf.PI) * 0.14f
                          + Mathf.Sin(t * 330f * 2f * Mathf.PI) * 0.06f;

            float white = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;
            lowPass = Mathf.Lerp(lowPass, white, 0.12f);

            // Döngü başı ve sonu pürüzsüz birleşsin
            float wobble = 0.88f + 0.12f * Mathf.Sin(t * 6f * 2f * Mathf.PI);
            data[i] = (turbine + lowPass) * wobble;
        }

        AudioClip clip = AudioClip.Create("SFX_VacuumLoop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateFootstepClip(int sampleRate)
    {
        int samples = Mathf.RoundToInt(sampleRate * 0.075f);
        float[] data = new float[samples];
        System.Random rng = new System.Random(101);
        float lp = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env = Mathf.Exp(-t * 28f);
            float noise = ((float)rng.NextDouble() * 2f - 1f);
            lp = Mathf.Lerp(lp, noise, 0.25f);
            float thud = Mathf.Sin(t * 95f * 2f * Mathf.PI) * Mathf.Exp(-t * 35f);
            data[i] = (lp * 0.35f + thud * 0.45f) * env;
        }

        AudioClip clip = AudioClip.Create("SFX_Footstep", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateBuildPlaceClip(int sampleRate)
    {
        // Tok bir inşa çekici/zemin vuruşu + parlak onay tınısı
        int samples = Mathf.RoundToInt(sampleRate * 0.26f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            float t = (float)i / samples;

            // 1. Alt darbe (Punchy bass thud)
            float bassFreq = Mathf.Lerp(160f, 52f, Mathf.Clamp01(t * 3f));
            float bass = Mathf.Sin(time * bassFreq * 2f * Mathf.PI) * Mathf.Exp(-t * 14f) * 0.75f;

            // 2. Üst mekanik tık + akor
            float chime = (Mathf.Sin(time * 587.33f * 2f * Mathf.PI) + Mathf.Sin(time * 880f * 2f * Mathf.PI) * 0.6f)
                          * Mathf.Exp(-t * 9f) * 0.35f;

            data[i] = Mathf.Clamp(bass + chime, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("SFX_BuildPlace", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateChordFanfareClip(string name, int sampleRate, float[] notes, float duration, bool addHammerClink)
    {
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            float t = (float)i / samples;
            float sample = 0f;

            if (addHammerClink && time < 0.08f)
            {
                float clinkEnv = Mathf.Exp(-time * 55f);
                sample += (Mathf.Sin(time * 1850f * 2f * Mathf.PI) + Mathf.Sin(time * 2420f * 2f * Mathf.PI)) * 0.25f * clinkEnv;
            }

            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = n * (duration * 0.14f);
                if (time >= noteStart)
                {
                    float localT = (time - noteStart) / (duration - noteStart);
                    float env = Mathf.Sin(Mathf.Clamp01(localT * 8f) * Mathf.PI * 0.5f) * Mathf.Exp(-localT * 4.2f);
                    float freq = notes[n];
                    float wave = Mathf.Sin(time * freq * 2f * Mathf.PI) * 0.7f
                               + Mathf.Sin(time * freq * 2f * 2f * Mathf.PI) * 0.2f;
                    sample += wave * env * (0.45f / notes.Length) * 2.2f;
                }
            }

            data[i] = Mathf.Clamp(sample, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateHarvestClip(int sampleRate)
    {
        int samples = Mathf.RoundToInt(sampleRate * 0.24f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            float t = (float)i / samples;
            float freq = time < 0.09f ? Mathf.Lerp(340f, 680f, time / 0.09f) : 880f;
            float env = Mathf.Exp(-t * 8.5f);
            data[i] = Mathf.Clamp(Mathf.Sin(time * freq * 2f * Mathf.PI) * env * 0.65f, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("SFX_Harvest", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateUiClickClip(int sampleRate)
    {
        int samples = Mathf.RoundToInt(sampleRate * 0.055f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            float t = (float)i / samples;
            float env = Mathf.Exp(-t * 32f);
            data[i] = Mathf.Sin(time * 1050f * 2f * Mathf.PI) * env * 0.5f;
        }

        AudioClip clip = AudioClip.Create("SFX_UiClick", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateErrorBuzzClip(int sampleRate)
    {
        int samples = Mathf.RoundToInt(sampleRate * 0.22f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            float t = (float)i / samples;
            float freq = time < 0.11f ? 190f : 145f;
            float env = Mathf.Exp(-t * 6f);
            float saw = (Mathf.Sin(time * freq * 2f * Mathf.PI) + 0.5f * Mathf.Sin(time * freq * 4f * Mathf.PI));
            data[i] = Mathf.Clamp(saw * env * 0.4f, -0.9f, 0.9f);
        }

        AudioClip clip = AudioClip.Create("SFX_ErrorBuzz", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateCozyAmbientMusicLoop(int sampleRate)
    {
        // 16 saniyelik sıcak, huzurlu izometrik ada ambiyans döngüsü (C Maj9 -> A Min7 -> F Maj7 -> G6)
        float duration = 16.0f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];

        float[][] chords = new float[][]
        {
            new float[] { 261.63f, 329.63f, 392.00f, 493.88f }, // Cmaj7
            new float[] { 220.00f, 261.63f, 329.63f, 392.00f }, // Am7
            new float[] { 174.61f, 261.63f, 329.63f, 440.00f }, // Fmaj7
            new float[] { 196.00f, 246.94f, 293.66f, 392.00f }  // G6
        };

        float[] melodyNotes = new float[]
        {
            523.25f, 659.25f, 783.99f, 659.25f,
            440.00f, 523.25f, 659.25f, 587.33f,
            523.25f, 440.00f, 392.00f, 523.25f,
            493.88f, 587.33f, 659.25f, 587.33f
        };

        for (int i = 0; i < samples; i++)
        {
            float time = (float)i / sampleRate;
            int chordIdx = Mathf.FloorToInt(time / 4.0f) % chords.Length;
            float chordLocalTime = time % 4.0f;
            float padEnv = Mathf.Sin(Mathf.Clamp01(chordLocalTime / 0.6f) * Mathf.PI * 0.5f)
                         * Mathf.Sin(Mathf.Clamp01((4.0f - chordLocalTime) / 0.6f) * Mathf.PI * 0.5f);

            float pad = 0f;
            float[] chord = chords[chordIdx];
            for (int c = 0; c < chord.Length; c++)
            {
                pad += Mathf.Sin(time * chord[c] * 2f * Mathf.PI) * 0.11f;
            }
            pad *= padEnv;

            // Yumuşak marimba/kalimba arpej notası (her saniyede bir)
            int melIdx = Mathf.FloorToInt(time) % melodyNotes.Length;
            float melLocal = time % 1.0f;
            float melEnv = Mathf.Exp(-melLocal * 5.5f) * Mathf.Sin(Mathf.Clamp01(melLocal * 40f) * Mathf.PI * 0.5f);
            float melFreq = melodyNotes[melIdx];
            float marimba = (Mathf.Sin(time * melFreq * 2f * Mathf.PI) + 0.25f * Mathf.Sin(time * melFreq * 4f * Mathf.PI)) * melEnv * 0.16f;

            data[i] = Mathf.Clamp(pad + marimba, -0.85f, 0.85f);
        }

        AudioClip clip = AudioClip.Create("Music_CozyEcoIsland", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
    #endregion

    #region Oomph / Ses & Görsel Tetikleyiciler (Public API)
    /// <summary>
    /// Vakum aktifken sürekli türbin/hava akımı sesini çalıştırır, bırakıldığında yumuşakça susturur.
    /// </summary>
    public void SetVacuumSoundActive(bool active, int pulledItemsCount = 0)
    {
        if (vacuumLoopSource == null) return;

        if (active)
        {
            if (!vacuumLoopSource.isPlaying && clipVacuumLoop != null)
            {
                vacuumLoopSource.clip = clipVacuumLoop;
                vacuumLoopSource.Play();
            }
            float targetPitch = 0.95f + Mathf.Clamp(pulledItemsCount * 0.08f, 0f, 0.45f) + Mathf.Sin(Time.time * 12f) * 0.03f;
            vacuumLoopSource.pitch = Mathf.Lerp(vacuumLoopSource.pitch, targetPitch, Time.deltaTime * 10f);
            vacuumLoopSource.volume = Mathf.Lerp(vacuumLoopSource.volume, SfxVolume * 0.42f, Time.deltaTime * 12f);
        }
        else if (vacuumLoopSource.isPlaying)
        {
            vacuumLoopSource.volume = Mathf.MoveTowards(vacuumLoopSource.volume, 0f, Time.deltaTime * 3.5f);
            if (vacuumLoopSource.volume <= 0.01f)
            {
                vacuumLoopSource.Stop();
            }
        }
    }

    public void PlayFootstep()
    {
        if (Time.time - lastFootstepTime < 0.24f) return;
        lastFootstepTime = Time.time;

        if (sfxSource != null && clipFootstep != null)
        {
            sfxSource.pitch = Random.Range(0.88f, 1.14f);
            sfxSource.PlayOneShot(clipFootstep, SfxVolume * 0.45f);
        }
    }

    /// <summary>
    /// Bir atık vakumla çekilip çantaya girdiğinde Kombo sesini, Voxel patlamasını,
    /// uçan +1 metnini ve kamera vuruşunu (Oomph!) tetikler.
    /// </summary>
    public void TriggerItemCollectedOomph(Vector3 worldPos, ItemType type, int amount, Transform playerOrNozzle = null)
    {
        if (Time.time - lastCollectTime < 1.35f)
            collectComboCount = Mathf.Min(collectComboCount + 1, 12);
        else
            collectComboCount = 0;

        lastCollectTime = Time.time;

        // 1. Komboya göre yükselen Pop sesi
        if (sfxSource != null && clipCollectPop != null)
        {
            float comboPitch = 0.95f + (collectComboCount * 0.065f);
            sfxSource.pitch = comboPitch;
            sfxSource.PlayOneShot(clipCollectPop, SfxVolume * 0.95f);
        }

        // 2. Renkli Voxel Parçacık Patlaması
        Color itemColor = ItemDatabase.GetItemColor(type);
        SpawnBurstVFX(worldPos, itemColor, 10, 0.85f);

        // 3. Uçan Metin (+1 Metal Hurda + Kombo)
        string comboTag = collectComboCount >= 2 ? $" <size=80%><color=#FFD700>(x{collectComboCount + 1}!)</color></size>" : "";
        SpawnFloatingText(worldPos + Vector3.up * 0.5f, $"+{amount} {ItemDatabase.GetDisplayName(type)}{comboTag}", itemColor, 1.05f);

        // 4. Oyuncu / Vakum Namlusu Elastik Tepkisi & Kamera Mikro-Vuruşu
        // Fizik kök nesnesini (CharacterController25D) ezmek yerine yalnızca görsel nesnelere veya hafif vuruş uygula
        if (playerOrNozzle != null && playerOrNozzle.GetComponent<CharacterController25D>() == null)
        {
            PlaySquashAndStretch(playerOrNozzle, 1.08f, 0.14f);
        }
        TriggerCameraShake(0.08f, 0.10f, -0.025f);
    }

    /// <summary>
    /// Bina inşa edildiğinde, makine kurulduğunda veya fidan dikildiğinde tok vuruş + toz/parıltı halkası üretir.
    /// </summary>
    public void TriggerBuildOrPlantOomph(Vector3 worldPos, string label, Color color, Transform targetTransform = null)
    {
        if (sfxSource != null && clipBuildPlace != null)
        {
            sfxSource.pitch = Random.Range(0.95f, 1.06f);
            sfxSource.PlayOneShot(clipBuildPlace, SfxVolume);
        }

        SpawnBurstVFX(worldPos + Vector3.up * 0.25f, color, 16, 1.25f);
        SpawnFloatingText(worldPos + Vector3.up * 1.4f, label, color, 1.25f);

        if (targetTransform != null)
        {
            PlaySquashAndStretch(targetTransform, 1.32f, 0.32f);
        }
        TriggerCameraShake(0.18f, 0.20f, -0.05f);
    }

    /// <summary>
    /// Baca filtresi takıldığında, traktör/değirmen onarıldığında veya nehir dubası çalıştığında
    /// coşkulu Eko-Arpej sesi, büyük ışık patlaması ve kamera zoom punch tetikler.
    /// </summary>
    public void TriggerPurifyOrRepairOomph(Vector3 worldPos, string title, Color color, Transform targetTransform = null)
    {
        if (sfxSource != null && clipPurifyOrRepair != null)
        {
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(clipPurifyOrRepair, SfxVolume);
        }

        SpawnBurstVFX(worldPos + Vector3.up * 0.6f, color, 26, 1.85f);
        SpawnFloatingText(worldPos + Vector3.up * 1.9f, $"★ {title} ★", color, 1.45f);

        if (targetTransform != null)
        {
            PlaySquashAndStretch(targetTransform, 1.35f, 0.42f);
        }
        TriggerCameraShake(0.28f, 0.32f, -0.09f);
    }

    /// <summary>
    /// Tarla mahsulü, ağaç meyvesi veya duba atığı hasat edildiğinde tetiklenir.
    /// </summary>
    public void TriggerHarvestOomph(Vector3 worldPos, string text, Color color, Transform targetTransform = null)
    {
        if (sfxSource != null && clipHarvest != null)
        {
            sfxSource.pitch = Random.Range(0.95f, 1.12f);
            sfxSource.PlayOneShot(clipHarvest, SfxVolume * 0.95f);
        }

        SpawnBurstVFX(worldPos + Vector3.up * 0.4f, color, 14, 1.1f);
        SpawnFloatingText(worldPos + Vector3.up * 1.2f, text, color, 1.2f);

        if (targetTransform != null)
        {
            PlaySquashAndStretch(targetTransform, 1.28f, 0.25f);
        }
        TriggerCameraShake(0.12f, 0.14f, -0.035f);
    }

    /// <summary>
    /// Atölyede üretim veya ekipman yükseltmesi yapıldığında tetiklenir.
    /// </summary>
    public void TriggerCraftOrUpgradeOomph(string itemOrUpgradeName)
    {
        if (sfxSource != null && clipCraftSuccess != null)
        {
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(clipCraftSuccess, SfxVolume);
        }

        var player = FindFirstObjectByType<CharacterController25D>();
        if (player != null)
        {
            Vector3 pos = player.transform.position;
            SpawnBurstVFX(pos + Vector3.up * 0.9f, new Color(1f, 0.85f, 0.25f), 20, 1.4f);
            SpawnFloatingText(pos + Vector3.up * 2.1f, $"ÜRETİLDİ: {itemOrUpgradeName}!", new Color(0.35f, 1f, 0.55f), 1.35f);
            PlaySquashAndStretch(player.transform, 1.24f, 0.28f);
        }
        TriggerCameraShake(0.20f, 0.22f, -0.06f);
    }

    public void PlayUiClick()
    {
        if (sfxSource != null && clipUiClick != null)
        {
            sfxSource.pitch = Random.Range(0.96f, 1.05f);
            sfxSource.PlayOneShot(clipUiClick, SfxVolume * 0.8f);
        }
    }

    public void PlayJumpSound(Vector3? worldPos = null, float volumeMultiplier = 0.9f, float customPitch = 1.0f)
    {
        if (sfxSource != null && clipJump != null)
        {
            sfxSource.pitch = (customPitch > 0.1f) ? (customPitch * Random.Range(0.96f, 1.05f)) : Random.Range(0.96f, 1.06f);
            sfxSource.PlayOneShot(clipJump, SfxVolume * volumeMultiplier);
        }
    }

    public void PlayErrorSound(Vector3? worldPos = null, string errorLabel = null)
    {
        if (sfxSource != null && clipError != null)
        {
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(clipError, SfxVolume * 0.75f);
        }

        if (worldPos.HasValue && !string.IsNullOrEmpty(errorLabel))
        {
            SpawnFloatingText(worldPos.Value + Vector3.up * 1.4f, errorLabel, new Color(1f, 0.35f, 0.35f), 1.1f);
        }
        TriggerCameraShake(0.10f, 0.12f, 0f);
    }

    public void TriggerCameraShake(float duration, float magnitude, float zoomKick = 0f)
    {
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow25D>() : FindFirstObjectByType<CameraFollow25D>();
        if (cam != null)
        {
            cam.TriggerOomphShake(duration, magnitude * ScreenShakeIntensity, zoomKick * ScreenShakeIntensity);
        }
    }
    #endregion

    #region 3B Uçan Metinler, Voxel Patlamaları & Squash-Stretch
    public void SpawnFloatingText(Vector3 worldPos, string message, Color color, float scaleMultiplier = 1f)
    {
        GameObject go = new GameObject("FloatingJuiceText");
        go.transform.position = worldPos + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = message;
        tmp.fontSize = 4.2f * scaleMultiplier;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = new Color(0.05f, 0.07f, 0.1f, 0.95f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.sortingOrder = 200;

        if (Camera.main != null)
        {
            go.transform.rotation = Camera.main.transform.rotation;
        }

        activePopups.Add(new FloatingPopupItem
        {
            root = go,
            textMesh = tmp,
            velocity = new Vector3(Random.Range(-0.4f, 0.4f), 2.8f, Random.Range(-0.4f, 0.4f)),
            elapsed = 0f,
            duration = 1.15f,
            baseColor = color,
            initialScale = 1f
        });
    }

    private void UpdateFloatingPopups()
    {
        Quaternion camRot = Camera.main != null ? Camera.main.transform.rotation : Quaternion.Euler(40f, 45f, 0f);

        for (int i = activePopups.Count - 1; i >= 0; i--)
        {
            var item = activePopups[i];
            if (item.root == null)
            {
                activePopups.RemoveAt(i);
                continue;
            }

            item.elapsed += Time.unscaledDeltaTime;
            float t = item.elapsed / item.duration;

            if (t >= 1f)
            {
                Destroy(item.root);
                activePopups.RemoveAt(i);
                continue;
            }

            item.velocity.y = Mathf.Max(0.4f, item.velocity.y - 3.2f * Time.unscaledDeltaTime);
            item.root.transform.position += item.velocity * Time.unscaledDeltaTime;
            item.root.transform.rotation = camRot;

            // İlk %18'lik dilimde elastik büyüme (Pop-in), son %35'te şeffaflaşma
            float scalePop = t < 0.18f
                ? Mathf.Lerp(0.2f, 1.25f, t / 0.18f)
                : Mathf.Lerp(1.25f, 0.92f, (t - 0.18f) / 0.82f);
            item.root.transform.localScale = Vector3.one * (item.initialScale * scalePop);

            if (t > 0.65f)
            {
                float alpha = 1f - ((t - 0.65f) / 0.35f);
                Color c = item.baseColor;
                c.a = alpha;
                item.textMesh.color = c;
            }
        }
    }

    public void SpawnBurstVFX(Vector3 worldPos, Color color, int particleCount = 12, float speedScale = 1f)
    {
        GameObject vfxObj = new GameObject("VoxelJuiceBurst");
        vfxObj.transform.position = worldPos;

        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f * speedScale, 5.2f * speedScale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f * speedScale, 0.26f * speedScale);
        main.gravityModifier = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.45f));

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)particleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(0.7f, 0.85f);
        curve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var psRenderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        psRenderer.mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempCube);

        Shader litShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        if (litShader != null)
        {
            Material mat = new Material(litShader);
            mat.color = color;
            psRenderer.material = mat;
        }

        ps.Play();
        Destroy(vfxObj, 1.0f);
    }

    private readonly Dictionary<Transform, Vector3> originalTargetScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Coroutine> activeSquashRoutines = new Dictionary<Transform, Coroutine>();

    public void PlaySquashAndStretch(Transform target, float peakMultiplier = 1.25f, float duration = 0.25f)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        if (activeSquashRoutines.TryGetValue(target, out Coroutine existingRoutine) && existingRoutine != null)
        {
            StopCoroutine(existingRoutine);
            activeSquashRoutines.Remove(target);
        }

        if (!originalTargetScales.ContainsKey(target))
        {
            Vector3 initScale = target.localScale;
            if (initScale.sqrMagnitude < 0.001f) initScale = Vector3.one;
            originalTargetScales[target] = initScale;
        }

        Coroutine c = StartCoroutine(SquashStretchRoutine(target, peakMultiplier, duration));
        activeSquashRoutines[target] = c;
    }

    private IEnumerator SquashStretchRoutine(Transform target, float peak, float duration)
    {
        if (target == null) yield break;

        Vector3 canonicalBase = originalTargetScales.TryGetValue(target, out Vector3 stored) ? stored : target.localScale;
        if (canonicalBase.sqrMagnitude < 0.0001f) canonicalBase = Vector3.one;

        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Sönümlü elastik dalga (Aşırı basıklaşmayı engellemek için scaleY alt sınırı %92 ile sınırlandırılır)
            float wave = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t);
            float scaleXZ = Mathf.Clamp(1f + wave * (peak - 1f), 0.85f, 1.4f);
            float scaleY = Mathf.Clamp(1f - wave * (peak - 1f) * 0.5f, 0.92f, 1.35f);

            target.localScale = new Vector3(canonicalBase.x * scaleXZ, canonicalBase.y * scaleY, canonicalBase.z * scaleXZ);
            yield return null;
        }

        if (target != null)
        {
            target.localScale = canonicalBase;
            activeSquashRoutines.Remove(target);
            originalTargetScales.Remove(target);
        }
    }
    #endregion
}

