using System.IO;
using Game.Core;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>Generates the title scene. The menu itself is built at runtime by <see cref="TitleMenu"/>.</summary>
    public static class MainMenuBuilder
    {
        [MenuItem("Null Record/Build Main Menu", priority = 2)]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MenuWidgets.Background;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            new GameObject("TitleMenu").AddComponent<TitleMenu>();

            Directory.CreateDirectory(Path.GetDirectoryName(GameFlow.TitleScene));
            EditorSceneManager.SaveScene(scene, GameFlow.TitleScene);
            Debug.Log($"[Null Record] Built title scene at {GameFlow.TitleScene}.");
        }
    }
}
