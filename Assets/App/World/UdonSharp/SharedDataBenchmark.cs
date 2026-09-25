using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class SharedDataBenchmark : UdonSharpBehaviour
    {
        public SharedDataTarget Target;
        public bool AutoRun = true;
        public int Warmups = 5;
        public int Batches = 30;
        [NonSerialized] public string LastCsv;
        [NonSerialized] public string Status = "ready";
        [NonSerialized] public bool Complete;
        [NonSerialized] public int Failures;
        private int[] _buffer;
        private DataList _list;
        private int[] _bulk;
        private DataDictionary _strings, _ints, _ids, _built;
        private string[] _stringKeys, _generatedStrings;
        private DataToken[] _stringTokens, _intTokens;
        private int[] _generatedInts;
        private int _n, _entries, _mask, _config, _run, _phase, _position;
        private bool _active;
        private StringBuilder _csv;
        private string[] _rows;
        private int _rowCount, _exportRow;
        private long _frequency;
        private string _environment;
        private int _expectedDict, _expectedLength;
        private const int CaseCount = 31;
        private string[] _names = new string[] { "clock", "comm_inline", "comm_argument_return", "comm_fields_direct", "comm_fields_event", "comm_array_direct", "comm_array_event", "comm_list_direct", "comm_list_event", "comm_array_argument", "comm_list_argument", "comm_cached_array_write", "comm_external_field_write", "comm_cached_list_write", "comm_array_batch", "dict_baseline", "dict_string_get", "dict_int_get", "dict_string_token_get", "dict_int_token_get", "dict_string_update", "dict_int_update", "dict_string_build", "dict_int_build", "key_string_generate", "key_int_generate", "dict_string_generate_get", "dict_string_to_id_get", "dict_string_tryget", "dict_int_tryget", "dict_build_baseline" };

        private void Start()
        {
            if (AutoRun) SendCustomEventDelayedFrames(nameof(_Begin), 120);
        }

        public void _Begin()
        {
            if (_active || Target == null) return;
            if (Batches < 1 || Batches > 60 || Warmups < 0 || Warmups > 10)
            { Status = "invalid settings"; return; }
            _frequency = Stopwatch.Frequency;
#if UNITY_EDITOR
            _environment = "Editor-UdonVM";
#else
            _environment = "VRChat-client";
#endif
            _rows = new string[4 * 2 * Batches * CaseCount];
            _rowCount = 0;
            _csv = new StringBuilder();
            _csv.AppendLine("experiment_id,environment,config,run,case,order,batch,iterations,entries,operations,elapsed_ticks,frequency,checksum,expected,checksum_ok");
            _config = 0; _run = 0; _phase = 0; _position = 0;
            Failures = 0; Complete = false; _active = true;
            Setup();
            SendCustomEventDelayedFrames(nameof(_Next), 1);
        }

        private void Setup()
        {
            _n = (_config & 1) == 0 ? 256 : 512;
            _entries = _config < 2 ? 128 : 1024;
            _mask = _entries - 1;
            _buffer = new int[2];
            _list = new DataList(); _list.Add(0); _list.Add(0);
            Target.Buffer = _buffer; Target.List = _list;
            // Two-way mutation check proves aliasing across this behaviour boundary.
            _buffer[0] = 17; Target._Array();
            _list[0] = 19; Target._List();
            if (_buffer[1] != 18 || _list[1].Int != 20)
            { Failures++; Status = "shared reference check failed"; _active = false; return; }
            _bulk = new int[_n];
            _strings = new DataDictionary(); _ints = new DataDictionary(); _ids = new DataDictionary();
            _stringKeys = new string[_entries];
            _stringTokens = new DataToken[_entries]; _intTokens = new DataToken[_entries];
            for (int i = 0; i < _entries; i++)
            {
                string key = "translation.menu.entry." + i.ToString();
                _stringKeys[i] = key; _stringTokens[i] = key; _intTokens[i] = i;
                _strings.Add(key, i + 1); _ints.Add(i, i + 1); _ids.Add(key, i);
            }
            _generatedStrings = new string[_n]; _generatedInts = new int[_n];
            _expectedDict = 0; _expectedLength = 0;
            for (int i = 0; i < _n; i++)
            { _expectedDict += (i & _mask) + 1; _expectedLength += _stringKeys[i & _mask].Length; }
            Debug.Log("[SharedData] setup config=" + _config + " alias_ok=1 N=" + _n + " entries=" + _entries
                + " frequency=" + _frequency + " highResolution=" + Stopwatch.IsHighResolution + " environment=" + _environment);
        }

        public void _Next()
        {
            if (!_active) return;
            int c = ((_phase + _run) & 1) == 0 ? _position : CaseCount - 1 - _position;
            // Allocation and reset are outside steady-state timings. Build includes constructor separately below.
            Target.Buffer = _buffer;
            if (c == 14)
            {
                Target.Buffer = _bulk;
            }
            long start = Stopwatch.GetTimestamp();
            int sum = Run(c);
            long elapsed = Stopwatch.GetTimestamp() - start;
            int expected = Expected(c);
            bool ok = sum == expected;
            if (c == 11) ok = ok && _buffer[0] == _n - 1;
            if (c == 12) ok = ok && Target.Input == _n - 1;
            if (c == 13) ok = ok && _list[0].Int == _n - 1;
            if (c == 24 || c == 25)
                for (int i = 0; i < _n; i++)
                    ok = ok && (c == 24 ? _generatedStrings[i] == _stringKeys[i & _mask] : _generatedInts[i] == (i & _mask));
            if (c == 22 || c == 23)
            {
                ok = ok && _built.Count == _entries;
                for (int i = 0; i < _entries; i++)
                {
                    DataToken key = c == 22 ? _stringTokens[i] : _intTokens[i];
                    ok = ok && _built[key].Int == i + 1;
                }
            }
            if (c == 20 || c == 21)
            {
                for (int i = 0; i < _entries; i++)
                {
                    int changed = i < _n ? i + 2 : i + 1;
                    ok = ok && (c == 20 ? _strings[_stringKeys[i]].Int : _ints[i].Int) == changed;
                    _strings[_stringKeys[i]] = i + 1; _ints[i] = i + 1;
                }
            }
            if (!ok) Failures++;
            if (_phase >= Warmups)
            {
                string row = "UDON-SHARED-KEY-20260925," + _environment + "," + _config + "," + _run + "," + _names[c]
                    + "," + _position + "," + (_phase - Warmups) + "," + _n + "," + _entries + ","
                    + (c == 0 ? 1 : ((c == 22 || c == 23 || c == 30) ? _entries : _n)) + "," + elapsed + "," + _frequency + "," + sum + "," + expected + "," + (ok ? 1 : 0);
                _rows[_rowCount++] = row; _csv.AppendLine(row);
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
                        if (_config == 4)
                        {
                            _active = false; Complete = true; LastCsv = _csv.ToString();
                            Status = Failures == 0 ? "complete" : "CHECKSUM FAILURE";
                            Debug.Log("[SharedData] " + Status + " rows=" + _rowCount);
                            _exportRow = 0; SendCustomEventDelayedFrames(nameof(_Export), 1); return;
                        }
                        Setup();
                    }
                }
            }
            Status = "config=" + _config + " run=" + _run + " phase=" + _phase + " rows=" + _rowCount;
            SendCustomEventDelayedFrames(nameof(_Next), 1);
        }

        public void _Export()
        {
            // Export only after all timing: one bounded line per frame for the client log.
            if (_exportRow >= _rowCount) { Debug.Log("[SharedData] EXPORT_END rows=" + _rowCount); return; }
            Debug.Log("[SharedDataCSV] " + _rows[_exportRow++]);
            SendCustomEventDelayedFrames(nameof(_Export), 1);
        }

        private int Run(int c)
        {
            int sum = 0;
            switch (c)
            {
                case 0: // clock
                    return 0;
                case 1: // comm_inline
                    for (int i = 0; i < _n; i++) { sum += i + 1; }
                    break;
                case 2: // comm_argument_return
                    for (int i = 0; i < _n; i++) { sum += Target._Argument(i); }
                    break;
                case 3: // comm_fields_direct
                    for (int i = 0; i < _n; i++) { Target.Input = i; Target._Fields(); sum += Target.Output; }
                    break;
                case 4: // comm_fields_event
                    for (int i = 0; i < _n; i++) { Target.Input = i; Target.SendCustomEvent(nameof(SharedDataTarget._Fields)); sum += Target.Output; }
                    break;
                case 5: // comm_array_direct
                    for (int i = 0; i < _n; i++) { _buffer[0] = i; Target._Array(); sum += _buffer[1]; }
                    break;
                case 6: // comm_array_event
                    for (int i = 0; i < _n; i++) { _buffer[0] = i; Target.SendCustomEvent(nameof(SharedDataTarget._Array)); sum += _buffer[1]; }
                    break;
                case 7: // comm_list_direct
                    for (int i = 0; i < _n; i++) { _list[0] = i; Target._List(); sum += _list[1].Int; }
                    break;
                case 8: // comm_list_event
                    for (int i = 0; i < _n; i++) { _list[0] = i; Target.SendCustomEvent(nameof(SharedDataTarget._List)); sum += _list[1].Int; }
                    break;
                case 9: // comm_array_argument
                    for (int i = 0; i < _n; i++) { _buffer[0] = i; Target._ArrayArgument(_buffer); sum += _buffer[1]; }
                    break;
                case 10: // comm_list_argument
                    for (int i = 0; i < _n; i++) { _list[0] = i; Target._ListArgument(_list); sum += _list[1].Int; }
                    break;
                case 11: // comm_cached_array_write
                    for (int i = 0; i < _n; i++) { _buffer[0] = i; sum += i + 1; }
                    break;
                case 12: // comm_external_field_write
                    for (int i = 0; i < _n; i++) { Target.Input = i; sum += i + 1; }
                    break;
                case 13: // comm_cached_list_write
                    for (int i = 0; i < _n; i++) { _list[0] = i; sum += i + 1; }
                    break;
                case 14: // comm_array_batch
                    for (int i = 0; i < _n; i++) _bulk[i] = i;
                    Target._ArrayBatch();
                    for (int i = 0; i < _n; i++) sum += _bulk[i];
                    break;
                case 15: // dict_baseline
                    for (int i = 0; i < _n; i++) { sum += (i & _mask) + 1; }
                    break;
                case 16: // dict_string_get
                    for (int i = 0; i < _n; i++) { sum += _strings[_stringKeys[i & _mask]].Int; }
                    break;
                case 17: // dict_int_get
                    for (int i = 0; i < _n; i++) { sum += _ints[i & _mask].Int; }
                    break;
                case 18: // dict_string_token_get
                    for (int i = 0; i < _n; i++) { sum += _strings[_stringTokens[i & _mask]].Int; }
                    break;
                case 19: // dict_int_token_get
                    for (int i = 0; i < _n; i++) { sum += _ints[_intTokens[i & _mask]].Int; }
                    break;
                case 20: // dict_string_update
                    for (int i = 0; i < _n; i++) { _strings[_stringKeys[i & _mask]] = (i & _mask) + 2; sum += (i & _mask) + 1; }
                    break;
                case 21: // dict_int_update
                    for (int i = 0; i < _n; i++) { _ints[i & _mask] = (i & _mask) + 2; sum += (i & _mask) + 1; }
                    break;
                case 22: // dict_string_build
                    _built = new DataDictionary();
                    for (int i = 0; i < _entries; i++) { _built.Add(_stringKeys[i], i + 1); sum += i + 1; }
                    break;
                case 23: // dict_int_build
                    _built = new DataDictionary();
                    for (int i = 0; i < _entries; i++) { _built.Add(i, i + 1); sum += i + 1; }
                    break;
                case 24: // key_string_generate
                    for (int i = 0; i < _n; i++) { _generatedStrings[i] = "translation.menu.entry." + (i & _mask).ToString(); sum += _generatedStrings[i].Length; }
                    break;
                case 25: // key_int_generate
                    for (int i = 0; i < _n; i++) { _generatedInts[i] = i & _mask; sum += _generatedInts[i] + 1; }
                    break;
                case 26: // dict_string_generate_get
                    for (int i = 0; i < _n; i++) { sum += _strings["translation.menu.entry." + (i & _mask).ToString()].Int; }
                    break;
                case 27: // dict_string_to_id_get
                    for (int i = 0; i < _n; i++) { sum += _ints[_ids[_stringKeys[i & _mask]].Int].Int; }
                    break;
                case 28: // dict_string_tryget
                    for (int i = 0; i < _n; i++) { DataToken value; _strings.TryGetValue(_stringKeys[i & _mask], out value); sum += value.Int; }
                    break;
                case 30: // dict_build_baseline
                    for (int i = 0; i < _entries; i++) sum += i + 1;
                    break;
                case 29: // dict_int_tryget
                    for (int i = 0; i < _n; i++) { DataToken value; _ints.TryGetValue(i & _mask, out value); sum += value.Int; }
                    break;
            }
            return sum;
        }

        private int Expected(int c)
        {
            if (c == 0) return 0;
            if (c < 15) return _n * (_n + 1) / 2;
            if (c == 22 || c == 23 || c == 30) return _entries * (_entries + 1) / 2;
            if (c == 24) return _expectedLength;
            return _expectedDict;
        }
    }
}
