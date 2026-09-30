#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using App;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace App.AppEditor
{
    public static class PersistenceExperiment
    {
        public const string ScenePath = "Assets/App/World/Scenes/PlayerObjectPersistenceExperiment.unity";
        public const string ReportDir = "Documents/Reports/2026-09-30-playerobject-persistence";
        private const string SourceDir = "Assets/App/World/UdonSharp/";
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/App/ArtResources/Fonts/KosugiMaru-Regular.ttf");
        private static readonly Color PanelColor = new Color(0.055f, 0.08f, 0.12f);

        [MenuItem("Tools/PlayerObject Persistence/Create Scene")]
        public static void CreateScene()
        {
            GuardEditMode();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene exists. Open it; do not rebuild persistence identities.");
            Compile();
            if (!AssetDatabase.CopyAsset("Assets/App/World/Scenes/World-TonboUdonLabs.unity", ScenePath))
                throw new IOException("Scene copy failed.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var oldLab = GameObject.Find("UdonPerformanceLab");
            if (oldLab != null) UnityEngine.Object.DestroyImmediate(oldLab);
            var descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            descriptor.GetComponent<PipelineManager>().blueprintId = "";
            var lab = new GameObject("PersistenceLab").AddUdonSharpComponent<PersistenceLab>();
            CreateProbe<PersistenceScalars>(lab, "GrowingScalars", 0, ScalarCount, false);
            CreateProbe<PersistenceFixed27>(lab, "Fixed27Scalars", 1, 27, false);
            CreateProbe<PersistenceArray27>(lab, "Array27", 2, 27, true);
            CreateUI(lab);
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.9f, -1.8f), Quaternion.identity);
            camera.fieldOfView = 65;
            var spawn = new GameObject("PersistenceSpawn").transform;
            spawn.position = new Vector3(0, 0.1f, -0.8f);
            descriptor.spawns = new[] { spawn };
            descriptor.RespawnHeightY = -100;
            descriptor.NetworkIDCollection.Clear(); // New unpublished scene only; never do this on schema updates.
            VRC.SDKBase.Network.NetworkIDAssignment.ConfigureNetworkIDs(descriptor,
                out List<VRC.SDKBase.Network.NetworkIDAssignment.SetErrorLocation> errors,
                VRC.SDKBase.Network.NetworkIDAssignment.SetError.IncompatibleTypes);
            Require(errors.Count == 0, "Network ID assignment failed.");
            EditorSceneManager.SaveScene(scene);
            FinishSetup();
            Selection.activeGameObject = lab.gameObject;
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0, 1.8f, 3.6f), new Vector3(5, 3, 0.1f)), false);
            Debug.Log("[PersistenceExperiment] Created dedicated scene. Blueprint is empty for a new private world.");
        }

        private static void CreateProbe<T>(PersistenceLab lab, string name, int index, int count, bool array) where T : PersistenceProbe
        {
            var go = new GameObject(name);
            go.AddComponent<VRCPlayerObject>();
            go.AddComponent<VRCEnablePersistence>();
            var probe = go.AddUdonSharpComponent<T>();
            probe.Lab = lab;
            probe.CaseIndex = index;
            probe.IntCount = count;
            probe.ArrayMode = array;
            UdonSharpEditorUtility.CopyProxyToUdon(probe);
        }

        [MenuItem("Tools/PlayerObject Persistence/Compile and Finish Setup")]
        public static void FinishSetup()
        {
            GuardEditMode();
            Require(SceneManager.GetActiveScene().path == ScenePath, "Open the persistence experiment scene.");
            Compile();
            var lab = UnityEngine.Object.FindObjectOfType<PersistenceLab>();
            lab.BuildLabel = "PO-PERSIST-v1 scalar=" + ScalarCount + " SDK=3.10.5 Unity=" + Application.unityVersion;
            var descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            foreach (var probe in UnityEngine.Object.FindObjectsOfType<PersistenceProbe>())
            {
                if (probe is PersistenceScalars) probe.IntCount = ScalarCount;
                var ids = descriptor.NetworkIDCollection.Where(x => x.gameObject == probe.gameObject).ToArray();
                Require(ids.Length == 1, "Missing or duplicate network ID: " + probe.name);
                probe.SceneNetworkId = ids[0].ID;
                UdonSharpEditorUtility.CopyProxyToUdon(probe);
            }
            UdonSharpEditorUtility.CopyProxyToUdon(lab);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            ValidateScene();
        }

        private static int ScalarCount => typeof(PersistenceScalars).GetFields().Count(x => x.IsDefined(typeof(UdonSyncedAttribute)));

        private static void Compile(bool editorBuild = true)
        {
            foreach (string name in new[] { "PersistenceScalars", "PersistenceFixed27", "PersistenceArray27", "PersistenceLab" })
            {
                string path = SourceDir + name;
                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path + ".asset") != null) continue;
                var asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                asset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path + ".cs");
                AssetDatabase.CreateAsset(asset, path + ".asset");
            }
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = editorBuild });
            foreach (string name in new[] { "PersistenceScalars", "PersistenceFixed27", "PersistenceArray27", "PersistenceLab" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(SourceDir + name + ".asset");
                Require(asset.CompiledVersion == UdonSharpProgramVersion.CurrentVersion && asset.SerializedProgramAsset.RetrieveProgram() != null,
                    "UdonSharp compilation incomplete: " + name);
            }
        }

        [MenuItem("Tools/PlayerObject Persistence/Compile Client Udon Without Build")]
        public static void CompileClientUdon()
        {
            GuardEditMode();
            Require(SceneManager.GetActiveScene().path == ScenePath, "Open experiment scene.");
            Compile(false);
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/client-udon-compilation.txt", "UTC=" + DateTime.UtcNow.ToString("O")
                + "\nPASS UdonSharp client compilation (IsEditorBuild=false), four concrete experiment programs.\n"
                + "No Unity player build, VRChat Build & Test, client launch or upload was performed.\n", new UTF8Encoding(false));
            Debug.Log("[PersistenceExperiment] PASS client-target Udon compilation only; no build or client launch.");
        }

        [MenuItem("Tools/PlayerObject Persistence/Schema/19 ints (baseline)")] public static void Schema19() { SetSchema(19); }
        [MenuItem("Tools/PlayerObject Persistence/Schema/20 ints")] public static void Schema20() { SetSchema(20); }
        [MenuItem("Tools/PlayerObject Persistence/Schema/23 ints")] public static void Schema23() { SetSchema(23); }
        [MenuItem("Tools/PlayerObject Persistence/Schema/26 ints")] public static void Schema26() { SetSchema(26); }
        [MenuItem("Tools/PlayerObject Persistence/Schema/27 ints (19 plus 8)")] public static void Schema27() { SetSchema(27); }
        [MenuItem("Tools/PlayerObject Persistence/Schema/28 ints")] public static void Schema28() { SetSchema(28); }
        [MenuItem("Tools/PlayerObject Persistence/Schema/32 ints")] public static void Schema32() { SetSchema(32); }

        private static void SetSchema(int count)
        {
            GuardEditMode();
            Require(SceneManager.GetActiveScene().path == ScenePath, "Open experiment scene before changing the schema.");
            string path = SourceDir + "PersistenceScalars.cs";
            string source = File.ReadAllText(path);
            var pattern = new Regex(@"(?<=// SCHEMA-BEGIN[^\r\n]*\r?\n).*?(?=        // SCHEMA-END)", RegexOptions.Singleline);
            Require(pattern.Matches(source).Count == 1, "Schema markers are missing or ambiguous.");
            var fields = new StringBuilder();
            for (int i = 1; i <= count; i++) fields.Append("        [UdonSynced] public int V").Append(i.ToString("D3")).Append(" = 0;\n");
            File.WriteAllText(path, pattern.Replace(source, fields.ToString()), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            Debug.Log("[PersistenceExperiment] Schema source changed to " + count
                + ". Wait for C# reload, then Compile and Finish Setup. Existing scene objects, GUIDs and Network IDs retained.");
        }

        private static void CreateUI(PersistenceLab lab)
        {
            var root = new GameObject("PersistencePanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(VRCUiShape));
            root.layer = 0;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1600, 950);
            rt.position = new Vector3(0, 1.8f, 3.6f);
            rt.localScale = Vector3.one * 0.003f;
            Image background = Rect("Background", root.transform, 0, 0, 1600, 950).gameObject.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;
            Label(root.transform, "Title", "PlayerObject Persistence Lab", 0, 425, 1500, 70, 42);
            Label(root.transform, "Guide", "① Rejoin後は先に読取り・出力  ② seedで書込  ③ 送信ログを出力  ④ Rejoinして同じseedを比較", 0, 370, 1500, 55, 24);
            var target = UdonSharpEditorUtility.GetBackingUdonBehaviour(lab);
            Button(root.transform, target, "変更対象 scalar", "_SelectGrowing", -490, 308, 430);
            Button(root.transform, target, "固定27 scalar", "_SelectFixed", -20, 308, 430);
            Button(root.transform, target, "int[27] 対照", "_SelectArray", 450, 308, 430);
            Label(root.transform, "RunLabel", "run / 条件メモ", -675, 250, 200, 40, 22);
            lab.RunInput = Input(root.transform, "RunInput", "initial-19", -370, 250, 380, 45, false);
            Label(root.transform, "SeedLabel", "seed", -90, 250, 85, 40, 22);
            lab.SeedInput = Input(root.transform, "SeedInput", "101", 35, 250, 150, 45, false);
            lab.SeedInput.contentType = InputField.ContentType.IntegerNumber;
            Label(root.transform, "PrefixLabel", "比較数 / 先頭N", 255, 250, 230, 40, 22);
            lab.PrefixInput = Input(root.transform, "PrefixInput", "19", 470, 250, 140, 45, false);
            lab.PrefixInput.contentType = InputField.ContentType.IntegerNumber;
            var writes = new List<Button>();
            Button(root.transform, target, "読取り", "_Read", -670, 180, 210);
            Button(root.transform, target, "seedと比較", "_CheckExpected", -430, 180, 230);
            Button(root.transform, target, "使用量取得", "_RequestUsage", -170, 180, 230);
            Button(root.transform, target, "TSV出力", "_Export", 505, 180, 550);
            writes.Add(Button(root.transform, target, "全int書込＋送信", "_WriteAll", -650, 110, 265));
            writes.Add(Button(root.transform, target, "先頭N書込＋送信", "_WritePrefix", -355, 110, 265));
            writes.Add(Button(root.transform, target, "値を変えず送信", "_RequestOnly", -60, 110, 265));
            writes.Add(Button(root.transform, target, "診断: NULLのみ0初期化＋送信", "_InitializeNulls", -355, 40, 850));
            lab.WriteButtons = writes.ToArray();
            foreach (var button in lab.WriteButtons) button.interactable = false;
            lab.StatusText = Label(root.transform, "Status", "復元待ち。書込みはOnPlayerRestored後に有効。", -340, -190, 875, 410, 22);
            lab.StatusText.alignment = TextAnchor.UpperLeft;
            lab.ExportInput = Input(root.transform, "ExportInput", "TSV出力ボタンでログを固定表示。\nDesktop: クリック → Ctrl+A → Ctrl+C\n別ファイルへ貼付し、退出前に保存。", 505, -145, 550, 560, true);
            lab.ExportInput.readOnly = true;
            Label(root.transform, "Warning", "自動書込なし / success = 送信結果 (クラウド保存完了ではない) / NULL初期化は生ログを保存した後の別試験", 0, -440, 1500, 50, 21);
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 0;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static Text Label(Transform parent, string name, string text, float x, float y, float w, float h, int size)
        {
            var label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = new Color(0.9f, 0.95f, 1);
            label.alignment = TextAnchor.MiddleCenter;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, UdonBehaviour target, string text, string method, float x, float y, float width)
        {
            var rt = Rect(method, parent, x, y, width, 55);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = method == "_InitializeNulls" ? new Color(0.45f, 0.22f, 0.06f) : new Color(0.1f, 0.25f, 0.39f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f);
            button.colors = colors;
            Label(rt, "Label", text, 0, 0, width - 12, 50, 22);
            UnityEventTools.AddStringPersistentListener(button.onClick, target.SendCustomEvent, method);
            return button;
        }

        private static InputField Input(Transform parent, string name, string initial, float x, float y, float w, float h, bool multiline)
        {
            var rt = Rect(name, parent, x, y, w, h);
            rt.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.16f, 0.2f);
            var input = rt.gameObject.AddComponent<InputField>();
            rt.gameObject.AddComponent<RectMask2D>();
            input.textComponent = Label(rt, "Text", "", 0, 0, w - 24, h - 12, multiline ? 18 : 23);
            input.textComponent.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            input.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            input.textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.characterLimit = multiline ? 0 : 160;
            input.text = initial;
            return input;
        }

        [MenuItem("Tools/PlayerObject Persistence/Validate Scene")]
        public static void ValidateScene()
        {
            Require(!EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "Open experiment scene in Edit Mode.");
            var lab = UnityEngine.Object.FindObjectOfType<PersistenceLab>();
            Require(lab != null && lab.ExportInput.readOnly && lab.ExportInput.characterLimit == 0, "Missing or limited export input.");
            var canvas = GameObject.Find("PersistencePanel").GetComponent<Canvas>();
            Require(canvas.renderMode == RenderMode.WorldSpace && canvas.GetComponent<VRCUiShape>() != null && canvas.gameObject.layer == 0, "World UI setup incomplete.");
            var report = new StringBuilder("Editor scene validation; not VRChat reproduction.\nUTC=" + DateTime.UtcNow.ToString("O") + "\n");
            var probes = UnityEngine.Object.FindObjectsOfType<PersistenceProbe>();
            Require(probes.Length == 3, "Expected three independent PlayerObject templates.");
            foreach (var probe in probes.OrderBy(x => x.CaseIndex))
            {
                Require(probe.Lab == lab && probe.GetComponent<VRCPlayerObject>() != null && probe.GetComponent<VRCEnablePersistence>() != null, "Missing probe wiring.");
                var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(probe);
                var program = (UdonSharpProgramAsset)ub.programSource;
                Require(ub.SyncMethod == Networking.SyncType.Manual, "Probe sync must be Manual.");
                var fields = program.fieldDefinitions.Values.Where(x => x.SyncMode.HasValue).ToArray();
                Require(fields.Length == (probe.ArrayMode ? 1 : probe.IntCount), "Compiled synced field count mismatch: " + probe.name);
                Require(fields.All(x => x.UserType == (probe.ArrayMode ? typeof(int[]) : typeof(int))), "Unexpected synced field type.");
                Require(probe.SceneNetworkId > 0, "Network ID is missing.");
                report.Append(probe.name).Append(" networkId=").Append(probe.SceneNetworkId).Append(" intCount=").Append(probe.IntCount)
                    .Append(" compiledSyncedFields=").Append(fields.Length).Append(" scriptGuid=")
                    .Append(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(program.sourceCsScript)))
                    .Append(" programGuid=").Append(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(program))).Append('\n');
            }
            foreach (var button in canvas.GetComponentsInChildren<Button>())
                Require(button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == UdonSharpEditorUtility.GetBackingUdonBehaviour(lab)
                    && button.onClick.GetPersistentMethodName(0) == "SendCustomEvent", "Invalid button binding: " + button.name);
            Require(canvas.GetComponentsInChildren<Button>().Length == 11, "Missing buttons.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/editor-validation.txt", report + "PASS scene, component markers, Manual sync, compiled field counts, stable IDs, 11 UI bindings, unlimited read-only export.\n", new UTF8Encoding(false));
            Debug.Log("[PersistenceExperiment] PASS scene validation: three probes, compiled schemas, stable IDs, UI wiring.");
        }

        [MenuItem("Tools/PlayerObject Persistence/Run Live Editor Checks")]
        public static void RunLiveChecks()
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "Run ClientSim and wait for restore first.");
            var lab = UnityEngine.Object.FindObjectOfType<PersistenceLab>();
            var controller = UdonSharpEditorUtility.GetBackingUdonBehaviour(lab);
            var objects = Networking.GetPlayerObjects(Networking.LocalPlayer);
            var live = objects.SelectMany(x => x.GetComponentsInChildren<UdonBehaviour>())
                .Where(x => x.programSource is UdonSharpProgramAsset p && typeof(PersistenceProbe).IsAssignableFrom(p.sourceCsScript.GetClass())).ToArray();
            Require(live.Length == 3, "Expected three spawned local probes.");
            var evidence = new StringBuilder("Live Udon VM checks in Editor/ClientSim; not real-client persistence evidence.\nUTC=" + DateTime.UtcNow.ToString("O") + "\n");
            foreach (var probe in live.OrderBy(x => (int)x.GetProgramVariable("CaseIndex")))
            {
                int index = (int)probe.GetProgramVariable("CaseIndex");
                int count = (int)probe.GetProgramVariable("IntCount");
                bool array = (bool)probe.GetProgramVariable("ArrayMode");
                Require((bool)probe.GetProgramVariable("Restored") && Networking.IsOwner(probe.gameObject), "Probe not restored or not owned.");
                string select = index == 0 ? "_SelectGrowing" : index == 1 ? "_SelectFixed" : "_SelectArray";
                lab.PrefixInput.text = (count + 1).ToString();
                GameObject.Find("PersistencePanel/" + select).GetComponent<Button>().onClick.Invoke();
                Require(lab.PrefixInput.text == count.ToString(), "Selection did not clamp oversized prefix.");
                int previousRequests = (int)probe.GetProgramVariable("Requests");
                probe.SetProgramVariable("Restored", false);
                probe.SendCustomEvent("_Request");
                Require((int)probe.GetProgramVariable("Requests") == previousRequests, "Pre-restore request guard failed.");
                probe.SetProgramVariable("Restored", true);
                lab.SeedInput.text = "invalid";
                GameObject.Find("PersistencePanel/_WriteAll").GetComponent<Button>().onClick.Invoke();
                Require((int)probe.GetProgramVariable("Requests") == previousRequests, "Invalid seed caused serialization.");
                lab.SeedInput.text = "701";
                lab.PrefixInput.text = "invalid"; // Full writes do not depend on the comparison/prefix-only input.
                lab.RunInput.text = "editor-self-check";
                GameObject.Find("PersistencePanel/_WriteAll").GetComponent<Button>().onClick.Invoke();
                Require((int)probe.GetProgramVariable("Requests") == previousRequests + 1, "Write button did not reach Udon request.");
                for (int i = 0; i < count; i++)
                {
                    object value = array ? ((int[])probe.GetProgramVariable("Values"))[i] : probe.GetProgramVariable("V" + (i + 1).ToString("D3"));
                    Require(value != null && (int)value == 70100 + i + 1, "Live Udon pattern mismatch.");
                }
                lab.PrefixInput.text = count.ToString();
                GameObject.Find("PersistencePanel/_CheckExpected").GetComponent<Button>().onClick.Invoke();
                Require(lab.StatusText.text.Contains(count + " / " + count + " 一致"), "Expected comparison failed.");
                evidence.Append("case=").Append(index).Append(" PASS restored, ownership, pre-restore guard, invalid input, UI write, ")
                    .Append(count).Append(" values, expected comparison\n");
                if (index == 0)
                {
                    string lastKey = "V" + count.ToString("D3");
                    probe.SetProgramVariable(lastKey, null); // Synthetic Editor heap fault; never a real-client reproduction claim.
                    Require(probe.GetProgramVariable(lastKey) == null, "Synthetic null was not injected.");
                    controller.SendCustomEvent("_Read");
                    Require(lab.StatusText.text.Contains("NULL"), "Raw null was not displayed safely.");
                    controller.SendCustomEvent("_CheckExpected");
                    Require(lab.StatusText.text.Contains((count - 1) + " / " + count + " 一致"), "Null-safe comparison failed.");
                    lab.PrefixInput.text = "1";
                    GameObject.Find("PersistencePanel/_WritePrefix").GetComponent<Button>().onClick.Invoke();
                    Require(probe.GetProgramVariable(lastKey) == null, "Prefix write silently initialized an untouched null.");
                    GameObject.Find("PersistencePanel/_InitializeNulls").GetComponent<Button>().onClick.Invoke();
                    Require((int)probe.GetProgramVariable(lastKey) == 0, "Explicit null initialization failed.");
                    lab.PrefixInput.text = count.ToString();
                    GameObject.Find("PersistencePanel/_WriteAll").GetComponent<Button>().onClick.Invoke();
                    Require((int)probe.GetProgramVariable(lastKey) == 70100 + count, "Pattern not restored after synthetic test.");
                    evidence.Append("case=0 PASS synthetic null read, null-safe comparison, prefix preserves null, explicit null initialization\n");
                }
            }
            GameObject.Find("PersistencePanel/_Export").GetComponent<Button>().onClick.Invoke();
            Require(lab.ExportInput.text.Contains("player-restored") && lab.ExportInput.text.Contains("check-expected")
                && lab.ExportInput.text.Contains("cloud_commit_ack=unavailable"), "Export missing observation evidence.");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/editor-live-checks.txt", evidence + "PASS export includes restore and comparison events; no cloud-ack claim.\n", new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/raw-editor.tsv", lab.ExportInput.text, new UTF8Encoding(false));
            Debug.Log("[PersistenceExperiment] PASS live Editor checks from UdonBehaviour.GetProgramVariable, including UI dispatch.");
        }

        [MenuItem("Tools/PlayerObject Persistence/Export Live Editor Log")]
        public static void ExportLiveLog()
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "Run experiment in Play Mode.");
            var lab = UnityEngine.Object.FindObjectOfType<PersistenceLab>();
            UdonSharpEditorUtility.GetBackingUdonBehaviour(lab).SendCustomEvent("_Export");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/raw-editor.tsv", lab.ExportInput.text, new UTF8Encoding(false));
            Debug.Log("[PersistenceExperiment] Exported Editor Udon observation log.");
        }

        [MenuItem("Tools/PlayerObject Persistence/Check Restored Editor Pattern")]
        public static void CheckRestoredPattern()
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath, "Run ClientSim and wait for restore.");
            var lab = UnityEngine.Object.FindObjectOfType<PersistenceLab>();
            var controller = UdonSharpEditorUtility.GetBackingUdonBehaviour(lab);
            var evidence = new StringBuilder("Read-only restored pattern checks in Editor/ClientSim; no writes requested.\nUTC=" + DateTime.UtcNow.ToString("O") + "\n");
            foreach (var go in Networking.GetPlayerObjects(Networking.LocalPlayer))
            foreach (var probe in go.GetComponentsInChildren<UdonBehaviour>())
            {
                if (!(probe.programSource is UdonSharpProgramAsset program) || !typeof(PersistenceProbe).IsAssignableFrom(program.sourceCsScript.GetClass())) continue;
                int count = (int)probe.GetProgramVariable("IntCount");
                int index = (int)probe.GetProgramVariable("CaseIndex");
                bool array = (bool)probe.GetProgramVariable("ArrayMode");
                Require((bool)probe.GetProgramVariable("Restored"), "Wait for restore.");
                Require((int)probe.GetProgramVariable("Requests") == 0, "Unexpected request before observation.");
                int expectedCount = index == 0 ? Math.Min(19, count) : 27;
                for (int i = 0; i < expectedCount; i++)
                {
                    object value = array ? ((int[])probe.GetProgramVariable("Values"))[i] : probe.GetProgramVariable("V" + (i + 1).ToString("D3"));
                    Require(value != null && (int)value == 70100 + i + 1, "Restored pattern mismatch; inspect raw log.");
                }
                controller.SendCustomEvent(index == 0 ? "_SelectGrowing" : index == 1 ? "_SelectFixed" : "_SelectArray");
                lab.SeedInput.text = "701";
                lab.PrefixInput.text = expectedCount.ToString();
                controller.SendCustomEvent("_CheckExpected");
                evidence.Append("case=").Append(index).Append(" PASS restored values=").Append(expectedCount).Append(" requests=0 schema=").Append(count).Append('\n');
            }
            controller.SendCustomEvent("_Export");
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportDir + "/editor-restored-" + ScalarCount + ".txt", evidence.ToString(), new UTF8Encoding(false));
            File.WriteAllText(ReportDir + "/raw-editor-restored-" + ScalarCount + ".tsv", lab.ExportInput.text, new UTF8Encoding(false));
            Debug.Log("[PersistenceExperiment] PASS read-only restored Editor pattern (seed 701); requests remain zero.");
        }

        [MenuItem("Tools/PlayerObject Persistence/Capture Panel")]
        public static void CapturePanel()
        {
            Require(SceneManager.GetActiveScene().path == ScenePath, "Open experiment scene.");
            var camera = Camera.main;
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(ReportDir);
                File.WriteAllBytes(ReportDir + "/editor-panel.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
            Debug.Log("[PersistenceExperiment] Captured panel.");
        }

        private static void GuardEditMode()
        {
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling, "Wait for Edit Mode and compilation.");
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Preserve unsaved scenes first.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
