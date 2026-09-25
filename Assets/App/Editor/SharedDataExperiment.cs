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
using VRC.Core;
using VRC.Editor;
using VRC.SDKBase.Editor;

namespace App.AppEditor
{
    public static class SharedDataExperiment
    {
        public const string ScenePath = "Assets/App/World/Scenes/SharedDataExperiment.unity";
        private const string ReportDir = "Documents/Reports/2026-09-25-udon-shared-data-keys";

        [MenuItem("Tools/Shared Data Experiment/Create Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes before continuing.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Experiment scene already exists; open it instead.");
            EnsureProgram("SharedDataTarget"); EnsureProgram("SharedDataBenchmark");
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
            if (!AssetDatabase.CopyAsset("Assets/App/World/Scenes/World-TonboUdonLabs.unity", ScenePath))
                throw new IOException("Scene copy failed.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject oldLab = GameObject.Find("UdonPerformanceLab");
            if (oldLab != null) oldLab.SetActive(false);
            var target = new GameObject("SharedDataTarget").AddUdonSharpComponent<SharedDataTarget>();
            var bench = new GameObject("SharedDataBenchmark").AddUdonSharpComponent<SharedDataBenchmark>();
            bench.Target = target;
            UdonSharpEditorUtility.CopyProxyToUdon(target);
            UdonSharpEditorUtility.CopyProxyToUdon(bench);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SharedData] Scene created; original scene untouched.");
        }

        private static void EnsureProgram(string name)
        {
            string path = "Assets/App/World/UdonSharp/" + name;
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path + ".asset") != null) return;
            var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path + ".cs");
            AssetDatabase.CreateAsset(program, path + ".asset");
        }

        [MenuItem("Tools/Shared Data Experiment/Finish Scene Setup")]
        public static void FinishSceneSetup()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open experiment scene in Edit Mode.");
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
            var bench = GameObject.Find("SharedDataBenchmark").GetComponent<SharedDataBenchmark>();
            var target = GameObject.Find("SharedDataTarget").GetComponent<SharedDataTarget>();
            bench.Target = target;
            UdonSharpEditorUtility.CopyProxyToUdon(target);
            UdonSharpEditorUtility.CopyProxyToUdon(bench);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Shared Data Experiment/Export Editor Result")]
        public static void Export()
        {
            var go = GameObject.Find("SharedDataBenchmark");
            if (go == null || !EditorApplication.isPlaying) throw new InvalidOperationException("Run experiment in Play Mode first.");
            var udon = go.GetComponent<UdonBehaviour>();
            if (udon == null || !(bool)udon.GetProgramVariable("Complete"))
                throw new InvalidOperationException("Benchmark not complete.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/raw-editor.csv", (string)udon.GetProgramVariable("LastCsv"), new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/editor-evidence.txt",
                "Retrieved from live VRC.Udon.UdonBehaviour.GetProgramVariable, not proxy fields.\n"
                + "UTC=" + DateTime.UtcNow.ToString("O") + "\nUnity=" + Application.unityVersion
                + "\nCPU=" + SystemInfo.processorType + "\nGPU=" + SystemInfo.graphicsDeviceName
                + "\nOS=" + SystemInfo.operatingSystem + "\nVSync=" + QualitySettings.vSyncCount
                + "\nTargetFPS=" + Application.targetFrameRate + "\nResolution=" + Screen.width + "x" + Screen.height
                + "\nProfiler=" + UnityEngine.Profiling.Profiler.enabled
                + "\nStatus=" + udon.GetProgramVariable("Status") + "\nFailures=" + udon.GetProgramVariable("Failures"));
            Debug.Log("[SharedData] Exported live Udon result.");
        }

        [MenuItem("Tools/Shared Data Experiment/Build And Test")]
        public static async void BuildAndTest()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the experiment scene in Edit Mode first.");
            int previousClients = VRCSettings.NumClients;
            try
            {
                if (!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var builder))
                    throw new InvalidOperationException("Open the VRChat SDK control panel Builder tab first.");
                VRCSettings.NumClients = 1;
                await builder.BuildAndTest();
                Debug.Log("[SharedData] BuildAndTest launched; client results still required.");
            }
            catch (Exception error) { Debug.LogException(error); }
            finally { VRCSettings.NumClients = previousClients; }
        }
    }
}
#endif
