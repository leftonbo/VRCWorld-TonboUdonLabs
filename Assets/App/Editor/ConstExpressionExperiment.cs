#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor;

namespace App.AppEditor
{
    /// <summary>生成されたプログラムをEditorバインディングのUdon VMで実行し、命令と値を保存します。</summary>
    public static class ConstExpressionExperiment
    {
        private const string ProgramPath = "Assets/App/World/UdonSharp/ConstExpressionProbe.asset";
        private const string ReportDir = "Documents/Reports/2026-11-06-udon-const-t296";

        [MenuItem("Tools/Const Expression Experiment/Compile and Run VM")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for idle Edit mode.");
            Directory.CreateDirectory(ReportDir);
            var asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                asset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(Path.ChangeExtension(ProgramPath, ".cs"));
                asset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                AssetDatabase.CreateAsset(asset, ProgramPath);
            }
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            var cacheType = typeof(UdonSharpProgramAsset).Assembly.GetType("UdonSharp.UdonSharpEditorCache");
            var cache = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string assembly = (string)cacheType.GetMethod("GetUASMStr").Invoke(cache, new object[] { asset });
            var program = asset.SerializedProgramAsset.RetrieveProgram();
            if (program == null || string.IsNullOrEmpty(assembly))
                throw new InvalidOperationException("Missing compiled program or assembly.");
            File.WriteAllText(ReportDir + "/ConstExpressionProbe.uasm", assembly, new UTF8Encoding(false));
            var initialHeap = new StringBuilder();
            foreach (string symbol in program.SymbolTable.GetSymbols())
            {
                uint address = program.SymbolTable.GetAddressFromSymbol(symbol);
                initialHeap.AppendLine($"heap[{address}] {symbol} = {program.Heap.GetHeapVariable(address)}");
            }
            File.WriteAllText(ReportDir + "/initial-heap.txt", initialHeap.ToString(), new UTF8Encoding(false));
            var vm = UdonEditorManager.Instance.ConstructUdonVM();
            vm.LoadProgram(program);
            string[] cases = { "ConstA", "ConstB", "Literal", "Parenthesized", "DivisionConst", "DivisionLiteral", "NestedConst", "LocalConst" };
            int[] inputs = { -17, -1, 0, 1, 2, 7, 12345 };
            var csv = new StringBuilder("case,input,expected,actual,vm_return,pass\n");
            uint inputAddress = program.SymbolTable.GetAddressFromSymbol("Input");
            uint outputAddress = program.SymbolTable.GetAddressFromSymbol("Result");
            int failures = 0;
            // Inputs are injected after LoadProgram, so the compiler cannot fold the outer multiplication.
            foreach (int input in inputs)
            foreach (string name in cases)
            {
                program.Heap.SetHeapVariable(inputAddress, input);
                program.Heap.SetHeapVariable(outputAddress, int.MinValue);
                vm.SetProgramCounter(program.EntryPoints.GetAddressFromSymbol(name));
                uint code = vm.Interpret();
                int actual = program.Heap.GetHeapVariable<int>(outputAddress);
                int expected = input * (name.StartsWith("Division", StringComparison.Ordinal) ? 6 : name == "NestedConst" ? 9 : 5);
                bool pass = code == 0 && actual == expected;
                if (!pass) failures++;
                csv.AppendLine($"{name},{input},{expected},{actual},{code},{pass}");
            }
            File.WriteAllText(ReportDir + "/raw-vm.csv", csv.ToString(), new UTF8Encoding(false));
            var evidence = new StringBuilder();
            evidence.AppendLine("UTC=" + DateTime.UtcNow.ToString("O"));
            evidence.AppendLine("Unity=" + Application.unityVersion);
            evidence.AppendLine("Execution=SDK UdonEditorManager.ConstructUdonVM / LoadProgram / Interpret (Edit mode)");
            evidence.AppendLine("VM=" + vm.GetType().AssemblyQualifiedName);
            evidence.AppendLine("UdonSharpAssembly=" + typeof(UdonSharpProgramAsset).Assembly.FullName);
            evidence.AppendLine("Program=" + ProgramPath);
            evidence.AppendLine("SerializedProgram=" + AssetDatabase.GetAssetPath(asset.SerializedProgramAsset));
            evidence.AppendLine("Rows=" + inputs.Length * cases.Length + " Failures=" + failures);
            foreach (string symbol in program.SymbolTable.GetSymbols())
            {
                uint address = program.SymbolTable.GetAddressFromSymbol(symbol);
                evidence.AppendLine($"heap[{address}] {symbol} = {program.Heap.GetHeapVariable(address)}");
            }
            File.WriteAllText(ReportDir + "/vm-evidence.txt", evidence.ToString(), new UTF8Encoding(false));
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            if (failures != 0) throw new InvalidOperationException("VM cases failed: " + failures);
            Debug.Log("[ConstExpression] VM verified " + inputs.Length * cases.Length + " cases; failures=0.");
        }
    }
}
#endif
