#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
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
    public static class NullCheckExperiment
    {
        public const string ScenePath = "Assets/App/World/Scenes/NullCheckExperiment.unity";
        public const string ReportDir = "Documents/Reports/2026-10-24-udon-null-check-t209";
        private const string MaterialPath = "Assets/App/World/Materials/NullCheckInput.mat";

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

        [MenuItem("Tools/Null Check Experiment/Compile and Export Assembly")]
        public static void Compile()
        {
            RequireEditMode();
            EnsureProgram("NullCheckTarget"); EnsureProgram("NullCheckBenchmark");
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
            // SDK's Inspector uses this same internal cache to show generated assembly.
            var cacheType = typeof(UdonSharpProgramAsset).Assembly.GetType("UdonSharp.UdonSharpEditorCache");
            var instance = cacheType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/App/World/UdonSharp/NullCheckBenchmark.asset");
            string assembly = (string)cacheType.GetMethod("GetUASMStr").Invoke(instance, new object[] { program });
            if (string.IsNullOrEmpty(assembly) || program.SerializedProgramAsset.RetrieveProgram() == null)
                throw new InvalidOperationException("No compiled Udon program/assembly.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/NullCheckBenchmark.uasm", assembly, new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/compile-evidence.txt", "UTC=" + DateTime.UtcNow.ToString("O")
                + "\nUnity=" + Application.unityVersion + "\nProgram=" + AssetDatabase.GetAssetPath(program)
                + "\nAssemblyChars=" + assembly.Length + "\nSerializedProgram=" + AssetDatabase.GetAssetPath(program.SerializedProgramAsset));
            Debug.Log("[NullCheck] Compiled and exported Udon assembly.");
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

        [MenuItem("Tools/Null Check Experiment/Create Scene")]
        public static void CreateScene()
        {
            RequireEditMode();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene exists; open it instead.");
            Compile();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // SDK scene callbacks may request a path as soon as a descriptor is added.
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Initial scene save failed.");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ExperimentFloor"; floor.transform.position = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(10, 0.2f, 10);
            var world = new GameObject("VRCWorld").AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>();
            var spawn = new GameObject("Spawn").transform; spawn.position = new Vector3(0, 0.1f, -3);
            world.spawns = new Transform[] { spawn };
            if (!AssetDatabase.IsValidFolder("Assets/App/World/Materials"))
                AssetDatabase.CreateFolder("Assets/App/World", "Materials");
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), MaterialPath);
            // No authoring adapter is installed in this checkout. Use official SDK creation,
            // and author persistent C# fields only; do not copy proxy backing-variable caches.
            var valid = new GameObject("ValidUdonInput").AddUdonSharpComponent<NullCheckTarget>();
            var doomed = new GameObject("DestroyUdonInput").AddUdonSharpComponent<NullCheckTarget>();
            var bench = new GameObject("NullCheckBenchmark").AddUdonSharpComponent<NullCheckBenchmark>();
            var settings = new SerializedObject(bench);
            settings.FindProperty(nameof(NullCheckBenchmark.ValidObject)).objectReferenceValue = valid.gameObject;
            settings.FindProperty(nameof(NullCheckBenchmark.ValidBehaviour)).objectReferenceValue = UdonSharpEditorUtility.GetBackingUdonBehaviour(valid);
            settings.FindProperty(nameof(NullCheckBenchmark.AssetReference)).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var destroyInput = GameObject.CreatePrimitive(PrimitiveType.Cube);
            destroyInput.name = "DestroyObjectInput";
            destroyInput.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            settings.FindProperty(nameof(NullCheckBenchmark.DestroyObject)).objectReferenceValue = destroyInput;
            settings.FindProperty(nameof(NullCheckBenchmark.DestroyBehaviour)).objectReferenceValue = UdonSharpEditorUtility.GetBackingUdonBehaviour(doomed);
            settings.ApplyModifiedProperties();
            if (bench.ValidBehaviour == null || bench.DestroyBehaviour == null || bench.AssetReference == null)
                throw new InvalidOperationException("Persistent input wiring failed.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[NullCheck] Created dedicated minimal scene.");
        }

        private static UdonBehaviour Live()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Run the null check scene in Play Mode.");
            return GameObject.Find("NullCheckBenchmark").GetComponent<UdonBehaviour>();
        }

        [MenuItem("Tools/Null Check Experiment/Live Status")]
        public static void Status()
        {
            var udon = Live();
            File.WriteAllText("Library/T209-live-state.txt", "UTC=" + DateTime.UtcNow.ToString("O")
                + "\nFrame=" + Time.frameCount + "\nStatus=" + udon.GetProgramVariable("Status")
                + "\nRows=" + udon.GetProgramVariable("Rows") + "\nFailures=" + udon.GetProgramVariable("Failures")
                + "\nComplete=" + udon.GetProgramVariable("Complete"));
        }

        [MenuItem("Tools/Null Check Experiment/Export Editor Result")]
        public static void Export()
        {
            var udon = Live();
            if (!(bool)udon.GetProgramVariable("Complete") || (int)udon.GetProgramVariable("Failures") != 0)
                throw new InvalidOperationException("Require completed run with zero failures.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/raw-editor.csv", (string)udon.GetProgramVariable("LastCsv"), new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/semantics-editor.csv", (string)udon.GetProgramVariable("SemanticsCsv"), new UTF8Encoding(false));
            var evidence = new StringBuilder("Read from live VRC.Udon.UdonBehaviour heap; no proxy method timing.\n");
            evidence.AppendLine("UTC=" + DateTime.UtcNow.ToString("O"));
            evidence.AppendLine("Project=" + Application.dataPath);
            evidence.AppendLine("Unity=" + Application.unityVersion + "\nCPU=" + SystemInfo.processorType
                + "\nGPU=" + SystemInfo.graphicsDeviceName + "\nOS=" + SystemInfo.operatingSystem
                + "\nVSync=" + QualitySettings.vSyncCount + "\nTargetFPS=" + Application.targetFrameRate
                + "\nResolution=" + Screen.width + "x" + Screen.height
                + "\nProfiler=" + UnityEngine.Profiling.Profiler.enabled
                + "\nDeepProfile=" + UnityEditorInternal.ProfilerDriver.deepProfiling
                + "\nFrame=" + Time.frameCount + "\nProgram=" + udon.programSource.name
                + "\nRows=" + udon.GetProgramVariable("Rows") + "\nFailures=" + udon.GetProgramVariable("Failures"));
            foreach (string field in new[] { "DestroyedGameObject", "DestroyedTransform", "DestroyedMaterial", "DestroyedUdon" })
            {
                object value = udon.GetProgramVariable(field);
                evidence.AppendLine(field + ":CLRNull=" + ReferenceEquals(value, null)
                    + ",UnityNull=" + ((UnityEngine.Object)value == null));
            }
            File.WriteAllText(ReportDir + "/editor-evidence.txt", evidence.ToString(), new UTF8Encoding(false));
            Debug.Log("[NullCheck] Exported CSV and destroyed-wrapper evidence.");
        }

        [MenuItem("Tools/Null Check Experiment/Export Runtime Metadata")]
        public static void ExportRuntimeMetadata()
        {
            var udon = Live();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object vm = typeof(UdonBehaviour).GetField("_udonVM", flags).GetValue(udon);
            object program = typeof(UdonBehaviour).GetField("_program", flags).GetValue(udon);
            if (vm == null || program == null) throw new InvalidOperationException("Runtime Udon VM is not initialized.");
            object heap = program.GetType().GetProperty("Heap").GetValue(program);
            var text = new StringBuilder();
            text.AppendLine("UTC=" + DateTime.UtcNow.ToString("O") + "\nFrame=" + Time.frameCount);
            text.AppendLine("VM=" + vm.GetType().AssemblyQualifiedName);
            text.AppendLine("Heap=" + heap.GetType().AssemblyQualifiedName);
            text.AppendLine("Program=" + program.GetType().AssemblyQualifiedName);
            text.AppendLine("UdonDebugLogging=" + VRC.Udon.UdonManager.Instance.DebugLogging);
            text.AppendLine("UdonSharpRuntimeAssembly=" + typeof(UdonSharpBehaviour).Assembly.FullName);
            text.AppendLine("UdonSharpCompilerAssembly=" + typeof(UdonSharpProgramAsset).Assembly.FullName);
            foreach (var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                if (component.GetType().FullName.StartsWith("VRC.SDK3.ClientSim.", StringComparison.Ordinal))
                    text.AppendLine("ClientSimComponent=" + component.GetType().FullName);
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/runtime-metadata.txt", text.ToString(), new UTF8Encoding(false));
        }
    }
}
#endif
