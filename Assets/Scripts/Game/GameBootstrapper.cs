using UnityEngine;

namespace CodeEscape.Game
{
    /// <summary>
    /// Creates the persistent GameSystems object (GameManager, LanguageManager, SettingsManager,
    /// AudioManager, SceneTransitionManager) before the first scene loads, so every scene -
    /// including one started directly in the Editor - has the global managers available.
    /// </summary>
    public static class GameBootstrapper
    {
        /// <summary>Resources path of Assets/Prefabs/Resources/GameSystems.prefab.</summary>
        public const string SystemsPrefabResourcePath = "GameSystems";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateGameSystems()
        {
            if (GameManager.Instance != null)
                return;

            GameObject prefab = Resources.Load<GameObject>(SystemsPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[GameBootstrapper] Resources/{SystemsPrefabResourcePath}.prefab not found. Global managers were not created.");
                return;
            }

            GameObject systems = Object.Instantiate(prefab);
            systems.name = prefab.name;
            Object.DontDestroyOnLoad(systems);
        }
    }
}
