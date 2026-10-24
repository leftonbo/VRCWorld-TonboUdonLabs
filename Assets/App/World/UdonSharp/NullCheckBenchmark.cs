using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NullCheckBenchmark : UdonSharpBehaviour
    {
        #region Configuration
        public GameObject ValidObject;
        public Material AssetReference;
        public UdonBehaviour ValidBehaviour;
        public GameObject DestroyObject;
        public UdonBehaviour DestroyBehaviour;
        public int Warmups = 5;
        public int Batches = 30;
        public int CasesPerFrame = 4;
        #endregion

        #region Results
        [NonSerialized] public bool Complete;
        [NonSerialized] public int Rows, Failures;
        [NonSerialized] public string LastCsv, SemanticsCsv, Status = "ready";
        [NonSerialized] public GameObject DestroyedGameObject;
        [NonSerialized] public Transform DestroyedTransform;
        [NonSerialized] public Material DestroyedMaterial;
        [NonSerialized] public UdonBehaviour DestroyedUdon;
        #endregion

        #region Runtime state
        private GameObject[] _go;
        private Material[] _asset;
        private Transform[] _component;
        private UdonBehaviour[] _udon;
        private UnityEngine.Object[] _object;
        private bool[] _mask;
        private int _config, _run, _phase, _position, _n;
        private bool _active;
        private long _frequency;
        private StringBuilder _csv;
        private string _environment;
        private const int CaseCount = 17;
        private string[] _names = new string[] { "clock", "baseline_bool",
            "gameobject_ne", "gameobject_valid", "gameobject_implicit",
            "material_ne", "material_valid", "material_implicit",
            "transform_ne", "transform_valid", "transform_implicit",
            "udonbehaviour_ne", "udonbehaviour_valid", "udonbehaviour_implicit",
            "object_ne", "object_valid", "object_implicit" };
        #endregion

        private void Start()
        {
            // Snapshot references BEFORE destroying only dedicated experiment inputs.
            DestroyedGameObject = DestroyObject;
            DestroyedTransform = DestroyObject.transform;
            DestroyedUdon = DestroyBehaviour;
            // Renderer.material creates a runtime clone; never destroy a persistent asset.
            DestroyedMaterial = DestroyObject.GetComponent<MeshRenderer>().material;
            Destroy(DestroyObject);
            Destroy(DestroyBehaviour.gameObject);
            Destroy(DestroyedMaterial);
            SendCustomEventDelayedFrames(nameof(_Begin), 120);
        }

        /// <summary>Start a fresh Udon VM run after deferred destruction has completed.</summary>
        public void _Begin()
        {
            if (_active || Complete || Warmups < 0 || Warmups > 10 || Batches < 1 || Batches > 60
                || CasesPerFrame < 1 || CasesPerFrame > 4) return;
            _frequency = Stopwatch.Frequency;
#if UNITY_EDITOR
            _environment = "Editor-UdonVM";
#else
            _environment = "VRChat-client";
#endif
            _go = new GameObject[2]; _asset = new Material[2]; _component = new Transform[2];
            _udon = new UdonBehaviour[2]; _object = new UnityEngine.Object[2]; _mask = new bool[2];
            _n = 2;
            StringBuilder semantics = new StringBuilder();
            semantics.AppendLine("input,case,checksum,expected,ok");
            for (int input = 0; input < 4; input++)
            {
                SetInput(input);
                for (int c = 1; c < CaseCount; c++)
                {
                    int sum = Run(c);
                    int expected = input == 0 ? 2 : (input == 2 ? 1 : 0);
                    bool ok = sum == expected;
                    if (!ok) Failures++;
                    semantics.AppendLine(input + "," + _names[c] + "," + sum + "," + expected + "," + (ok ? 1 : 0));
                }
            }
            SemanticsCsv = semantics.ToString();
            if (Failures != 0) { Status = "SEMANTIC FAILURE"; return; }
            _csv = new StringBuilder();
            _csv.AppendLine("experiment_id,environment,config,input,run,case,order,batch,iterations,elapsed_ticks,frequency,checksum,expected,checksum_ok");
            _config = 0; _run = 0; _phase = 0; _position = 0;
            _active = true; Setup();
            Debug.Log("[NullCheck] begin frequency=" + _frequency + " highResolution=" + Stopwatch.IsHighResolution);
            SendCustomEventDelayedFrames(nameof(_Next), 1);
        }

        private void SetInput(int input)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                bool valid = input == 0 || (input == 2 && slot == 0);
                _go[slot] = valid ? ValidObject : (input == 3 ? DestroyedGameObject : null);
                _asset[slot] = valid ? AssetReference : (input == 3 ? DestroyedMaterial : null);
                _component[slot] = valid ? ValidObject.transform : (input == 3 ? DestroyedTransform : null);
                _udon[slot] = valid ? ValidBehaviour : (input == 3 ? DestroyedUdon : null);
                _object[slot] = valid ? ValidObject : (input == 3 ? DestroyedGameObject : null);
                _mask[slot] = valid;
            }
        }

        private void Setup()
        {
            _n = _config < 4 ? 128 : 1024;
            SetInput(_config % 4);
        }

        /// <summary>Measure short batches and yield between groups of at most four cases.</summary>
        public void _Next()
        {
            if (!_active) return;
            for (int step = 0; step < CasesPerFrame; step++)
            {
                // Rotate cases by batch; second run reverses the order.
                int c = (_position + _phase) % CaseCount;
                if (_run == 1) c = CaseCount - 1 - c;
                long start = Stopwatch.GetTimestamp();
                int sum = Run(c);
                long ticks = Stopwatch.GetTimestamp() - start;
                int input = _config % 4;
                int expected = c == 0 ? 0 : (input == 0 ? _n : (input == 2 ? _n / 2 : 0));
                bool ok = sum == expected;
                if (!ok) Failures++;
                if (_phase >= Warmups)
                {
                    _csv.AppendLine("UDON-NULL-T209," + _environment + "," + _config + "," + input + "," + _run
                        + "," + _names[c] + "," + _position + "," + (_phase - Warmups) + "," + _n
                        + "," + ticks + "," + _frequency + "," + sum + "," + expected + "," + (ok ? 1 : 0));
                    Rows++;
                }
                _position++;
                if (_position == CaseCount)
                {
                    _position = 0; _phase++;
                    if (_phase == Warmups + Batches)
                    {
                        _phase = 0; _run++;
                        if (_run == 2)
                        {
                            _run = 0; _config++;
                            if (_config == 8)
                            {
                                _active = false; Complete = true; LastCsv = _csv.ToString();
                                Status = Failures == 0 ? "complete" : "CHECKSUM FAILURE";
                                Debug.Log("[NullCheck] " + Status + " rows=" + Rows + " failures=" + Failures);
                                return;
                            }
                            Setup();
                        }
                    }
                }
            }
            Status = "config=" + _config + " run=" + _run + " phase=" + _phase + " rows=" + Rows;
            SendCustomEventDelayedFrames(nameof(_Next), 1);
        }

        private int Run(int c)
        {
            int sum = 0;
            switch (c)
            {
                case 0: return 0;
                case 1:
                    for (int i = 0; i < _n; i++) { bool obj = _mask[i & 1]; if (obj) sum++; }
                    break;
                case 2:
                    for (int i = 0; i < _n; i++) { GameObject obj = _go[i & 1]; if (obj != null) sum++; }
                    break;
                case 3:
                    for (int i = 0; i < _n; i++) { GameObject obj = _go[i & 1]; if (Utilities.IsValid(obj)) sum++; }
                    break;
                case 4:
                    for (int i = 0; i < _n; i++) { GameObject obj = _go[i & 1]; if (obj) sum++; }
                    break;
                case 5:
                    for (int i = 0; i < _n; i++) { Material obj = _asset[i & 1]; if (obj != null) sum++; }
                    break;
                case 6:
                    for (int i = 0; i < _n; i++) { Material obj = _asset[i & 1]; if (Utilities.IsValid(obj)) sum++; }
                    break;
                case 7:
                    for (int i = 0; i < _n; i++) { Material obj = _asset[i & 1]; if (obj) sum++; }
                    break;
                case 8:
                    for (int i = 0; i < _n; i++) { Transform obj = _component[i & 1]; if (obj != null) sum++; }
                    break;
                case 9:
                    for (int i = 0; i < _n; i++) { Transform obj = _component[i & 1]; if (Utilities.IsValid(obj)) sum++; }
                    break;
                case 10:
                    for (int i = 0; i < _n; i++) { Transform obj = _component[i & 1]; if (obj) sum++; }
                    break;
                case 11:
                    for (int i = 0; i < _n; i++) { UdonBehaviour obj = _udon[i & 1]; if (obj != null) sum++; }
                    break;
                case 12:
                    for (int i = 0; i < _n; i++) { UdonBehaviour obj = _udon[i & 1]; if (Utilities.IsValid(obj)) sum++; }
                    break;
                case 13:
                    for (int i = 0; i < _n; i++) { UdonBehaviour obj = _udon[i & 1]; if (obj) sum++; }
                    break;
                case 14:
                    for (int i = 0; i < _n; i++) { UnityEngine.Object obj = _object[i & 1]; if (obj != null) sum++; }
                    break;
                case 15:
                    for (int i = 0; i < _n; i++) { UnityEngine.Object obj = _object[i & 1]; if (Utilities.IsValid(obj)) sum++; }
                    break;
                case 16:
                    for (int i = 0; i < _n; i++) { UnityEngine.Object obj = _object[i & 1]; if (obj) sum++; }
                    break;
            }
            return sum;
        }
    }
}
