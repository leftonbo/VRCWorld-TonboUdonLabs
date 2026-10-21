#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using App;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;

namespace App.AppEditor
{
    public static class InterUdonDataExperiment
    {
        public const string ScenePath = "Assets/App/World/Scenes/InterUdonDataExperiment.unity";
        private const string ReportDir = "Documents/Reports/2026-10-21-udon-interdata-t174";

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for Edit Mode and compilation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Preserve unsaved scenes before continuing.");
        }

        [MenuItem("Tools/Inter Udon Data/Compile")]
        public static void Compile()
        {
            RequireEditMode();
            EnsureProgram("InterUdonDataTarget"); EnsureProgram("InterUdonDataBenchmark");
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
        }

        private static void EnsureProgram(string name)
        {
            string path = "Assets/App/World/UdonSharp/" + name;
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path + ".asset") != null) return;
            var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path + ".cs");
            program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            AssetDatabase.CreateAsset(program, path + ".asset");
        }

        [MenuItem("Tools/Inter Udon Data/Create Scene")]
        public static void CreateScene()
        {
            RequireEditMode();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene exists; open it instead.");
            Compile();
            if (!AssetDatabase.CopyAsset(SharedDataExperiment.ScenePath, ScenePath))
                throw new IOException("Scene copy failed.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var old in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
                    old.gameObject.SetActive(false);
            // This checkout has no TonboWorkshop authoring adapter. Use official SDK creation,
            // then author only the persistent proxy's declared field, without backing-cache copies.
            var target = new GameObject("InterUdonDataTarget").AddUdonSharpComponent<InterUdonDataTarget>();
            var bench = new GameObject("InterUdonDataBenchmark").AddUdonSharpComponent<InterUdonDataBenchmark>();
            var settings = new SerializedObject(bench);
            settings.FindProperty(nameof(InterUdonDataBenchmark.Target)).objectReferenceValue = target;
            settings.ApplyModifiedProperties();
            if (bench.Target != target || target.gameObject == bench.gameObject)
                throw new InvalidOperationException("Persistent two-behaviour wiring failed.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[InterUdon] Created dedicated scene, two separate GameObjects, sync None.");
        }

        private static UdonBehaviour Live()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Run the InterUdon scene in Play Mode.");
            var go = GameObject.Find("InterUdonDataBenchmark");
            if (go == null) throw new InvalidOperationException("Benchmark not found.");
            var udon = go.GetComponent<UdonBehaviour>();
            if (udon == null) throw new InvalidOperationException("Backing UdonBehaviour not found.");
            return udon;
        }

        [MenuItem("Tools/Inter Udon Data/Live Status")]
        public static void Status()
        {
            var udon = Live();
            string state = "Status=" + udon.GetProgramVariable("Status") + "\nRows=" + udon.GetProgramVariable("Rows")
                + "\nFailures=" + udon.GetProgramVariable("Failures") + "\nAliasPassed=" + udon.GetProgramVariable("AliasPassed")
                + "\nComplete=" + udon.GetProgramVariable("Complete");
            File.WriteAllText("Library/T174-live-state.txt", state);
            Debug.Log("[InterUdon] " + state);
        }

        [MenuItem("Tools/Inter Udon Data/Export Editor Result")]
        public static void Export()
        {
            var udon = Live();
            if (!(bool)udon.GetProgramVariable("Complete") || !(bool)udon.GetProgramVariable("AliasPassed")
                || (int)udon.GetProgramVariable("Failures") != 0)
                throw new InvalidOperationException("Benchmark must complete with alias and checksum success.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/raw-editor.csv", (string)udon.GetProgramVariable("LastCsv"), new UTF8Encoding(false));
            var peer = GameObject.Find("InterUdonDataTarget").GetComponent<UdonBehaviour>();
            File.WriteAllText(ReportDir + "/editor-evidence.txt",
                "Retrieved via live VRC.Udon.UdonBehaviour.GetProgramVariable, not proxy fields.\n"
                + "UTC=" + DateTime.UtcNow.ToString("O") + "\nUnity=" + Application.unityVersion
                + "\nCPU=" + SystemInfo.processorType + "\nGPU=" + SystemInfo.graphicsDeviceName
                + "\nOS=" + SystemInfo.operatingSystem + "\nVSync=" + QualitySettings.vSyncCount
                + "\nTargetFPS=" + Application.targetFrameRate + "\nResolution=" + Screen.width + "x" + Screen.height
                + "\nProfiler=" + UnityEngine.Profiling.Profiler.enabled
                + "\nDeepProfile=" + UnityEditorInternal.ProfilerDriver.deepProfiling
                + "\nDistinctUdonBehaviours=" + (peer != udon)
                + "\nPeerProgram=" + peer.programSource.name + "\nRunnerProgram=" + udon.programSource.name
                + "\nAliasPassed=" + udon.GetProgramVariable("AliasPassed")
                + "\nComplete=" + udon.GetProgramVariable("Complete") + "\nRows=" + udon.GetProgramVariable("Rows")
                + "\nFailures=" + udon.GetProgramVariable("Failures") + "\nStatus=" + udon.GetProgramVariable("Status"));
            Debug.Log("[InterUdon] Exported live Udon CSV and environment evidence.");
        }
    }
}
#endif
