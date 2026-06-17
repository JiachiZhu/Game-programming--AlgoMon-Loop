/*
Script Audit:
- Purpose: Owns cross-scene background music. Picks the right track for each scene/encounter and crossfades between them.
- Attached GameObject: Persistent "AudioManager" instantiated from Resources/AudioManager prefab via Bootstrap/EnsureInstance (mirrors GameManager).
- Main responsibilities: Play menu/grid/battle/boss/victory BGM driven by SceneManager.sceneLoaded, expose a player-selectable menu track + music volume for the Settings UI, and persist those choices in PlayerPrefs.
- Important variables: Instance, menuTracks, gridExplorationMusic, battleWildEliteMusic, battleHackerMusic, battleBossMusic, victoryTracks, musicVolume, selectedMenuTrackIndex.
- Inputs: Unity SceneManager.sceneLoaded, GameManager run state (current node type, pending run outcome), and Settings UI calls.
- Outputs or effects: Drives two AudioSources with a crossfade; writes PlayerPrefs for the selected menu track and music volume.
- AI/tutorial/template assistance: Drafted with AI assistance; scene/encounter mapping was checked against GameManager scene flow and node types.
- Testing notes: Enter MainTerminal / TheGrid / TheArena (Combat, Elite, Hacker, Boss) / RunResult and confirm the matching track plays and crossfades; change the menu track + volume in Settings and confirm both survive a restart.
*/
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Game-wide UI sound effects, routed through AudioManager.PlayUiSfx so call
/// sites never juggle raw AudioClip references and spam is debounced centrally.
/// </summary>
// Defense note: UiSfx defines the valid ui sfx options used by the gameplay systems.
public enum UiSfx
{
    Hover,        // pointer enters an interactable button
    Click,        // button press
    ZoomEnable,   // terminal zoom turned on
    ZoomDisable,  // terminal zoom turned off
    Invalid,      // action rejected — gameplay condition not met
    Impact,       // grid node entry / pre-battle transition
}

/// <summary>
/// Singleton that owns all background music. Persists across scene loads via
/// DontDestroyOnLoad and selects a track from the current scene plus the active
/// run state, so it never has to be wired into individual scene controllers.
///
/// Music map (see README "Music (Pixabay)" credits for sources):
///   MainTerminal / TheLab — one of the player-selectable menu tracks
///   TheGrid               — grid exploration track
///   TheArena              — battle track chosen by node type:
///                             Combat / Elite -> wild & elite track
///                             Hacker (BREACH) -> advanced-hacker track
///                             Boss            -> boss track
///   RunResult (Victory)   — a random track from the victory pool
///   RunResult (Defeat)    — silence (no defeat track sourced yet)
///
/// The system listens to Unity's SceneManager.sceneLoaded rather than the
/// project EventBus, because EventBus.Clear() wipes every subscription on each
/// scene transition (only GameManager re-subscribes). sceneLoaded is owned by
/// Unity and survives that reset.
/// </summary>
// Defense note: AudioManager coordinates the main state and flow for the audio system.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // Prefab lives at Assets/_AlgoMon/Resources/AudioManager.prefab so it can be
    // instantiated from any entry scene (matches GameManager's auto-bootstrap).
    private const string ResourcePath = "AudioManager";

    private const string MusicVolumeKey = "audio.musicVolume";
    private const string SfxVolumeKey = "audio.sfxVolume";
    private const string MenuTrackKey = "audio.menuTrackIndex";

    [Header("Main Menu — player-selectable (MainTerminal / TheLab)")]
    [Tooltip("Order must match menuTrackNames. Suggested: Hi-Tech Loop, Shadowy Figure, Nightscape.")]
    public AudioClip[] menuTracks;
    [Tooltip("Display labels shown in the Settings music switcher; index-matched to menuTracks.")]
    public string[] menuTrackNames = { "Hi-Tech Loop", "Shadowy Figure", "Nightscape" };

    [Header("The Grid")]
    public AudioClip gridExplorationMusic;       // Synthwave 80s Retro Background — INPLUSMUSIC

    [Header("The Arena — battle tracks")]
    public AudioClip battleWildEliteMusic;       // Light Vortex — Psychronic (Combat + Elite)
    public AudioClip battleHackerMusic;          // Dark Matter Canon — Psychronic (Hacker / BREACH)
    public AudioClip battleBossMusic;            // Flight of the Cosmos — Psychronic (Boss)

    [Header("Victory — random pool (RunResult)")]
    public AudioClip[] victoryTracks;            // Party Celebration (Sonican) + Synthwave Synth-Pop (HitsLab)

    [Header("SFX")]
    [Tooltip("Looping keyboard ambience for the MainTerminal typing character (CC0 — stu556, Freesound 450282). Plays while the character is visible; stops in terminal-zoom mode.")]
    public AudioClip keyboardTypingClip;

    [Header("UI SFX (Sci-Fi UI SFX Pack — Hove Audio)")]
    public AudioClip uiHoverClip;        // Click_Combo — pointer enters a button
    public AudioClip uiClickClip;        // Click_Combo_2 — button press
    public AudioClip uiZoomEnableClip;   // Click_Combo_2_High — terminal zoom on
    public AudioClip uiZoomDisableClip;  // Click_Combo_2_Low — terminal zoom off
    public AudioClip uiInvalidClip;      // Glitch_1 — action rejected (condition not met)
    public AudioClip uiImpactClip;       // Impact_1 — grid node entry / battle transition

    /// <summary>Attack sound for one species; evolvedAttack falls back to baseAttack when unset.</summary>
    [System.Serializable]
    // Defense note: SpeciesAttackSfx groups small runtime values that are passed around together.
    public struct SpeciesAttackSfx
    {
        public string codeName;          // e.g. "Heapion" — matched case-insensitively
        public AudioClip baseAttack;
        public AudioClip evolvedAttack;  // leave empty to reuse baseAttack for the evolved form
    }

    [Header("Battle SFX — per-species attack sounds")]
    public SpeciesAttackSfx[] speciesAttackSfx;

    [Header("Battle SFX — status feedback")]
    [Tooltip("Any positive boost — buff, charge (CP gain), or heal.")]
    public AudioClip statusBuffClip;
    [Tooltip("Any negative status — debuff.")]
    public AudioClip statusDebuffClip;

    [Header("Battle SFX — defense / counter")]
    [Tooltip("Plays when any AlgoMon executes a Defense skill (shared across species).")]
    public AudioClip defenseClip;
    [Tooltip("Plays when an ASD counter succeeds.")]
    public AudioClip counterClip;

    [Header("Economy / outcome SFX")]
    [Tooltip("Successful shop purchase.")]
    public AudioClip purchaseClip;
    [Tooltip("Node-clear reward (winning a non-boss encounter).")]
    public AudioClip rewardClip;
    [Tooltip("Run defeat sting on the RunResult screen.")]
    public AudioClip defeatClip;

    [Header("Mixing")]
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Tooltip("Seconds to crossfade between two tracks when the context changes.")]
    [Min(0f)] public float crossfadeSeconds = 0.75f;

    private AudioSource _sourceA;
    private AudioSource _sourceB;
    private AudioSource _active;          // the source currently fading in / playing
    private AudioClip _currentClip;       // what _active is (or will be) playing
    private Coroutine _fadeRoutine;
    private bool _muted;

    private AudioSource _sfxSource;       // one-shot SFX (button clicks, etc.)
    private AudioSource _keyboardSource;  // looping keyboard ambience

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    // Defense note: Runs the bootstrap helper used by this script.
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    // Defense note: Ensures the instance dependency or state exists before use.
    public static AudioManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        AudioManager prefab = Resources.Load<AudioManager>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning(
                "[AudioManager] No Resources/AudioManager prefab found — background music is disabled. " +
                "Create Assets/_AlgoMon/Resources/AudioManager.prefab from this component and assign the clips.");
            return null;
        }

        AudioManager instance = Instantiate(prefab);
        instance.gameObject.name = nameof(AudioManager);
        return instance;
    }

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Guarantee exactly one AudioListener for the whole game. The gameplay
        // scenes ship without one, so without this nothing is ever audible even
        // though the AudioSources are playing. Self-hosting it on the persistent
        // manager also means it follows every scene load.
        if (GetComponent<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        _sourceA = CreateSource();
        _sourceB = CreateSource();
        _active = _sourceA;

        _sfxSource = CreateSource();
        _sfxSource.loop = false;
        _keyboardSource = CreateSource();
        _keyboardSource.loop = true;

        // Global hover/click/invalid sounds for every Selectable in every scene.
        if (GetComponent<GlobalUiSfxDriver>() == null)
            gameObject.AddComponent<GlobalUiSfxDriver>();

        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume));
        selectedMenuTrackIndex = PlayerPrefs.GetInt(MenuTrackKey, 0);

        SceneManager.sceneLoaded += OnSceneLoaded;
        // sceneLoaded does not fire for the scene that was already active when we
        // bootstrapped, so resolve music for it once on startup.
        HandleScene(SceneManager.GetActiveScene().name);
    }

    // Defense note: Unity lifecycle hook that runs the on destroy step for this component.
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    // Defense note: Creates the source object used by the scene or runtime.
    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // pure 2D BGM
        source.volume = 0f;
        return source;
    }

    // ----------------------------------------------------------------
    // Scene-driven track selection

    // Defense note: Runs the on scene loaded helper used by this script.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleScene(scene.name);
    }

    // Defense note: Handles the scene event or player interaction.
    private void HandleScene(string sceneName)
    {
        if (!System.Enum.TryParse(sceneName, out GameScene scene))
            return; // unknown / non-gameplay scene: leave current music alone

        // Keyboard ambience only lives on the MainTerminal; its controller toggles
        // it by the terminal-zoom state, so kill it the moment we leave that scene.
        if (scene != GameScene.MainTerminal)
            SetKeyboardLoopActive(false);

        switch (scene)
        {
            case GameScene.MainTerminal:
            case GameScene.TheLab:
                PlayMenuMusic();
                break;
            case GameScene.TheGrid:
                PlayBGM(gridExplorationMusic);
                break;
            case GameScene.TheArena:
                PlayBattleMusic();
                break;
            case GameScene.RunResult:
                PlayResultMusic();
                break;
        }
    }

    // Defense note: Plays the battle music animation, audio, or feedback.
    private void PlayBattleMusic()
    {
        NodeType type = NodeType.Combat;
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.currentRunGraph != null && !string.IsNullOrEmpty(gm.currentNodeId))
        {
            GridNode node = gm.currentRunGraph.GetNode(gm.currentNodeId);
            if (node != null)
                type = node.nodeType;
        }

        switch (type)
        {
            case NodeType.Hacker:
                PlayBGM(battleHackerMusic);
                break;
            case NodeType.Boss:
                PlayBGM(battleBossMusic);
                break;
            default: // Combat, Elite, and any fallback
                PlayBGM(battleWildEliteMusic);
                break;
        }
    }

    // Defense note: Plays the result music animation, audio, or feedback.
    private void PlayResultMusic()
    {
        GameManager gm = GameManager.Instance;
        bool victory = gm != null && gm.pendingRunOutcome == RunOutcome.Victory;
        if (victory && victoryTracks != null && victoryTracks.Length > 0)
        {
            PlayBGM(victoryTracks[Random.Range(0, victoryTracks.Length)]);
        }
        else
        {
            StopMusic();
            // Defeat gets a one-shot sting over the silence.
            if (gm != null && gm.pendingRunOutcome == RunOutcome.Defeat)
                PlayDefeatSfx();
        }
    }

    // ----------------------------------------------------------------
    // Settings API (call from the Settings UI)

    [SerializeField, HideInInspector] private int selectedMenuTrackIndex;

    /// <summary>Number of selectable main-menu tracks.</summary>
    public int MenuTrackCount => menuTracks != null ? menuTracks.Length : 0;

    /// <summary>Index of the currently chosen main-menu track.</summary>
    public int SelectedMenuTrackIndex => selectedMenuTrackIndex;

    /// <summary>Display label for a selectable menu track (falls back to clip name).</summary>
    // Defense note: Retrieves the menu track name value used by this system.
    public string GetMenuTrackName(int index)
    {
        if (menuTrackNames != null && index >= 0 && index < menuTrackNames.Length &&
            !string.IsNullOrEmpty(menuTrackNames[index]))
            return menuTrackNames[index];
        if (menuTracks != null && index >= 0 && index < menuTracks.Length && menuTracks[index] != null)
            return menuTracks[index].name;
        return $"Track {index + 1}";
    }

    /// <summary>
    /// Choose the main-menu track. Persists the choice and, if the menu is the
    /// current context, switches to it immediately.
    /// </summary>
    // Defense note: Updates the menu track state or visual value.
    public void SetMenuTrack(int index)
    {
        if (MenuTrackCount == 0)
            return;

        selectedMenuTrackIndex = ((index % MenuTrackCount) + MenuTrackCount) % MenuTrackCount;
        PlayerPrefs.SetInt(MenuTrackKey, selectedMenuTrackIndex);
        PlayerPrefs.Save();

        AudioClip selected = menuTracks[selectedMenuTrackIndex];
        // Only retarget audio if a menu track is what's currently playing.
        if (_currentClip != null && IsMenuTrack(_currentClip))
            PlayBGM(selected);
    }

    /// <summary>Advance to the next menu track (handy for a single cycle button).</summary>
    // Defense note: Runs the cycle menu track helper used by this script.
    public void CycleMenuTrack() => SetMenuTrack(selectedMenuTrackIndex + 1);

    public float MusicVolume => musicVolume;

    /// <summary>Set music volume (0..1). Persists and applies live.</summary>
    // Defense note: Updates the music volume state or visual value.
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        PlayerPrefs.Save();
        ApplyActiveVolume();
    }

    public bool IsMusicMuted => _muted;

    // Defense note: Updates the music muted state or visual value.
    public void SetMusicMuted(bool muted)
    {
        _muted = muted;
        ApplyActiveVolume();
    }

    // Defense note: Toggles the music muted mode or selection state.
    public void ToggleMusicMuted() => SetMusicMuted(!_muted);

    public float SfxVolume => sfxVolume;

    /// <summary>Set SFX volume (0..1). Persists and applies live to active SFX sources.</summary>
    // Defense note: Updates the sfx volume state or visual value.
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.Save();
        if (_sfxSource != null)
            _sfxSource.volume = sfxVolume;
        if (_keyboardSource != null && _keyboardSource.isPlaying)
            _keyboardSource.volume = sfxVolume;
    }

    // ----------------------------------------------------------------
    // SFX

    /// <summary>Play a one-shot sound effect (button clicks, transitions, etc.) at the current SFX volume.</summary>
    // Defense note: Plays the sfx animation, audio, or feedback.
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || _sfxSource == null)
            return;
        _sfxSource.volume = sfxVolume;
        _sfxSource.PlayOneShot(clip);
    }

    // Minimum unscaled seconds between repeats of the same UiSfx kind. Hover gets a
    // short window so sweeping across a button row doesn't machine-gun; Impact gets a
    // long one so node-entry and the battle transition (fired ~one frame apart for
    // encounter nodes) collapse into a single hit.
    private static readonly float[] UiSfxMinInterval = { 0.07f, 0.05f, 0.05f, 0.05f, 0.12f, 0.50f };
    private readonly float[] _uiSfxLastTime = { -10f, -10f, -10f, -10f, -10f, -10f };

    /// <summary>Play a UI sound by kind, with per-kind anti-spam debounce.</summary>
    // Defense note: Plays the ui sfx animation, audio, or feedback.
    public void PlayUiSfx(UiSfx kind)
    {
        int index = (int)kind;
        if (index < 0 || index >= _uiSfxLastTime.Length)
            return;

        float now = Time.unscaledTime;
        if (now - _uiSfxLastTime[index] < UiSfxMinInterval[index])
            return;

        AudioClip clip = ClipFor(kind);
        if (clip == null)
            return;

        _uiSfxLastTime[index] = now;
        PlaySfx(clip);
    }

    // Defense note: Runs the clip for helper used by this script.
    private AudioClip ClipFor(UiSfx kind)
    {
        switch (kind)
        {
            case UiSfx.Hover: return uiHoverClip;
            case UiSfx.Click: return uiClickClip;
            case UiSfx.ZoomEnable: return uiZoomEnableClip;
            case UiSfx.ZoomDisable: return uiZoomDisableClip;
            case UiSfx.Invalid: return uiInvalidClip;
            case UiSfx.Impact: return uiImpactClip;
            default: return null;
        }
    }

    private float _statusBuffLastTime = -10f;
    private float _statusDebuffLastTime = -10f;
    private const float StatusSfxMinInterval = 0.12f;

    /// <summary>
    /// Play the status-change cue: positive (buff / charge / heal) or negative
    /// (debuff). Debounced per polarity so a multi-hit resolution doesn't stack.
    /// </summary>
    // Defense note: Plays the status sfx animation, audio, or feedback.
    public void PlayStatusSfx(bool positive)
    {
        float now = Time.unscaledTime;
        if (positive)
        {
            if (now - _statusBuffLastTime < StatusSfxMinInterval)
                return;
            _statusBuffLastTime = now;
            PlaySfx(statusBuffClip);
        }
        else
        {
            if (now - _statusDebuffLastTime < StatusSfxMinInterval)
                return;
            _statusDebuffLastTime = now;
            PlaySfx(statusDebuffClip);
        }
    }

    /// <summary>Play the shared defense-skill sound.</summary>
    // Defense note: Plays the defense sfx animation, audio, or feedback.
    public void PlayDefenseSfx() => PlaySfx(defenseClip);

    /// <summary>Play the ASD-counter-success sound.</summary>
    // Defense note: Plays the counter sfx animation, audio, or feedback.
    public void PlayCounterSfx() => PlaySfx(counterClip);

    /// <summary>Play the successful-shop-purchase sound.</summary>
    // Defense note: Plays the purchase sfx animation, audio, or feedback.
    public void PlayPurchaseSfx() => PlaySfx(purchaseClip);

    /// <summary>Play the node-clear reward sound.</summary>
    // Defense note: Plays the reward sfx animation, audio, or feedback.
    public void PlayRewardSfx() => PlaySfx(rewardClip);

    /// <summary>Play the run-defeat sting.</summary>
    // Defense note: Plays the defeat sfx animation, audio, or feedback.
    public void PlayDefeatSfx() => PlaySfx(defeatClip);

    /// <summary>
    /// Play the attack sound for a species/form. formName follows the battle
    /// presentation convention ("Base"/"Evolved"); unknown species are silent.
    /// </summary>
    // Defense note: Plays the attack sfx animation, audio, or feedback.
    public void PlayAttackSfx(string speciesCodeName, string formName)
    {
        if (string.IsNullOrEmpty(speciesCodeName) || speciesAttackSfx == null)
            return;

        for (int i = 0; i < speciesAttackSfx.Length; i++)
        {
            if (!string.Equals(speciesAttackSfx[i].codeName, speciesCodeName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            bool evolved = !string.IsNullOrEmpty(formName) &&
                           !string.Equals(formName, "Base", System.StringComparison.OrdinalIgnoreCase);
            AudioClip clip = evolved && speciesAttackSfx[i].evolvedAttack != null
                ? speciesAttackSfx[i].evolvedAttack
                : speciesAttackSfx[i].baseAttack;
            PlaySfx(clip);
            return;
        }
    }

    /// <summary>
    /// Start or stop the looping keyboard ambience. The MainTerminal controller
    /// calls this from its zoom toggle: play while the typing character is shown
    /// (zoom off), stop when zoom mode hides it.
    /// </summary>
    // Defense note: Updates the keyboard loop active state or visual value.
    public void SetKeyboardLoopActive(bool active)
    {
        if (_keyboardSource == null)
            return;

        if (active)
        {
            if (keyboardTypingClip == null)
                return;
            if (_keyboardSource.clip != keyboardTypingClip)
                _keyboardSource.clip = keyboardTypingClip;
            _keyboardSource.volume = sfxVolume;
            if (!_keyboardSource.isPlaying)
                _keyboardSource.Play();
        }
        else if (_keyboardSource.isPlaying)
        {
            _keyboardSource.Stop();
        }
    }

    // Defense note: Stops the keyboard loop routine or feedback safely.
    public void StopKeyboardLoop() => SetKeyboardLoopActive(false);

    // ----------------------------------------------------------------
    // Playback core

    // Defense note: Plays the menu music animation, audio, or feedback.
    private void PlayMenuMusic()
    {
        if (MenuTrackCount == 0)
        {
            StopMusic();
            return;
        }

        int index = Mathf.Clamp(selectedMenuTrackIndex, 0, MenuTrackCount - 1);
        PlayBGM(menuTracks[index]);
    }

    /// <summary>Crossfade to <paramref name="clip"/>. No-op if it is already playing.</summary>
    // Defense note: Plays the bgm animation, audio, or feedback.
    private void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            StopMusic();
            return;
        }
        if (clip == _currentClip && _active != null && _active.isPlaying)
            return;

        _currentClip = clip;
        StartFade(clip);
    }

    // Defense note: Stops the music routine or feedback safely.
    private void StopMusic()
    {
        _currentClip = null;
        StartFade(null);
    }

    /// <summary>
    /// Fade the music out quickly (default 0.35s). Used by the scene transitions
    /// so the Impact SFX punches into silence instead of fighting the BGM; the
    /// next scene's track comes back in via the normal sceneLoaded crossfade.
    /// </summary>
    // Defense note: Runs the fade out music helper used by this script.
    public void FadeOutMusic(float seconds = 0.35f)
    {
        _currentClip = null;
        StartFade(null, seconds);
    }

    // Defense note: Runs the start fade helper used by this script.
    private void StartFade(AudioClip incomingClip)
    {
        StartFade(incomingClip, crossfadeSeconds);
    }

    // Defense note: Runs the start fade helper used by this script.
    private void StartFade(AudioClip incomingClip, float seconds)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(CrossfadeRoutine(incomingClip, seconds));
    }

    // Defense note: Runs the crossfade routine helper used by this script.
    private IEnumerator CrossfadeRoutine(AudioClip incomingClip, float fadeSeconds)
    {
        AudioSource outgoing = _active;
        AudioSource incoming = _active == _sourceA ? _sourceB : _sourceA;

        float fromOut = outgoing != null ? outgoing.volume : 0f;

        if (incomingClip != null)
        {
            incoming.clip = incomingClip;
            incoming.volume = 0f;
            incoming.Play();
            _active = incoming;
        }

        float duration = Mathf.Max(0.0001f, fadeSeconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // music should keep fading even if the game is paused
            float k = Mathf.Clamp01(t / duration);
            if (outgoing != null)
                outgoing.volume = Mathf.Lerp(fromOut, 0f, k);
            if (incomingClip != null)
                incoming.volume = Mathf.Lerp(0f, TargetVolume(), k); // live: respects volume/mute changes mid-fade
            yield return null;
        }

        if (outgoing != null && outgoing != _active)
        {
            outgoing.Stop();
            outgoing.clip = null;
            outgoing.volume = 0f;
        }
        if (incomingClip != null)
            incoming.volume = TargetVolume();

        _fadeRoutine = null;
    }

    // Defense note: Runs the target volume helper used by this script.
    private float TargetVolume() => _muted ? 0f : musicVolume;

    // Defense note: Applies the active volume change to gameplay or UI state.
    private void ApplyActiveVolume()
    {
        // Only touch the live source while no crossfade is mid-flight; the fade
        // routine reads TargetVolume() itself when one is running.
        if (_fadeRoutine == null && _active != null && _active.isPlaying)
            _active.volume = TargetVolume();
    }

    // Defense note: Returns whether this value is menu track.
    private bool IsMenuTrack(AudioClip clip)
    {
        if (clip == null || menuTracks == null)
            return false;
        for (int i = 0; i < menuTracks.Length; i++)
        {
            if (menuTracks[i] == clip)
                return true;
        }
        return false;
    }
}
