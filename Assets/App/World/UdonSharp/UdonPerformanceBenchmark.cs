namespace App
{
    using System;
    using System.Text;
    using UdonSharp;
    using UnityEngine;
    using Stopwatch = System.Diagnostics.Stopwatch;

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class UdonPerformanceBenchmark : UdonSharpBehaviour
    {
        [Header("Run settings")]
        public bool AutoRun = true;
        public int Iterations = 1000;
        public int WarmupBatches = 5;
        public int MeasuredBatches = 30;
        public int Runs = 2;

        [Header("Runtime result (not serialized)")]
        [NonSerialized] public bool IsComplete;
        [NonSerialized] public string LastStatus;
        [NonSerialized] public string LastCsv;
        [NonSerialized] public long StopwatchFrequency;
        [NonSerialized] public int LastChecksum;
        [NonSerialized] public int LastExpectedChecksum;
        [NonSerialized] public int RawRowCount;

        [NonSerialized] public int PublicField;
        [NonSerialized] public bool PublicBool;
        [NonSerialized] public int PublicIntFlag;

        private const int CaseCount = 52;
        private const int MaxRows = 52 * 2 * 30;

        private int _privateField = 7;
        private bool _privateBool = true;
        private int _privateIntFlag = 1;
        private int _instanceInput;
        private int _sink;
        private string[] _rawRows;
        private int _rawCount;
        private int _runIndex;
        private int _phaseIndex;
        private int _casePosition;
        private bool _runActive;
        private UdonPerformanceExternalTarget _externalTarget;
        private int PrivateProperty { get; set; }
        public int PublicProperty { get; set; }

        private void Start()
        {
            PublicField = 7;
            PublicBool = true;
            PublicIntFlag = 1;
            PrivateProperty = 7;
            PublicProperty = 7;
            _externalTarget = GetComponent<UdonPerformanceExternalTarget>();
            StopwatchFrequency = Stopwatch.Frequency;
            LastStatus = "ready";

            if (AutoRun)
                SendCustomEventDelayedFrames(nameof(_Begin), 2);
        }

        public void _Begin()
        {
            if (_runActive)
                return;

            if (Iterations < 1 || WarmupBatches < 0 || MeasuredBatches < 1 || Runs < 1 || Runs > 2)
            {
                LastStatus = "invalid settings";
                return;
            }

            if (_externalTarget == null)
            {
                LastStatus = "external target missing";
                return;
            }

            _rawRows = new string[MaxRows];
            _rawCount = 0;
            _runIndex = 0;
            _phaseIndex = 0;
            _casePosition = 0;
            _runActive = true;
            IsComplete = false;
            LastStatus = "running";
            SendCustomEvent(nameof(_RunNextBatch));
        }

        public void _RunNextBatch()
        {
            if (!_runActive)
                return;

            int totalPhases = WarmupBatches + MeasuredBatches;
            if (_runIndex >= Runs)
            {
                Complete();
                return;
            }

            if (_casePosition >= CaseCount)
            {
                _casePosition = 0;
                _phaseIndex++;
                if (_phaseIndex >= totalPhases)
                {
                    _phaseIndex = 0;
                    _runIndex++;
                    if (_runIndex >= Runs)
                    {
                        Complete();
                        return;
                    }
                }
            }

            int caseIndex = _runIndex == 0 ? _casePosition : CaseCount - 1 - _casePosition;
            MeasureCase(caseIndex);
            _casePosition++;
            SendCustomEventDelayedFrames(nameof(_RunNextBatch), 1);
        }

        private void MeasureCase(int caseIndex)
        {
            int phase = _phaseIndex;
            bool warmup = phase < WarmupBatches;
            long startTicks = Stopwatch.GetTimestamp();
            int checksum = RunCase(caseIndex);
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;

            if (warmup)
                return;

            int measuredBatch = phase - WarmupBatches + 1;
            int order = _runIndex == 0 ? 0 : 1;
            int expected = ExpectedChecksum(caseIndex);
            string row = string.Format(
                "{0},{1},{2},{3},{4},{5},{6},{7},{8:R},{9},{10},{11}",
                "UDON-PERF-20260925",
                _runIndex + 1,
                CaseName(caseIndex),
                order,
                measuredBatch,
                Iterations,
                elapsedTicks,
                StopwatchFrequency,
                (float)(elapsedTicks * 1000.0 / StopwatchFrequency),
                checksum,
                expected,
                checksum == expected ? 1 : 0);

            if (_rawCount < _rawRows.Length)
                _rawRows[_rawCount++] = row;

            LastChecksum = checksum;
            LastExpectedChecksum = expected;
        }

        private void Complete()
        {
            _runActive = false;
            IsComplete = true;
            RawRowCount = _rawCount;
            LastStatus = _rawCount == (Runs * MeasuredBatches * CaseCount) ? "complete" : "row capacity exceeded";

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("experiment_id,run_id,case,order,batch,iterations,elapsed_ticks,frequency,elapsed_ms,checksum,expected,checksum_ok");
            for (int i = 0; i < _rawCount; i++)
                builder.AppendLine(_rawRows[i]);
            LastCsv = builder.ToString();

            UnityEngine.Debug.Log("[UdonPerformanceBenchmark] " + LastStatus + " rows=" + _rawCount);
        }

        private int RunCase(int caseIndex)
        {
            switch (caseIndex)
            {
                case 0: return RunBaselineArg();
                case 1: return RunPrivateNoArgReturn();
                case 2: return RunPublicNoArgReturn();
                case 3: return RunPrivateArgReturn();
                case 4: return RunPublicArgReturn();
                case 5: return RunPrivateStaticNoArgReturn();
                case 6: return RunPublicStaticNoArgReturn();
                case 7: return RunPrivateStaticArgReturn();
                case 8: return RunPublicStaticArgReturn();
                case 9: return RunPrivateNoArgVoid();
                case 10: return RunPublicNoArgVoid();
                case 11: return RunPrivateArgVoid();
                case 12: return RunPublicArgVoid();
                case 13: return RunPrivateOut();
                case 14: return RunPublicOut();
                case 15: return RunPrivateRef();
                case 16: return RunPublicRef();
                case 17: return RunPrivateStaticOut();
                case 18: return RunPublicStaticOut();
                case 19: return RunPrivateStaticRef();
                case 20: return RunPublicStaticRef();
                case 21: return RunPrivateLocal0();
                case 22: return RunPrivateLocal1();
                case 23: return RunPrivateLocal4();
                case 24: return RunPrivateLocal8();
                case 25: return RunPrivateStaticLocal0();
                case 26: return RunPrivateStaticLocal1();
                case 27: return RunPrivateStaticLocal4();
                case 28: return RunPrivateStaticLocal8();
                case 29: return RunPrivateFieldRead();
                case 30: return RunPublicFieldRead();
                case 31: return RunPrivateBoolRead();
                case 32: return RunPublicBoolRead();
                case 33: return RunPrivateIntFlagRead();
                case 34: return RunPublicIntFlagRead();
                case 35: return RunPrivateFieldWrite();
                case 36: return RunPublicFieldWrite();
                case 37: return RunPrivatePropertyRead();
                case 38: return RunPublicPropertyRead();
                case 39: return RunPrivatePropertyWrite();
                case 40: return RunPublicPropertyWrite();
                case 41: return RunExternalPublicNoArgReturn();
                case 42: return RunExternalPublicNoArgVoid();
                case 43: return RunExternalPublicArgReturn();
                case 44: return RunExternalPublicArgVoid();
                case 45: return RunExternalPublicOut();
                case 46: return RunExternalPublicRef();
                case 47: return RunExternalSendCustomEvent();
                case 48: return RunExternalPublicFieldRead();
                case 49: return RunExternalPublicFieldWrite();
                case 50: return RunExternalPublicPropertyRead();
                case 51: return RunExternalPublicPropertyWrite();
                default: return 0;
            }
        }

        private int RunBaselineArg()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += i + 1;
            return _sink;
        }

        private int RunPrivateNoArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _instanceInput = i;
                _sink += PrivateNoArgReturn();
            }
            return _sink;
        }

        private int RunPublicNoArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _instanceInput = i;
                _sink += PublicNoArgReturn();
            }
            return _sink;
        }

        private int RunPrivateArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateArgReturn(i);
            return _sink;
        }

        private int RunPublicArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PublicArgReturn(i);
            return _sink;
        }

        private int RunPrivateStaticNoArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateStaticNoArgReturn();
            return _sink;
        }

        private int RunPublicStaticNoArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PublicStaticNoArgReturn();
            return _sink;
        }

        private int RunPrivateStaticArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateStaticArgReturn(i);
            return _sink;
        }

        private int RunPublicStaticArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PublicStaticArgReturn(i);
            return _sink;
        }

        private int RunPrivateNoArgVoid()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _instanceInput = i;
                PrivateNoArgVoid();
            }
            return _sink;
        }

        private int RunPublicNoArgVoid()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _instanceInput = i;
                PublicNoArgVoid();
            }
            return _sink;
        }

        private int RunPrivateArgVoid()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                PrivateArgVoid(i);
            return _sink;
        }

        private int RunPublicArgVoid()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                PublicArgVoid(i);
            return _sink;
        }

        private int RunPrivateOut()
        {
            _sink = 0;
            int output = 0;
            for (int i = 0; i < Iterations; i++)
            {
                PrivateOut(i, out output);
                _sink += output;
            }
            return _sink;
        }

        private int RunPublicOut()
        {
            _sink = 0;
            int output = 0;
            for (int i = 0; i < Iterations; i++)
            {
                PublicOut(i, out output);
                _sink += output;
            }
            return _sink;
        }

        private int RunPrivateRef()
        {
            _sink = 0;
            int value = 0;
            for (int i = 0; i < Iterations; i++)
            {
                value = i;
                PrivateRef(ref value);
                _sink += value;
            }
            return _sink;
        }

        private int RunPublicRef()
        {
            _sink = 0;
            int value = 0;
            for (int i = 0; i < Iterations; i++)
            {
                value = i;
                PublicRef(ref value);
                _sink += value;
            }
            return _sink;
        }

        private int RunPrivateStaticOut()
        {
            _sink = 0;
            int output = 0;
            for (int i = 0; i < Iterations; i++)
            {
                PrivateStaticOut(i, out output);
                _sink += output;
            }
            return _sink;
        }

        private int RunPublicStaticOut()
        {
            _sink = 0;
            int output = 0;
            for (int i = 0; i < Iterations; i++)
            {
                PublicStaticOut(i, out output);
                _sink += output;
            }
            return _sink;
        }

        private int RunPrivateStaticRef()
        {
            _sink = 0;
            int value = 0;
            for (int i = 0; i < Iterations; i++)
            {
                value = i;
                PrivateStaticRef(ref value);
                _sink += value;
            }
            return _sink;
        }

        private int RunPublicStaticRef()
        {
            _sink = 0;
            int value = 0;
            for (int i = 0; i < Iterations; i++)
            {
                value = i;
                PublicStaticRef(ref value);
                _sink += value;
            }
            return _sink;
        }

        private int RunPrivateLocal0()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateLocal0(i);
            return _sink;
        }

        private int RunPrivateLocal1()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateLocal1(i);
            return _sink;
        }

        private int RunPrivateLocal4()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateLocal4(i);
            return _sink;
        }

        private int RunPrivateLocal8()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateLocal8(i);
            return _sink;
        }

        private int RunPrivateStaticLocal0()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateStaticLocal0(i);
            return _sink;
        }

        private int RunPrivateStaticLocal1()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateStaticLocal1(i);
            return _sink;
        }

        private int RunPrivateStaticLocal4()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateStaticLocal4(i);
            return _sink;
        }

        private int RunPrivateStaticLocal8()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateStaticLocal8(i);
            return _sink;
        }

        private int RunPrivateFieldRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateFieldRead(i);
            return _sink;
        }

        private int RunPublicFieldRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PublicFieldRead(i);
            return _sink;
        }

        private int RunPrivateBoolRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateBoolRead(i);
            return _sink;
        }

        private int RunPublicBoolRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PublicBoolRead(i);
            return _sink;
        }

        private int RunPrivateIntFlagRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PrivateIntFlagRead(i);
            return _sink;
        }

        private int RunPublicIntFlagRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += PublicIntFlagRead(i);
            return _sink;
        }

        private int RunPrivateFieldWrite()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _privateField = i;
                _sink += i + 1;
            }
            return _sink;
        }

        private int RunPublicFieldWrite()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                PublicField = i;
                _sink += i + 1;
            }
            return _sink;
        }

        private int RunPrivatePropertyRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += i + PrivateProperty;
            return _sink;
        }

        private int RunPublicPropertyRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += i + PublicProperty;
            return _sink;
        }

        private int RunPrivatePropertyWrite()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                PrivateProperty = i;
                _sink += i + 1;
            }
            return _sink;
        }

        private int RunPublicPropertyWrite()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                PublicProperty = i;
                _sink += i + 1;
            }
            return _sink;
        }

        private int RunExternalPublicNoArgReturn()
        {
            _externalTarget.ResetState();
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += _externalTarget.PublicNoArgReturn();
            return _sink;
        }

        private int RunExternalPublicNoArgVoid()
        {
            _externalTarget.ResetState();
            for (int i = 0; i < Iterations; i++)
                _externalTarget.PublicNoArgVoid();
            return _externalTarget.Sink;
        }

        private int RunExternalPublicArgReturn()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += _externalTarget.PublicArgReturn(i);
            return _sink;
        }

        private int RunExternalPublicArgVoid()
        {
            _externalTarget.ResetState();
            for (int i = 0; i < Iterations; i++)
                _externalTarget.PublicArgVoid(i);
            return _externalTarget.Sink;
        }

        private int RunExternalPublicOut()
        {
            _sink = 0;
            int output = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _externalTarget.PublicOut(i, out output);
                _sink += output;
            }
            return _sink;
        }

        private int RunExternalPublicRef()
        {
            _sink = 0;
            int value = 0;
            for (int i = 0; i < Iterations; i++)
            {
                value = i;
                _externalTarget.PublicRef(ref value);
                _sink += value;
            }
            return _sink;
        }

        private int RunExternalSendCustomEvent()
        {
            _externalTarget.ResetState();
            for (int i = 0; i < Iterations; i++)
                _externalTarget.SendCustomEvent(nameof(UdonPerformanceExternalTarget.PublicEventNoArg));
            return _externalTarget.EventSink;
        }

        private int RunExternalPublicFieldRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += _externalTarget.PublicField;
            return _sink;
        }

        private int RunExternalPublicFieldWrite()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _externalTarget.PublicField = i;
                _sink += i + 1;
            }
            return _sink;
        }

        private int RunExternalPublicPropertyRead()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
                _sink += _externalTarget.PublicProperty;
            return _sink;
        }

        private int RunExternalPublicPropertyWrite()
        {
            _sink = 0;
            for (int i = 0; i < Iterations; i++)
            {
                _externalTarget.PublicProperty = i;
                _sink += i + 1;
            }
            return _sink;
        }

        private int PrivateNoArgReturn() => _instanceInput + 1;
        public int PublicNoArgReturn() => _instanceInput + 1;
        private int PrivateArgReturn(int value) => value + 1;
        public int PublicArgReturn(int value) => value + 1;
        private static int PrivateStaticNoArgReturn() => 1;
        public static int PublicStaticNoArgReturn() => 1;
        private static int PrivateStaticArgReturn(int value) => value + 1;
        public static int PublicStaticArgReturn(int value) => value + 1;

        private void PrivateNoArgVoid() { _sink += _instanceInput + 1; }
        public void PublicNoArgVoid() { _sink += _instanceInput + 1; }
        private void PrivateArgVoid(int value) { _sink += value + 1; }
        public void PublicArgVoid(int value) { _sink += value + 1; }
        private void PrivateOut(int value, out int output) { output = value + 1; }
        public void PublicOut(int value, out int output) { output = value + 1; }
        private void PrivateRef(ref int value) { value += 1; }
        public void PublicRef(ref int value) { value += 1; }
        private static void PrivateStaticOut(int value, out int output) { output = value + 1; }
        public static void PublicStaticOut(int value, out int output) { output = value + 1; }
        private static void PrivateStaticRef(ref int value) { value += 1; }
        public static void PublicStaticRef(ref int value) { value += 1; }

        private int PrivateLocal0(int value) => value + 1;

        private int PrivateLocal1(int value)
        {
            int a = value + 1;
            return a;
        }

        private int PrivateLocal4(int value)
        {
            int a = value;
            int b = a + 1;
            int c = b;
            int d = c;
            return d;
        }

        private int PrivateLocal8(int value)
        {
            int a = value;
            int b = a;
            int c = b;
            int d = c;
            int e = d + 1;
            int f = e;
            int g = f;
            int h = g;
            return h;
        }

        private static int PrivateStaticLocal0(int value) => value + 1;

        private static int PrivateStaticLocal1(int value)
        {
            int a = value + 1;
            return a;
        }

        private static int PrivateStaticLocal4(int value)
        {
            int a = value;
            int b = a + 1;
            int c = b;
            int d = c;
            return d;
        }

        private static int PrivateStaticLocal8(int value)
        {
            int a = value;
            int b = a;
            int c = b;
            int d = c;
            int e = d + 1;
            int f = e;
            int g = f;
            int h = g;
            return h;
        }

        private int PrivateFieldRead(int value) => value + _privateField;
        private int PublicFieldRead(int value) => value + PublicField;
        private int PrivateBoolRead(int value) => _privateBool ? value + 1 : value;
        private int PublicBoolRead(int value) => PublicBool ? value + 1 : value;
        private int PrivateIntFlagRead(int value) => _privateIntFlag != 0 ? value + 1 : value;
        private int PublicIntFlagRead(int value) => PublicIntFlag != 0 ? value + 1 : value;

        private int ExpectedChecksum(int caseIndex)
        {
            int sum = Iterations * (Iterations - 1) / 2 + Iterations;
            switch (caseIndex)
            {
                case 5: return Iterations;
                case 6: return Iterations;
                case 29: return Iterations * (Iterations - 1) / 2 + Iterations * _privateField;
                case 30: return Iterations * (Iterations - 1) / 2 + Iterations * PublicField;
                case 37: return Iterations * (Iterations - 1) / 2 + Iterations * PrivateProperty;
                case 38: return Iterations * (Iterations - 1) / 2 + Iterations * PublicProperty;
                case 48: return Iterations * _externalTarget.PublicField;
                case 50: return Iterations * _externalTarget.PublicProperty;
                default: return sum;
            }
        }

        private string CaseName(int caseIndex)
        {
            switch (caseIndex)
            {
                case 0: return "baseline_arg";
                case 1: return "private_noarg_return";
                case 2: return "public_noarg_return";
                case 3: return "private_arg_return";
                case 4: return "public_arg_return";
                case 5: return "private_static_noarg_return";
                case 6: return "public_static_noarg_return";
                case 7: return "private_static_arg_return";
                case 8: return "public_static_arg_return";
                case 9: return "private_noarg_void";
                case 10: return "public_noarg_void";
                case 11: return "private_arg_void";
                case 12: return "public_arg_void";
                case 13: return "private_out";
                case 14: return "public_out";
                case 15: return "private_ref";
                case 16: return "public_ref";
                case 17: return "private_static_out";
                case 18: return "public_static_out";
                case 19: return "private_static_ref";
                case 20: return "public_static_ref";
                case 21: return "private_local_0";
                case 22: return "private_local_1";
                case 23: return "private_local_4";
                case 24: return "private_local_8";
                case 25: return "private_static_local_0";
                case 26: return "private_static_local_1";
                case 27: return "private_static_local_4";
                case 28: return "private_static_local_8";
                case 29: return "private_field_read";
                case 30: return "public_field_read";
                case 31: return "private_bool_read";
                case 32: return "public_bool_read";
                case 33: return "private_int_flag_read";
                case 34: return "public_int_flag_read";
                case 35: return "private_field_write";
                case 36: return "public_field_write";
                case 37: return "private_property_read";
                case 38: return "public_property_read";
                case 39: return "private_property_write";
                case 40: return "public_property_write";
                case 41: return "external_public_noarg_return";
                case 42: return "external_public_noarg_void";
                case 43: return "external_public_arg_return";
                case 44: return "external_public_arg_void";
                case 45: return "external_public_out";
                case 46: return "external_public_ref";
                case 47: return "external_send_custom_event";
                case 48: return "external_public_field_read";
                case 49: return "external_public_field_write";
                case 50: return "external_public_property_read";
                case 51: return "external_public_property_write";
                default: return "unknown";
            }
        }
    }
}
