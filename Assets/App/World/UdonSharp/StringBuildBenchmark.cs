using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class StringBuildBenchmark : UdonSharpBehaviour
    {
        #region Configuration
        public int Warmups = 5;
        public int Batches = 30;
        // One batch per frame bounds the stall independently of the display rate.
        public int StartDelayFrames = 120;
        #endregion

        #region Results
        [NonSerialized] public bool Complete;
        [NonSerialized] public int Rows, Failures;
        [NonSerialized] public string LastCsv, SemanticsCsv, Status = "ready", LastOutput;
        #endregion

        #region Runtime state
        private const int CaseCount = 10;
        private string[] _names = new string[] { "baseline", "plus_expression", "plus_steps", "sb_new_split", "sb_new_chain", "sb_capacity_split", "sb_capacity_chain", "sb_reuse_split", "sb_reuse_chain", "clock" };
        private string[] _fragments, _labels, _strings;
        private int[] _values;
        private StringBuilder _reuse, _csv, _semantics;
        private string _expected, _environment;
        private int _config, _run, _phase, _position, _n, _workload, _lines, _capacity, _preflightInput;
        private long _frequency;
        private bool _active;
        private int _exportOffset;
        private string _exportText;
        #endregion

        private void Start()
        {
            SendCustomEventDelayedFrames(nameof(_Begin), StartDelayFrames);
        }

        public void _Begin()
        {
            if (_active || Complete || Warmups < 1 || Warmups > 10 || Batches < 1 || Batches > 60) return;
            _frequency = Stopwatch.Frequency;
#if UNITY_EDITOR
            _environment = "Editor-UdonVM";
#else
            _environment = "VRChat-client";
#endif
            _fragments = new string[8]; _labels = new string[128];
            _values = new int[128]; _strings = new string[128];
            // Runtime-created inputs keep the non-literal workloads out of constant folding.
            for (int j = 0; j < 8; j++) _fragments[j] = "Token" + j.ToString() + "|";
            for (int j = 0; j < 128; j++)
            {
                _labels[j] = "Stat" + j.ToString();
                _values[j] = 37 + j * 17;
                _strings[j] = _values[j].ToString();
            }
            _semantics = new StringBuilder();
            _semantics.AppendLine("input,config,case,length,expected_length,exact_equal");
            _active = true;
            SendCustomEventDelayedFrames(nameof(_Preflight), 1);
        }

        public void _Preflight()
        {
            if (!_active) return;
            // Yield each configuration so correctness checks cannot become a long startup stall.
            Setup(); _n = 2;
            for (int c = 0; c < CaseCount - 1; c++)
            {
                int sum = Run(c);
                bool ok = LastOutput == _expected && sum == _n * _expected.Length;
                if (!ok) Failures++;
                _semantics.AppendLine(_preflightInput + "," + _config + "," + _names[c] + "," + LastOutput.Length
                    + "," + _expected.Length + "," + (ok ? 1 : 0));
            }
            _config++;
            if (_config == 16)
            {
                _config = 0; _preflightInput++;
                for (int j = 0; j < 128; j++) { _values[j] += 100; _strings[j] = _values[j].ToString(); }
            }
            if (_preflightInput < 2) { SendCustomEventDelayedFrames(nameof(_Preflight), 1); return; }
            // Restore the documented measurement values after preflight.
            for (int j = 0; j < 128; j++) { _values[j] = 37 + j * 17; _strings[j] = _values[j].ToString(); }
            SemanticsCsv = _semantics.ToString();
            if (Failures != 0) { Status = "SEMANTIC FAILURE"; _active = false; return; }
            _csv = new StringBuilder();
            _csv.AppendLine("experiment_id,environment,config,workload,lines,run,case,order,batch,iterations,elapsed_ticks,frequency,checksum,expected,exact_equal,capacity");
            _config = 0; _run = 0; _phase = 0; _position = 0; Setup(); _active = true;
            Debug.Log("[StringBuild] begin frequency=" + _frequency + " highResolution=" + Stopwatch.IsHighResolution);
            SendCustomEventDelayedFrames(nameof(_Next), 1);
        }

        private void Setup()
        {
            int condition = _config % 8;
            _workload = condition < 2 ? condition : (condition < 5 ? 2 : 3);
            _lines = condition < 2 ? 1 : (condition % 3 == 2 ? 8 : (condition % 3 == 0 ? 32 : 128));
            // Same N within a condition; larger documents get fewer repetitions.
            _n = condition < 2 ? 64 : (_lines == 8 ? 16 : (_lines == 32 ? 4 : 2));
            if (_config >= 8) _n *= 4;
            _expected = "";
            if (_workload == 0) _expected = "Token0|Token1|Token2|Token3|Token4|Token5|Token6|Token7|";
            else if (_workload == 1)
                for (int j = 0; j < 8; j++) _expected += _fragments[j];
            else
                for (int j = 0; j < _lines; j++) _expected += _labels[j] + ": " + _strings[j] + "\n";
            _capacity = _expected.Length;
            _reuse = new StringBuilder(_capacity);
        }

        public void _Next()
        {
            if (!_active) return;
            int c = (_position + _phase) % CaseCount;
            if (_run == 1) c = CaseCount - 1 - c;
            long start = Stopwatch.GetTimestamp();
            int sum = Run(c);
            long ticks = Stopwatch.GetTimestamp() - start;
            int expected = c == CaseCount - 1 ? 0 : _n * _expected.Length;
            bool ok = sum == expected && (c == CaseCount - 1 || LastOutput == _expected);
            if (!ok) Failures++;
            if (_phase >= Warmups)
            {
                _csv.AppendLine("UDON-STRING-T228," + _environment + "," + _config + "," + _workload + "," + _lines
                    + "," + _run + "," + _names[c] + "," + _position + "," + (_phase - Warmups) + "," + _n
                    + "," + ticks + "," + _frequency + "," + sum + "," + expected
                    + "," + (ok ? 1 : 0) + "," + _reuse.Capacity);
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
                        if (_config == 16)
                        {
                            _active = false; Complete = true; LastCsv = _csv.ToString();
                            Status = Failures == 0 ? "complete" : "OUTPUT FAILURE";
                            Debug.Log("[StringBuild] " + Status + " rows=" + Rows + " failures=" + Failures);
#if !UNITY_EDITOR
                            // Hardware/client version comes from the client's standard startup log;
                            // SystemInfo/Application metadata getters are not exposed to Udon here.
                            Debug.Log("[StringBuildMeta] highResolution=" + Stopwatch.IsHighResolution);
                            _exportText = LastCsv.Replace("\r", "").Replace("\n", "~");
                            SendCustomEventDelayedSeconds(nameof(_ExportCsv), 1f);
#endif
                            return;
                        }
                        Setup();
                    }
                }
            }
            Status = "config=" + _config + " run=" + _run + " phase=" + _phase + " rows=" + Rows;
            SendCustomEventDelayedFrames(nameof(_Next), 1);
        }

        /// <summary>Client export happens only after all timed batches; pace logs below 4/second.</summary>
        public void _ExportCsv()
        {
            if (!Complete || _exportText == null) return;
            int length = Mathf.Min(1800, _exportText.Length - _exportOffset);
            Debug.Log("[StringBuildCSV:" + _exportOffset + "]" + _exportText.Substring(_exportOffset, length));
            _exportOffset += length;
            if (_exportOffset < _exportText.Length) SendCustomEventDelayedSeconds(nameof(_ExportCsv), 0.3f);
            else Debug.Log("[StringBuildCSV-END] chars=" + _exportText.Length + " rows=" + Rows + " failures=" + Failures);
        }

        private int Run(int c)
        {
            int sum = 0;
            // Dispatch once per batch. Every case includes one same-behaviour call per document,
            // Length accumulation and final retention; baseline returns the prebuilt output.
            switch (c)
            {
                case 0:
                    for (int i = 0; i < _n; i++) { LastOutput = Baseline(); sum += LastOutput.Length; }
                    break;
                case 1:
                    for (int i = 0; i < _n; i++) { LastOutput = PlusExpression(); sum += LastOutput.Length; }
                    break;
                case 2:
                    for (int i = 0; i < _n; i++) { LastOutput = PlusSteps(); sum += LastOutput.Length; }
                    break;
                case 3:
                    for (int i = 0; i < _n; i++) { LastOutput = SbNewSplit(); sum += LastOutput.Length; }
                    break;
                case 4:
                    for (int i = 0; i < _n; i++) { LastOutput = SbNewChain(); sum += LastOutput.Length; }
                    break;
                case 5:
                    for (int i = 0; i < _n; i++) { LastOutput = SbCapacitySplit(); sum += LastOutput.Length; }
                    break;
                case 6:
                    for (int i = 0; i < _n; i++) { LastOutput = SbCapacityChain(); sum += LastOutput.Length; }
                    break;
                case 7:
                    for (int i = 0; i < _n; i++) { LastOutput = SbReuseSplit(); sum += LastOutput.Length; }
                    break;
                case 8:
                    for (int i = 0; i < _n; i++) { LastOutput = SbReuseChain(); sum += LastOutput.Length; }
                    break;
            }
            return sum;
        }

        private string Baseline()
        {
            return _expected;
        }

        private string PlusExpression()
        {
            if (_workload == 0) return "Token0|" + "Token1|" + "Token2|" + "Token3|" + "Token4|" + "Token5|" + "Token6|" + "Token7|";
            if (_workload == 1) return _fragments[0] + _fragments[1] + _fragments[2] + _fragments[3] + _fragments[4] + _fragments[5] + _fragments[6] + _fragments[7];
            string text = "";
            if (_workload == 2)
                for (int j = 0; j < _lines; j++) text += _labels[j] + ": " + _strings[j] + "\n";
            else
                for (int j = 0; j < _lines; j++) text += _labels[j] + ": " + _values[j].ToString() + "\n";
            return text;
        }

        private string PlusSteps()
        {
            string text = "";
            if (_workload == 0)
            {
                text += "Token0|";
                text += "Token1|";
                text += "Token2|";
                text += "Token3|";
                text += "Token4|";
                text += "Token5|";
                text += "Token6|";
                text += "Token7|";
            }
            else if (_workload == 1)
            {
                text += _fragments[0];
                text += _fragments[1];
                text += _fragments[2];
                text += _fragments[3];
                text += _fragments[4];
                text += _fragments[5];
                text += _fragments[6];
                text += _fragments[7];
            }
            else if (_workload == 2)
                for (int j = 0; j < _lines; j++) { text += _labels[j]; text += ": "; text += _strings[j]; text += "\n"; }
            else
                for (int j = 0; j < _lines; j++) { text += _labels[j]; text += ": "; text += _values[j].ToString(); text += "\n"; }
            return text;
        }

        private string SbNewSplit()
        {
            StringBuilder sb = new StringBuilder();
            if (_workload == 0)
            {
                sb.Append("Token0|");
                sb.Append("Token1|");
                sb.Append("Token2|");
                sb.Append("Token3|");
                sb.Append("Token4|");
                sb.Append("Token5|");
                sb.Append("Token6|");
                sb.Append("Token7|");
            }
            else if (_workload == 1)
            {
                sb.Append(_fragments[0]);
                sb.Append(_fragments[1]);
                sb.Append(_fragments[2]);
                sb.Append(_fragments[3]);
                sb.Append(_fragments[4]);
                sb.Append(_fragments[5]);
                sb.Append(_fragments[6]);
                sb.Append(_fragments[7]);
            }
            else if (_workload == 2)
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]);
                    sb.Append(": ");
                    sb.Append(_strings[j]);
                    sb.Append("\n");
                }
            }
            else
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]);
                    sb.Append(": ");
                    sb.Append(_values[j]);
                    sb.Append("\n");
                }
            }
            return sb.ToString();
        }

        private string SbNewChain()
        {
            StringBuilder sb = new StringBuilder();
            if (_workload == 0)
            {
                sb.Append("Token0|").Append("Token1|").Append("Token2|").Append("Token3|").Append("Token4|").Append("Token5|").Append("Token6|").Append("Token7|");
            }
            else if (_workload == 1)
            {
                sb.Append(_fragments[0]).Append(_fragments[1]).Append(_fragments[2]).Append(_fragments[3]).Append(_fragments[4]).Append(_fragments[5]).Append(_fragments[6]).Append(_fragments[7]);
            }
            else if (_workload == 2)
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]).Append(": ").Append(_strings[j]).Append("\n");
                }
            }
            else
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]).Append(": ").Append(_values[j]).Append("\n");
                }
            }
            return sb.ToString();
        }

        private string SbCapacitySplit()
        {
            StringBuilder sb = new StringBuilder(_capacity);
            if (_workload == 0)
            {
                sb.Append("Token0|");
                sb.Append("Token1|");
                sb.Append("Token2|");
                sb.Append("Token3|");
                sb.Append("Token4|");
                sb.Append("Token5|");
                sb.Append("Token6|");
                sb.Append("Token7|");
            }
            else if (_workload == 1)
            {
                sb.Append(_fragments[0]);
                sb.Append(_fragments[1]);
                sb.Append(_fragments[2]);
                sb.Append(_fragments[3]);
                sb.Append(_fragments[4]);
                sb.Append(_fragments[5]);
                sb.Append(_fragments[6]);
                sb.Append(_fragments[7]);
            }
            else if (_workload == 2)
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]);
                    sb.Append(": ");
                    sb.Append(_strings[j]);
                    sb.Append("\n");
                }
            }
            else
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]);
                    sb.Append(": ");
                    sb.Append(_values[j]);
                    sb.Append("\n");
                }
            }
            return sb.ToString();
        }

        private string SbCapacityChain()
        {
            StringBuilder sb = new StringBuilder(_capacity);
            if (_workload == 0)
            {
                sb.Append("Token0|").Append("Token1|").Append("Token2|").Append("Token3|").Append("Token4|").Append("Token5|").Append("Token6|").Append("Token7|");
            }
            else if (_workload == 1)
            {
                sb.Append(_fragments[0]).Append(_fragments[1]).Append(_fragments[2]).Append(_fragments[3]).Append(_fragments[4]).Append(_fragments[5]).Append(_fragments[6]).Append(_fragments[7]);
            }
            else if (_workload == 2)
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]).Append(": ").Append(_strings[j]).Append("\n");
                }
            }
            else
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]).Append(": ").Append(_values[j]).Append("\n");
                }
            }
            return sb.ToString();
        }

        private string SbReuseSplit()
        {
            StringBuilder sb = _reuse;
            sb.Clear();
            if (_workload == 0)
            {
                sb.Append("Token0|");
                sb.Append("Token1|");
                sb.Append("Token2|");
                sb.Append("Token3|");
                sb.Append("Token4|");
                sb.Append("Token5|");
                sb.Append("Token6|");
                sb.Append("Token7|");
            }
            else if (_workload == 1)
            {
                sb.Append(_fragments[0]);
                sb.Append(_fragments[1]);
                sb.Append(_fragments[2]);
                sb.Append(_fragments[3]);
                sb.Append(_fragments[4]);
                sb.Append(_fragments[5]);
                sb.Append(_fragments[6]);
                sb.Append(_fragments[7]);
            }
            else if (_workload == 2)
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]);
                    sb.Append(": ");
                    sb.Append(_strings[j]);
                    sb.Append("\n");
                }
            }
            else
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]);
                    sb.Append(": ");
                    sb.Append(_values[j]);
                    sb.Append("\n");
                }
            }
            return sb.ToString();
        }

        private string SbReuseChain()
        {
            StringBuilder sb = _reuse;
            sb.Clear();
            if (_workload == 0)
            {
                sb.Append("Token0|").Append("Token1|").Append("Token2|").Append("Token3|").Append("Token4|").Append("Token5|").Append("Token6|").Append("Token7|");
            }
            else if (_workload == 1)
            {
                sb.Append(_fragments[0]).Append(_fragments[1]).Append(_fragments[2]).Append(_fragments[3]).Append(_fragments[4]).Append(_fragments[5]).Append(_fragments[6]).Append(_fragments[7]);
            }
            else if (_workload == 2)
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]).Append(": ").Append(_strings[j]).Append("\n");
                }
            }
            else
            {
                for (int j = 0; j < _lines; j++)
                {
                    sb.Append(_labels[j]).Append(": ").Append(_values[j]).Append("\n");
                }
            }
            return sb.ToString();
        }
    }
}
