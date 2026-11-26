using Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>Scene-level flow: title → game, continue, new game, quit.</summary>
    public static class GameFlow
    {
        public const string TitleScene = "Assets/_Project/Scenes/MainMenu.unity";
        public const string GameScene = "Assets/_Project/Scenes/Greybox_Intake.unity";

        /// <summary>Loads the saved scene; the game scene's SaveManager restores the rest.</summary>
        public static void Continue()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SaveData save = SaveSystem.Read();
            SceneManager.LoadScene(save != null && !string.IsNullOrEmpty(save.scene) ? save.scene : GameScene);
        }

        /// <summary>Discards the save and starts from the beginning.</summary>
        public static void NewGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SaveSystem.Delete();
            SceneManager.LoadScene(GameScene);
        }

        public static void QuitToTitle()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(TitleScene);
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>Player preferences (not part of the save). Stored in PlayerPrefs.</summary>
    public static class GameSettings
    {
        private const string VolumeKey = "settings.masterVolume";
        private const string FullscreenKey = "settings.fullscreen";
        private const string DebugHudKey = "settings.debugHud";
        private const string EffectsKey = "settings.effectsVolume";
        private const string AmbienceKey = "settings.ambienceVolume";

        public static float EffectsVolume
        {
            get => PlayerPrefs.GetFloat(EffectsKey, 1f);
            set => PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value));
        }

        /// <summary>Room ambience and the combat loop.</summary>
        public static float AmbienceVolume
        {
            get => PlayerPrefs.GetFloat(AmbienceKey, 0.8f);
            set => PlayerPrefs.SetFloat(AmbienceKey, Mathf.Clamp01(value));
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                Apply();
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                Apply();
            }
        }

        public static bool DebugHud
        {
            get => PlayerPrefs.GetInt(DebugHudKey, Application.isEditor ? 1 : 0) == 1;
            set => PlayerPrefs.SetInt(DebugHudKey, value ? 1 : 0);
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (!Application.isEditor) Screen.fullScreen = Fullscreen;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
