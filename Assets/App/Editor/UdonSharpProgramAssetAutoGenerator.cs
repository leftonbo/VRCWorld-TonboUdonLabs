#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace App.AppEditor
{
    /// <summary>
    /// Keeps directly-created UdonSharp source files paired with ProgramAssets.
    /// </summary>
    public class UdonSharpProgramAssetAutoGenerator : AssetPostprocessor
    {
        [MenuItem("Tools/Udon Performance/Generate Program Assets")]
        private static void GenerateLabProgramAssets()
        {
            CreateProgramAsset("Assets/App/World/UdonSharp/UdonPerformanceBenchmark.cs");
            CreateProgramAsset("Assets/App/World/UdonSharp/UdonPerformanceExternalTarget.cs");
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Udon Performance/Export Live CSV")]
        private static void ExportLiveBenchmarkCsv()
        {
            GameObject lab = GameObject.Find("UdonPerformanceLab");
            if (lab == null)
            {
                Debug.LogError("[UdonPerformance] UdonPerformanceLab was not found.");
                return;
            }

            foreach (UdonBehaviour behaviour in lab.GetComponents<UdonBehaviour>())
            {
                UdonSharpProgramAsset source = behaviour.programSource as UdonSharpProgramAsset;
                if (source == null || source.sourceCsScript == null || source.sourceCsScript.name != "UdonPerformanceBenchmark")
                    continue;

                string csv = behaviour.GetProgramVariable<string>("LastCsv");
                if (string.IsNullOrEmpty(csv))
                {
                    Debug.LogError("[UdonPerformance] LastCsv is empty; run the benchmark first.");
                    return;
                }

                string reportDirectory = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    "Documents",
                    "Reports",
                    "2026-09-25-udon-performance-member-cost"));
                Directory.CreateDirectory(reportDirectory);
                string outputPath = Path.Combine(reportDirectory, "raw-editor.csv");
                File.WriteAllText(outputPath, csv, new UTF8Encoding(false));
                Debug.Log("[UdonPerformance] Exported CSV: " + outputPath);
                return;
            }

            Debug.LogError("[UdonPerformance] Benchmark UdonBehaviour was not found.");
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (!didDomainReload)
                return;

            bool createdAny = false;
            foreach (string importedAssetPath in importedAssets)
            {
                if (!importedAssetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                createdAny |= CreateProgramAsset(importedAssetPath);
            }

            if (createdAny)
                AssetDatabase.Refresh();
        }

        private static bool CreateProgramAsset(string scriptPath)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            Type scriptClass = script == null ? null : script.GetClass();
            if (scriptClass == null
                || scriptClass.IsAbstract
                || !typeof(UdonSharpBehaviour).IsAssignableFrom(scriptClass)
                || UdonSharpEditorUtility.GetUdonSharpProgramAsset(scriptClass) != null)
                return false;

            string programAssetPath = Path.ChangeExtension(scriptPath, ".asset")?.Replace('\\', '/');
            if (string.IsNullOrEmpty(programAssetPath)
                || !programAssetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || AssetDatabase.LoadMainAssetAtPath(programAssetPath) != null)
                return false;

            UdonSharpProgramAsset programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            programAsset.sourceCsScript = script;
            programAsset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            AssetDatabase.CreateAsset(programAsset, programAssetPath);
            AssetDatabase.ImportAsset(programAssetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[UdonSharp AutoGenerator] Created ProgramAsset: " + programAssetPath);
            return true;
        }
    }
}
#endif
