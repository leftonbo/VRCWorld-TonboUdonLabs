using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace App
{
    // Shared observation code adds no synced fields to the measured schemas.
    public abstract class PersistenceProbe : UdonSharpBehaviour
    {
        public PersistenceLab Lab;
        public PersistenceBatchLab BatchLab;
        public int CaseIndex;
        public int IntCount;
        public bool ArrayMode;
        // Unsynced type selector; _TypeName maps each kind to its type.
        public int ValueKind;
        public string CustomStringPattern = "";
        // VRCUrl cannot be constructed in Udon; the Editor bakes independent expected URL objects.
        public VRCUrl[] UrlPattern = new VRCUrl[0];
        public VRCUrl EmptyUrl = new VRCUrl("");
        public int SceneNetworkId;
        [System.NonSerialized] public bool Restored;
        [System.NonSerialized] public int Requests;
        [System.NonSerialized] public int PreSerializations;
        [System.NonSerialized] public int PostSerializations;
        [System.NonSerialized] public bool LastSuccess;
        [System.NonSerialized] public int LastBytes;
        [System.NonSerialized] public float LastRequestTime = -1;
        [System.NonSerialized] public float LastPostTime = -1;

        private void Start() { Observe("start-before-restored", ""); }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !Networking.IsOwner(player, gameObject)) return;
            Restored = true;
            if (player.isLocal)
            {
                if (Utilities.IsValid(BatchLab)) BatchLab._Bind(this);
                else if (Utilities.IsValid(Lab)) Lab._Bind(this);
            }
            Observe("player-restored", "");
        }

        public override void OnDeserialization(DeserializationResult result)
        {
            Observe("deserialization", "fromStorage=" + result.isFromStorage
                + "|sendTime=" + result.sendTime + "|receiveTime=" + result.receiveTime);
        }

        public override void OnPreSerialization()
        {
            PreSerializations++;
            Observe("pre-serialization", "");
        }

        public override void OnPostSerialization(SerializationResult result)
        {
            PostSerializations++;
            LastSuccess = result.success;
            LastBytes = result.byteCount;
            LastPostTime = Time.realtimeSinceStartup;
            Observe("post-serialization", "success=" + result.success + "|bytes=" + result.byteCount);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Observe("ownership", "");
        }

        public object _ReadRaw(int index)
        {
            if (index < 0 || index >= IntCount) return null;
            if (!ArrayMode) return GetProgramVariable("V" + (index + 1).ToString("D3"));
            object raw = GetProgramVariable("Values");
            if (raw == null) return null;
            if (ValueKind == 1)
            {
                long[] longs = (long[])raw;
                return index < longs.Length ? (object)longs[index] : null;
            }
            int[] values = (int[])raw;
            if (index >= values.Length) return null;
            return values[index];
        }

        public string _Shape()
        {
            if (!ArrayMode) return "scalar";
            object raw = GetProgramVariable("Values");
            if (raw == null) return "array-null";
            return "array-length=" + (ValueKind == 1 ? ((long[])raw).Length : ((int[])raw).Length);
        }

        public bool _CanWrite()
        {
            return Restored && Utilities.IsValid(Networking.LocalPlayer) && Networking.IsOwner(gameObject);
        }

        public string _TypeName()
        {
            if (ValueKind == 1) return "long";
            if (ValueKind == 2) return "float";
            if (ValueKind == 3) return "double";
            if (ValueKind == 4) return "bool";
            if (ValueKind == 5) return "string";
            if (ValueKind == 6) return "short";
            if (ValueKind == 7) return "byte";
            if (ValueKind == 8) return "sbyte";
            if (ValueKind == 9) return "ushort";
            if (ValueKind == 10) return "uint";
            if (ValueKind == 11) return "ulong";
            if (ValueKind == 12) return "char";
            if (ValueKind == 13) return "Vector2";
            if (ValueKind == 14) return "Vector3";
            if (ValueKind == 15) return "Vector4";
            if (ValueKind == 16) return "Quaternion";
            if (ValueKind == 17) return "Color";
            if (ValueKind == 18) return "Color32";
            if (ValueKind == 19) return "VRCUrl";
            return "int";
        }

        public bool _ValidSeed(int seed)
        {
            if (ValueKind == 19) return seed == 101;
            return seed >= 1 && seed <= (ValueKind == 2 ? 10000 : ValueKind == 6 ? 300 : ValueKind == 9 ? 600 : 1000000);
        }

        public object _ExpectedValue(int seed, int index)
        {
            if (ValueKind == 1) return 5000000000L + (long)seed * 100L + index + 1L;
            if (ValueKind == 2) return seed * 100f + index + 1.5f;
            if (ValueKind == 3) return 5000000000d + seed * 100d + index + 1.5d;
            if (ValueKind == 4) return true; // Every field differs from its false default; bool ignores seed.
            if (ValueKind == 5 && CustomStringPattern != "") return CustomStringPattern;
            if (ValueKind == 5) return "s" + seed + "-v" + (index + 1).ToString("D3");
            // Convert preserves the narrow boxed type through the Udon object-return path.
            if (ValueKind == 6) return System.Convert.ToInt16(seed * 100 + index + 1);
            if (ValueKind == 7) return System.Convert.ToByte(index + 1);
            if (ValueKind == 8) return System.Convert.ToSByte(-index - 1);
            if (ValueKind == 9) return System.Convert.ToUInt16(seed * 100 + index + 1);
            if (ValueKind == 10) return System.Convert.ToUInt32(3000000000L + (long)seed * 100L + index + 1L);
            if (ValueKind == 11) return System.Convert.ToUInt64(5000000000L + (long)seed * 100L + index + 1L);
            if (ValueKind == 12) return System.Convert.ToChar(0x3001 + index);
            if (ValueKind == 13) return new Vector2(seed + index + 0.25f, -seed - index - 0.5f);
            if (ValueKind == 14) return new Vector3(seed + index + 0.25f, -seed - index - 0.5f, index + 0.75f);
            if (ValueKind == 15) return new Vector4(seed + index + 0.25f, -seed - index - 0.5f, index + 0.75f, 1.25f);
            if (ValueKind == 16)
            {
                // Unit quaternions with exact components avoid rounding from normalization.
                int axis = index % 4;
                return new Quaternion(axis == 0 ? 1f : 0f, axis == 1 ? 1f : 0f, axis == 2 ? 1f : 0f, axis == 3 ? 1f : 0f);
            }
            if (ValueKind == 17) return new Color(0.25f, 0.5f, (index + 1) / 128f, 1f);
            if (ValueKind == 18) return new Color32(System.Convert.ToByte(index + 1), 31, 63, 255);
            if (ValueKind == 19) return UrlPattern != null && index >= 0 && index < UrlPattern.Length ? UrlPattern[index] : EmptyUrl;
            return seed * 100 + index + 1;
        }

        public bool _MatchesExpected(int seed, int index)
        {
            object value = _ReadRaw(index);
            if (value == null) return false;
            object expected = _ExpectedValue(seed, index);
            if (ValueKind == 19) return ((VRCUrl)value).Get() == ((VRCUrl)expected).Get();
            return value.Equals(expected);
        }

        public string _ValueText(int index)
        {
            object raw = _ReadRaw(index);
            if (raw == null) return "NULL";
            if (ValueKind == 12) return "U+" + ((int)(char)raw).ToString("X4");
            if (ValueKind == 13) { Vector2 v = (Vector2)raw; return v.x.ToString("R") + "," + v.y.ToString("R"); }
            if (ValueKind == 14) { Vector3 v = (Vector3)raw; return v.x.ToString("R") + "," + v.y.ToString("R") + "," + v.z.ToString("R"); }
            if (ValueKind == 15) { Vector4 v = (Vector4)raw; return v.x.ToString("R") + "," + v.y.ToString("R") + "," + v.z.ToString("R") + "," + v.w.ToString("R"); }
            if (ValueKind == 16) { Quaternion v = (Quaternion)raw; return v.x.ToString("R") + "," + v.y.ToString("R") + "," + v.z.ToString("R") + "," + v.w.ToString("R"); }
            if (ValueKind == 17) { Color v = (Color)raw; return v.r.ToString("R") + "," + v.g.ToString("R") + "," + v.b.ToString("R") + "," + v.a.ToString("R"); }
            if (ValueKind == 18) { Color32 v = (Color32)raw; return v.r + "," + v.g + "," + v.b + "," + v.a; }
            if (ValueKind == 19) return ((VRCUrl)raw).Get();
            return raw.ToString();
        }

        private object DefaultValue()
        {
            if (ValueKind == 1) return 0L;
            if (ValueKind == 2) return 0f;
            if (ValueKind == 3) return 0d;
            if (ValueKind == 4) return false;
            if (ValueKind == 5) return "";
            if (ValueKind == 6) return System.Convert.ToInt16(0);
            if (ValueKind == 7) return System.Convert.ToByte(0);
            if (ValueKind == 8) return System.Convert.ToSByte(0);
            if (ValueKind == 9) return System.Convert.ToUInt16(0);
            if (ValueKind == 10) return 0u;
            if (ValueKind == 11) return 0ul;
            if (ValueKind == 12) return System.Convert.ToChar(0);
            if (ValueKind == 13) return new Vector2();
            if (ValueKind == 14) return new Vector3();
            if (ValueKind == 15) return new Vector4();
            if (ValueKind == 16) return new Quaternion();
            if (ValueKind == 17) return new Color();
            if (ValueKind == 18) return new Color32();
            if (ValueKind == 19) return EmptyUrl;
            return 0;
        }

        public void _WritePattern(int seed, int count)
        {
            if (!_CanWrite() || !_ValidSeed(seed) || count < 1 || count > IntCount) return;
            Observe("before-write", "seed=" + seed + "|count=" + count);
            if (ArrayMode)
            {
                object raw = GetProgramVariable("Values");
                // Explicit save repairs a missing/wrong-length array; restoration remains observable.
                if (ValueKind == 1)
                {
                    long[] longs = raw == null ? new long[IntCount] : (long[])raw;
                    if (longs.Length != IntCount) longs = new long[IntCount];
                    for (int i = 0; i < count; i++) longs[i] = 5000000000L + (long)seed * 100L + i + 1L;
                    SetProgramVariable("Values", longs);
                }
                else
                {
                    int[] values = raw == null ? new int[IntCount] : (int[])raw;
                    if (values.Length != IntCount) values = new int[IntCount];
                    for (int i = 0; i < count; i++) values[i] = seed * 100 + i + 1;
                    SetProgramVariable("Values", values);
                }
            }
            else
                for (int i = 0; i < count; i++)
                    SetProgramVariable("V" + (i + 1).ToString("D3"), _ExpectedValue(seed, i));
            Observe("after-write", "seed=" + seed + "|count=" + count);
            _Request();
        }

        public void _Request()
        {
            if (!_CanWrite()) { Observe("request-blocked", "not-restored-or-not-owner"); return; }
            Requests++;
            LastRequestTime = Time.realtimeSinceStartup;
            Observe("request", "");
            RequestSerialization();
        }

        public void _InitializeNulls()
        {
            if (!_CanWrite()) return;
            Observe("before-initialize-nulls", "manual-diagnostic-only");
            if (ArrayMode)
            {
                if (GetProgramVariable("Values") == null)
                {
                    if (ValueKind == 1) SetProgramVariable("Values", new long[IntCount]);
                    else SetProgramVariable("Values", new int[IntCount]);
                }
            }
            else
                for (int i = 0; i < IntCount; i++)
                {
                    string key = "V" + (i + 1).ToString("D3");
                    if (GetProgramVariable(key) == null) SetProgramVariable(key, DefaultValue());
                }
            Observe("after-initialize-nulls", "manual-diagnostic-only");
            _Request();
        }

        private void Observe(string eventName, string detail)
        {
            if (Utilities.IsValid(BatchLab)) BatchLab._Record(this, eventName, detail);
            else if (Utilities.IsValid(Lab)) Lab._Record(this, eventName, detail);
        }
    }
}
