using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InterUdonDataBenchmark : UdonSharpBehaviour
    {
        public InterUdonDataTarget Target;
        public bool AutoRun = true;
        public int Warmups = 5;
        public int Batches = 30;
        [NonSerialized] public bool Complete, AliasPassed;
        [NonSerialized] public int Failures, Rows;
        [NonSerialized] public string LastCsv, Status = "ready";
        private int[] _state, _localArray, _call, _batch, _acquiredArray;
        private DataList _list, _localList, _callList, _acquiredList;
        private int _hp, _money, _n, _config, _run, _phase, _position, _exportRow;
        private int HpProperty { get; set; }
        private int MoneyProperty { get; set; }
        private bool _active;
        private long _frequency;
        private StringBuilder _csv;
        private string[] _rows;
        private string _environment;
        private const int CaseCount = 32;
        private string[] _names = new string[] {
            "clock", "state_inline", "state_local_fields", "state_local_property", "state_local_methods",
            "state_local_array", "state_local_list", "state_external_fields", "state_external_property", "state_external_methods",
            "state_shared_array", "state_shared_list", "state_refetch_array", "state_refetch_list",
            "call_inline", "call_local_method", "call_argument_return", "call_fields", "call_array", "call_list",
            "call_array_event", "call_list_event", "call_array_argument", "call_list_argument", "call_array_batch",
            "setup_attach_array", "setup_attach_list", "setup_acquire_array", "setup_acquire_list",
            "setup_allocate_array", "setup_allocate_list", "setup_baseline" };

        private void Start() { if (AutoRun) SendCustomEventDelayedFrames(nameof(_Begin), 120); }

        public void _Begin()
        {
            if (_active || Target == null || Batches < 1 || Batches > 60 || Warmups < 0 || Warmups > 10) return;
            _frequency = Stopwatch.Frequency;
#if UNITY_EDITOR
            _environment = "Editor-UdonVM";
#else
            _environment = "VRChat-client";
#endif
            _csv = new StringBuilder();
            _csv.AppendLine("experiment_id,environment,config,run,case,order,batch,iterations,elapsed_ticks,frequency,checksum,expected,checksum_ok");
            _rows = new string[3 * 2 * Batches * CaseCount];
            Rows = 0; Failures = 0; Complete = false; AliasPassed = false;
            _config = 0; _run = 0; _phase = 0; _position = 0; _active = true;
            Setup();
            SendCustomEventDelayedFrames(nameof(_Next), 1);
        }

        private void Setup()
        {
            _n = _config == 0 ? 1 : (_config == 1 ? 128 : 512);
            _state = new int[2]; _localArray = new int[2]; _call = new int[3]; _batch = new int[_n * 3];
            _list = new DataList(); _list.Add(0); _list.Add(0);
            _localList = new DataList(); _localList.Add(0); _localList.Add(0);
            _callList = new DataList(); _callList.Add(0); _callList.Add(0); _callList.Add(0);
            Target.StateArray = _state; Target.StateList = _list;
            Target.CallArray = _call; Target.CallList = _callList; Target.BatchArray = _batch;
            _state[0] = 17; _state[1] = 23; _list[0] = 17; _list[1] = 23;
            bool peerRead = Target._ReadArray() == 40 && Target._ReadList() == 40;
            Target._MutateState();
            AliasPassed = peerRead && _state[0] == 24 && _state[1] == 34 && _list[0].Int == 24 && _list[1].Int == 34;
            if (!AliasPassed) { Failures++; _active = false; Status = "ALIAS FAILURE"; }
            Debug.Log("[InterUdon] alias=" + AliasPassed + " config=" + _config + " N=" + _n
                + " frequency=" + _frequency + " highResolution=" + Stopwatch.IsHighResolution + " environment=" + _environment);
        }

        public void _Next()
        {
            if (!_active) return;
            int c = ((_phase + _run) & 1) == 0 ? _position : CaseCount - 1 - _position;
            // Restore shared identities outside timing, including after allocation-only cases.
            Target.StateArray = _state; Target.StateList = _list;
            long start = Stopwatch.GetTimestamp();
            int sum = Run(c);
            long ticks = Stopwatch.GetTimestamp() - start;
            int expected = c == 0 ? 0 : (c >= 25 ? _n : (c < 14 ? _n * _n : 2 * _n * _n - _n));
            bool ok = sum == expected;
            if (c == 7 || c == 9) ok = ok && Target.Hp == _n - 1 && Target.Money == _n;
            if (c == 8) ok = ok && Target.HpProperty == _n - 1 && Target.MoneyProperty == _n;
            if (c == 10 || c == 12) ok = ok && Target._ReadArray() == 2 * _n - 1;
            if (c == 11 || c == 13) ok = ok && Target._ReadList() == 2 * _n - 1;
            if (c == 25) ok = ok && Target._ReadArray() == _state[0] + _state[1];
            if (c == 26) ok = ok && Target._ReadList() == _list[0].Int + _list[1].Int;
            if (c == 27) { _acquiredArray[0] = 41; ok = ok && Target._ReadArray() == 41 + _state[1]; }
            if (c == 28) { _acquiredList[0] = 41; ok = ok && Target._ReadList() == 41 + _list[1].Int; }
            if (c == 29) ok = ok && _acquiredArray.Length == 3;
            if (c == 30) ok = ok && _acquiredList.Count == 3 && _acquiredList[2].Int == 0;
            if (!ok) Failures++;
            if (_phase >= Warmups)
            {
                string row = "UDON-INTERDATA-T174," + _environment + "," + _config + "," + _run + "," + _names[c]
                    + "," + _position + "," + (_phase - Warmups) + "," + _n + "," + ticks + "," + _frequency
                    + "," + sum + "," + expected + "," + (ok ? 1 : 0);
                _rows[Rows++] = row; _csv.AppendLine(row);
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
                        if (_config == 3)
                        {
                            _active = false; Complete = true; LastCsv = _csv.ToString();
                            Status = Failures == 0 ? "complete" : "CHECKSUM FAILURE";
                            Debug.Log("[InterUdon] " + Status + " rows=" + Rows + " failures=" + Failures);
                            _exportRow = 0;
                            // Editor exports directly from the live Udon heap; client logs after timing.
#if !UNITY_EDITOR
                            SendCustomEventDelayedFrames(nameof(_Export), 1);
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

        public void _Export()
        {
            if (_exportRow >= Rows) { Debug.Log("[InterUdon] EXPORT_END rows=" + Rows); return; }
            Debug.Log("[InterUdonCSV] " + _rows[_exportRow++]);
            SendCustomEventDelayedFrames(nameof(_Export), 1);
        }

        private void SetHp(int value) { _hp = value; }
        private void SetMoney(int value) { _money = value; }
        private int GetHp() { return _hp; }
        private int GetMoney() { return _money; }
        private int Calculate(int hp, int money) { return hp * 3 + money; }

        private int Run(int c)
        {
            int sum = 0;
            switch (c)
            {
                case 0: return 0;
                case 1:
                    for (int i = 0; i < _n; i++) { int hp = i; int money = i + 1; sum += hp + money; } break;
                case 2:
                    for (int i = 0; i < _n; i++) { _hp = i; _money = i + 1; sum += _hp + _money; } break;
                case 3:
                    for (int i = 0; i < _n; i++) { HpProperty = i; MoneyProperty = i + 1; sum += HpProperty + MoneyProperty; } break;
                case 4:
                    for (int i = 0; i < _n; i++) { SetHp(i); SetMoney(i + 1); sum += GetHp() + GetMoney(); } break;
                case 5:
                    for (int i = 0; i < _n; i++) { _localArray[0] = i; _localArray[1] = i + 1; sum += _localArray[0] + _localArray[1]; } break;
                case 6:
                    for (int i = 0; i < _n; i++) { _localList[0] = i; _localList[1] = i + 1; sum += _localList[0].Int + _localList[1].Int; } break;
                case 7:
                    for (int i = 0; i < _n; i++) { Target.Hp = i; Target.Money = i + 1; sum += Target.Hp + Target.Money; } break;
                case 8:
                    for (int i = 0; i < _n; i++) { Target.HpProperty = i; Target.MoneyProperty = i + 1; sum += Target.HpProperty + Target.MoneyProperty; } break;
                case 9:
                    for (int i = 0; i < _n; i++) { Target._SetHp(i); Target._SetMoney(i + 1); sum += Target._GetHp() + Target._GetMoney(); } break;
                case 10:
                    for (int i = 0; i < _n; i++) { _state[0] = i; _state[1] = i + 1; sum += _state[0] + _state[1]; } break;
                case 11:
                    for (int i = 0; i < _n; i++) { _list[0] = i; _list[1] = i + 1; sum += _list[0].Int + _list[1].Int; } break;
                case 12:
                    for (int i = 0; i < _n; i++) { Target.StateArray[0] = i; Target.StateArray[1] = i + 1; sum += Target.StateArray[0] + Target.StateArray[1]; } break;
                case 13:
                    for (int i = 0; i < _n; i++) { Target.StateList[0] = i; Target.StateList[1] = i + 1; sum += Target.StateList[0].Int + Target.StateList[1].Int; } break;
                case 14:
                    for (int i = 0; i < _n; i++) sum += i * 3 + i + 1; break;
                case 15:
                    for (int i = 0; i < _n; i++) sum += Calculate(i, i + 1); break;
                case 16:
                    for (int i = 0; i < _n; i++) sum += Target._Calculate(i, i + 1); break;
                case 17:
                    for (int i = 0; i < _n; i++) { Target.Hp = i; Target.Money = i + 1; Target._Fields(); sum += Target.Result; } break;
                case 18:
                    for (int i = 0; i < _n; i++) { _call[0] = i; _call[1] = i + 1; Target._Array(); sum += _call[2]; } break;
                case 19:
                    for (int i = 0; i < _n; i++) { _callList[0] = i; _callList[1] = i + 1; Target._List(); sum += _callList[2].Int; } break;
                case 20:
                    for (int i = 0; i < _n; i++) { _call[0] = i; _call[1] = i + 1; Target.SendCustomEvent(nameof(InterUdonDataTarget._Array)); sum += _call[2]; } break;
                case 21:
                    for (int i = 0; i < _n; i++) { _callList[0] = i; _callList[1] = i + 1; Target.SendCustomEvent(nameof(InterUdonDataTarget._List)); sum += _callList[2].Int; } break;
                case 22:
                    for (int i = 0; i < _n; i++) { _call[0] = i; _call[1] = i + 1; Target._ArrayArgument(_call); sum += _call[2]; } break;
                case 23:
                    for (int i = 0; i < _n; i++) { _callList[0] = i; _callList[1] = i + 1; Target._ListArgument(_callList); sum += _callList[2].Int; } break;
                case 24:
                    for (int i = 0; i < _n; i++) { _batch[i] = i; _batch[_n + i] = i + 1; }
                    Target._Batch();
                    for (int i = 0; i < _n; i++) sum += _batch[_n * 2 + i]; break;
                case 25:
                    for (int i = 0; i < _n; i++) { Target.StateArray = _state; sum += 1; } break;
                case 26:
                    for (int i = 0; i < _n; i++) { Target.StateList = _list; sum += 1; } break;
                case 27:
                    for (int i = 0; i < _n; i++) { _acquiredArray = Target.StateArray; sum += 1; } break;
                case 28:
                    for (int i = 0; i < _n; i++) { _acquiredList = Target.StateList; sum += 1; } break;
                case 29:
                    for (int i = 0; i < _n; i++) { _acquiredArray = new int[3]; sum += 1; } break;
                case 30:
                    for (int i = 0; i < _n; i++) { _acquiredList = new DataList(); _acquiredList.Add(0); _acquiredList.Add(0); _acquiredList.Add(0); sum += 1; } break;
                case 31:
                    for (int i = 0; i < _n; i++) sum += 1; break;
            }
            return sum;
        }
    }
}
