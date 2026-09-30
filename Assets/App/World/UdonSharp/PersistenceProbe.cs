using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace App
{
    // Shared observation code adds no synced fields to the three measured schemas.
    public abstract class PersistenceProbe : UdonSharpBehaviour
    {
        public PersistenceLab Lab;
        public int CaseIndex;
        public int IntCount;
        public bool ArrayMode;
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
            if (player.isLocal) Lab._Bind(this);
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
            int[] values = (int[])raw;
            if (index >= values.Length) return null;
            return values[index];
        }

        public string _Shape()
        {
            if (!ArrayMode) return "scalar";
            object raw = GetProgramVariable("Values");
            if (raw == null) return "array-null";
            return "array-length=" + ((int[])raw).Length;
        }

        public bool _CanWrite()
        {
            return Restored && Utilities.IsValid(Networking.LocalPlayer) && Networking.IsOwner(gameObject);
        }

        public void _WritePattern(int seed, int count)
        {
            if (!_CanWrite() || seed < 1 || seed > 1000000 || count < 1 || count > IntCount) return;
            Observe("before-write", "seed=" + seed + "|count=" + count);
            if (ArrayMode)
            {
                object raw = GetProgramVariable("Values");
                if (raw == null || ((int[])raw).Length != IntCount)
                {
                    Observe("write-blocked", "array-null-or-length-mismatch");
                    return;
                }
                int[] values = (int[])raw;
                for (int i = 0; i < count; i++) values[i] = seed * 100 + i + 1;
                SetProgramVariable("Values", values);
            }
            else
                for (int i = 0; i < count; i++)
                    SetProgramVariable("V" + (i + 1).ToString("D3"), seed * 100 + i + 1);
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
                if (GetProgramVariable("Values") == null) SetProgramVariable("Values", new int[IntCount]);
            }
            else
                for (int i = 0; i < IntCount; i++)
                {
                    string key = "V" + (i + 1).ToString("D3");
                    if (GetProgramVariable(key) == null) SetProgramVariable(key, 0);
                }
            Observe("after-initialize-nulls", "manual-diagnostic-only");
            _Request();
        }

        private void Observe(string eventName, string detail)
        {
            if (Utilities.IsValid(Lab)) Lab._Record(this, eventName, detail);
        }
    }
}
