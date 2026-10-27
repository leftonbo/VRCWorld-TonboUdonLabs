#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using App;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;

namespace App.AppEditor
{
    public static class StringBuildExperiment
    {
        public const string ScenePath = "Assets/App/World/Scenes/StringBuildExperiment.unity";
        public const string ReportDir = "Documents/Reports/2026-10-27-udon-string-build-t228";

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for Edit Mode and compilation.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Leave Prefab Mode before scene operations.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Preserve unsaved scenes before continuing.");
        }

        [MenuItem("Tools/String Build Experiment/Compile and Export Assembly")]
        public static void Compile()
        {
            RequireEditMode();
            const string path = "Assets/App/World/UdonSharp/StringBuildBenchmark";
            var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path + ".asset");
            if (program == null)
            {
                program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path + ".cs");
                program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                AssetDatabase.CreateAsset(program, path + ".asset");
            }
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            AssetDatabase.SaveAssets();
            var cacheType = typeof(UdonSharpProgramAsset).Assembly.GetType("UdonSharp.UdonSharpEditorCache");
            var instance = cacheType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            string assembly = (string)cacheType.GetMethod("GetUASMStr").Invoke(instance, new object[] { program });
            if (string.IsNullOrEmpty(assembly) || program.SerializedProgramAsset.RetrieveProgram() == null)
                throw new InvalidOperationException("No compiled Udon program/assembly.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/StringBuildBenchmark.uasm", assembly, new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/compile-evidence.txt", "UTC=" + DateTime.UtcNow.ToString("O")
                + "\nUnity=" + Application.unityVersion + "\nProgram=" + AssetDatabase.GetAssetPath(program)
                + "\nAssemblyChars=" + assembly.Length + "\nSerializedProgram=" + AssetDatabase.GetAssetPath(program.SerializedProgramAsset));
            Debug.Log("[StringBuild] Compiled and exported Udon assembly.");
        }

        [MenuItem("Tools/String Build Experiment/Create Scene")]
        public static void CreateScene()
        {
            RequireEditMode();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene exists; open it instead.");
            Compile();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Initial scene save failed.");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ExperimentFloor"; floor.transform.position = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(10, 0.2f, 10);
            var world = new GameObject("VRCWorld").AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>();
            world.gameObject.AddComponent<VRC.Core.PipelineManager>();
            var spawn = new GameObject("Spawn").transform; spawn.position = new Vector3(0, 0.1f, -3);
            world.spawns = new Transform[] { spawn };
            // No guarded authoring adapter is installed here. Follow the existing experiments:
            // SDK creation, persistent C# settings, no redundant backing-variable copy.
            new GameObject("StringBuildBenchmark").AddUdonSharpComponent<StringBuildBenchmark>();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            AssetDatabase.SaveAssets();
            Debug.Log("[StringBuild] Created dedicated scene with one local sync-None benchmark.");
        }

        private static UdonBehaviour Live()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Run the string build scene in Play Mode.");
            return GameObject.Find("StringBuildBenchmark").GetComponent<UdonBehaviour>();
        }

        [MenuItem("Tools/String Build Experiment/Check Client Test Availability")]
        public static void CheckClientTest()
        {
            RequireEditMode();
            bool available = VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var builder);
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/client-test-evidence.txt", "UTC=" + DateTime.UtcNow.ToString("O")
                + "\nSDKBuilderAvailable=" + available + "\nSDKLoggedIn=" + VRC.Core.APIUser.IsLoggedIn);
        }

        [MenuItem("Tools/String Build Experiment/Prepare Client Scene")]
        public static void PrepareClientScene()
        {
            RequireEditMode();
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the string build scene first.");
            var descriptor = UnityEngine.Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>();
            if (descriptor == null) throw new InvalidOperationException("Missing experiment descriptor.");
            if (descriptor.GetComponent<VRC.Core.PipelineManager>() == null)
                Undo.AddComponent<VRC.Core.PipelineManager>(descriptor.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/String Build Experiment/Build and Test")]
        public static void BuildAndTest()
        {
            RequireEditMode();
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the string build scene first.");
            // Release the REST/menu execution context before the SDK awaits Editor ticks.
            File.AppendAllText(ReportDir + "/client-test-evidence.txt", "\nScheduledBuildUTC=" + DateTime.UtcNow.ToString("O"));
            EditorApplication.update -= StartClientBuild;
            EditorApplication.update += StartClientBuild;
        }

        private static void StartClientBuild()
        {
            EditorApplication.update -= StartClientBuild;
            RunClientBuild();
        }

        private static async void RunClientBuild()
        {
            RequireEditMode();
            File.AppendAllText(ReportDir + "/client-test-evidence.txt", "\nStartedBuildUTC=" + DateTime.UtcNow.ToString("O"));
            if (!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var builder))
                throw new InvalidOperationException("Open the VRChat SDK panel and select the world builder.");
            try
            {
                await builder.BuildAndTest();
                File.AppendAllText(ReportDir + "/client-test-evidence.txt", "\nBuildAndTest=completed UTC=" + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception error)
            {
                File.AppendAllText(ReportDir + "/client-test-evidence.txt", "\nBuildAndTest=" + error.GetType().Name + ": " + error.Message);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/String Build Experiment/Live Status")]
        public static void Status()
        {
            var udon = Live();
            Directory.CreateDirectory("Library/T228");
            File.WriteAllText("Library/T228/live-state.txt", "UTC=" + DateTime.UtcNow.ToString("O")
                + "\nFrame=" + Time.frameCount + "\nStatus=" + udon.GetProgramVariable("Status")
                + "\nRows=" + udon.GetProgramVariable("Rows") + "\nFailures=" + udon.GetProgramVariable("Failures")
                + "\nComplete=" + udon.GetProgramVariable("Complete"));
        }

        [MenuItem("Tools/String Build Experiment/Export Live Results")]
        public static void Export()
        {
            var udon = Live();
            if (!(bool)udon.GetProgramVariable("Complete")) throw new InvalidOperationException("Wait for completion.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/raw-editor.csv", (string)udon.GetProgramVariable("LastCsv"), new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/semantics-editor.csv", (string)udon.GetProgramVariable("SemanticsCsv"), new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/sample-output.txt", (string)udon.GetProgramVariable("LastOutput"), new UTF8Encoding(false));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object vm = typeof(UdonBehaviour).GetField("_udonVM", flags).GetValue(udon);
            object program = typeof(UdonBehaviour).GetField("_program", flags).GetValue(udon);
            if (vm == null || program == null) throw new InvalidOperationException("Runtime Udon VM is not initialized.");
            var text = new StringBuilder();
            text.AppendLine("UTC=" + DateTime.UtcNow.ToString("O") + "\nFrame=" + Time.frameCount);
            text.AppendLine("Rows=" + udon.GetProgramVariable("Rows") + "\nFailures=" + udon.GetProgramVariable("Failures"));
            text.AppendLine("VM=" + vm.GetType().AssemblyQualifiedName);
            text.AppendLine("Program=" + program.GetType().AssemblyQualifiedName);
            text.AppendLine("UdonDebugLogging=" + UdonManager.Instance.DebugLogging);
            text.AppendLine("Unity=" + Application.unityVersion);
            text.AppendLine("UdonSharpRuntimeAssembly=" + typeof(UdonSharpBehaviour).Assembly.FullName);
            text.AppendLine("UdonSharpCompilerAssembly=" + typeof(UdonSharpProgramAsset).Assembly.FullName);
            text.AppendLine("OS=" + SystemInfo.operatingSystem + "\nCPU=" + SystemInfo.processorType + "\nGPU=" + SystemInfo.graphicsDeviceName);
            text.AppendLine("Resolution=" + Screen.width + "x" + Screen.height + "\nTargetFPS=" + Application.targetFrameRate + "\nVSync=" + QualitySettings.vSyncCount);
            text.AppendLine("Profiler=" + UnityEngine.Profiling.Profiler.enabled);
            foreach (var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                if (component.GetType().FullName.StartsWith("VRC.SDK3.ClientSim.", StringComparison.Ordinal))
                    text.AppendLine("ClientSimComponent=" + component.GetType().FullName);
            File.WriteAllText(ReportDir + "/runtime-metadata.txt", text.ToString(), new UTF8Encoding(false));
            Debug.Log("[StringBuild] Exported Udon VM results and runtime metadata.");
        }
    }
}
#endif
