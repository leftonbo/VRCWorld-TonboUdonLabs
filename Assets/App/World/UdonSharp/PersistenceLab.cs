using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PersistenceLab : UdonSharpBehaviour
    {
        public Text StatusText;
        public InputField SeedInput;
        public InputField PrefixInput;
        public InputField RunInput;
        public InputField ExportInput;
        public Button[] WriteButtons;
        public string BuildLabel;
        [System.NonSerialized] public string Log = "";
        [System.NonSerialized] public int DroppedRows;
        private PersistenceProbe[] _local = new PersistenceProbe[3];
        private int _selected;
        private int _usage = -1;
        private int _limit = -1;
        private float _usageTime = -1;
        private float _lastUsageRequest = -30;
        private bool _localRestored;

        private void Start()
        {
            Append("session", "controller", "no-auto-write;post-success-is-not-cloud-ack");
            Refresh();
        }

        public void _Bind(PersistenceProbe probe)
        {
            if (probe.CaseIndex < 0 || probe.CaseIndex >= _local.Length || !Networking.IsOwner(probe.gameObject)) return;
            _local[probe.CaseIndex] = probe;
            Refresh();
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            _localRestored = true;
            Append("local-player-restored", "controller", "");
            Refresh();
        }

        public void _Record(PersistenceProbe probe, string eventName, string detail)
        {
            VRCPlayerApi owner = Networking.GetOwner(probe.gameObject);
            string values = "";
            int nulls = 0;
            for (int i = 0; i < probe.IntCount; i++)
            {
                object raw = probe._ReadRaw(i);
                if (raw == null) nulls++;
                values += (i == 0 ? "" : "|") + (i + 1) + "=" + (raw == null ? "NULL" : raw.ToString());
            }
            Append(eventName, probe.CaseIndex.ToString(), "owner=" + (Utilities.IsValid(owner) ? owner.playerId : -1)
                + ";localOwner=" + Networking.IsOwner(probe.gameObject) + ";ready=" + probe.Restored
                + ";networkId=" + probe.SceneNetworkId + ";ints=" + probe.IntCount
                + ";valueType=" + probe._TypeName() + ";valueCount=" + probe.IntCount
                + ";syncedFields=" + (probe.ArrayMode ? 1 : probe.IntCount) + ";shape=" + probe._Shape()
                + ";nulls=" + nulls + ";requests=" + probe.Requests + ";pre=" + probe.PreSerializations
                + ";post=" + probe.PostSerializations + ";lastSuccess=" + probe.LastSuccess
                + ";lastBytes=" + probe.LastBytes + ";requestTime=" + probe.LastRequestTime
                + ";postTime=" + probe.LastPostTime + ";usage=" + _usage + ";limit=" + _limit
                + ";usageTime=" + _usageTime + ";" + detail + ";values=" + values);
            Refresh();
        }

        public void _SelectGrowing() { Select(0); }
        public void _SelectFixed() { Select(1); }
        public void _SelectArray() { Select(2); }
        private void Select(int index)
        {
            _selected = index;
            int prefix;
            if (Utilities.IsValid(_local[index]) && int.TryParse(PrefixInput.text, out prefix) && prefix > _local[index].IntCount)
                PrefixInput.text = _local[index].IntCount.ToString();
            Refresh();
        }
        public void _Read()
        {
            PersistenceProbe probe = _local[_selected];
            if (Utilities.IsValid(probe)) _Record(probe, "manual-read", "");
            else Refresh();
        }

        public void _WriteAll() { Write(false); }
        public void _WritePrefix() { Write(true); }
        private void Write(bool prefixOnly)
        {
            PersistenceProbe probe = _local[_selected];
            int seed;
            if (!Utilities.IsValid(probe) || !probe._CanWrite()) return;
            int prefix = probe.IntCount;
            if (!int.TryParse(SeedInput.text, out seed) || !probe._ValidSeed(seed)
                || (prefixOnly && (!int.TryParse(PrefixInput.text, out prefix) || prefix < 1 || prefix > probe.IntCount)))
            {
                Append("input-rejected", _selected.ToString(), "seed=1..1000000;prefix=1..count");
                StatusText.text = "入力を確認: seedは1〜1000000 (floatは10000まで)、比較数は1〜値の数。";
                return;
            }
            probe._WritePattern(seed, prefixOnly ? prefix : probe.IntCount);
        }

        public void _RequestOnly()
        {
            PersistenceProbe probe = _local[_selected];
            if (Utilities.IsValid(probe)) probe._Request();
        }

        public void _InitializeNulls()
        {
            PersistenceProbe probe = _local[_selected];
            if (Utilities.IsValid(probe)) probe._InitializeNulls();
        }

        public void _CheckExpected()
        {
            PersistenceProbe probe = _local[_selected];
            int seed;
            int prefix;
            if (!Utilities.IsValid(probe) || !int.TryParse(SeedInput.text, out seed)
                || !probe._ValidSeed(seed) || !int.TryParse(PrefixInput.text, out prefix)
                || prefix < 1 || prefix > probe.IntCount) return;
            int matched = 0;
            for (int i = 0; i < prefix; i++)
            {
                if (probe._MatchesExpected(seed, i)) matched++;
            }
            _Record(probe, "check-expected", "seed=" + seed + "|prefix=" + prefix + "|matched=" + matched);
            StatusText.text += "\n比較結果: " + matched + " / " + prefix + " 一致 (値の比較のみ)";
        }

        public void _RequestUsage()
        {
            if (!_localRestored || Time.realtimeSinceStartup - _lastUsageRequest < 10) return;
            _lastUsageRequest = Time.realtimeSinceStartup;
            Append("usage-request", "controller", "");
            Networking.RequestStorageUsageUpdate();
        }

        // Installed SDK 3.10.5 uses a parameterless event, despite the web event table.
        public override void OnPersistenceUsageUpdated()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            _usage = Networking.GetPlayerObjectStorageUsage(Networking.LocalPlayer);
            _limit = Networking.GetPlayerObjectStorageLimit();
            _usageTime = Time.realtimeSinceStartup;
            Append("usage-updated", "controller", "bytes=" + _usage + ";limit=" + _limit);
            Refresh();
        }

        public override void OnPlayerObjectStorageWarning(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal) Append("storage-warning", "controller", "");
        }
        public override void OnPlayerObjectStorageExceeded(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal) Append("storage-exceeded", "controller", "");
        }

        public void _Export()
        {
            _Read();
            ExportInput.text = "utc\telapsed_s\tbuild\trun\tevent\tcase\tdetail\n" + Log
                + "# dropped_rows=" + DroppedRows + "\n# cloud_commit_ack=unavailable\n";
        }

        private void Append(string eventName, string caseName, string detail)
        {
            string run = Utilities.IsValid(RunInput) ? RunInput.text : "";
            string environment = "VRChat-client";
#if UNITY_EDITOR
            environment = "Editor-ClientSim";
#endif
            string row = System.DateTime.UtcNow.ToString("O") + "\t" + Time.realtimeSinceStartup + "\t"
                + Clean(BuildLabel + "|" + environment) + "\t" + Clean(run) + "\t" + eventName
                + "\t" + caseName + "\t" + Clean(detail) + "\n";
            // ponytail: 64000-character buffer preserves earliest restore evidence; export before long runs.
            if (Log.Length + row.Length <= 64000) Log += row;
            else DroppedRows++;
            Debug.Log("[PersistenceLab] " + row);
        }

        private string Clean(string value) { return value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " "); }

        private void Refresh()
        {
            if (!Utilities.IsValid(StatusText)) return;
            PersistenceProbe probe = _local[_selected];
            bool ready = Utilities.IsValid(probe) && probe._CanWrite();
            if (WriteButtons != null)
                for (int i = 0; i < WriteButtons.Length; i++) WriteButtons[i].interactable = ready;
            string name = _selected == 0 ? "変更対象 scalar" : (_selected == 1 ? "固定27 scalar" : "int[27] 対照");
            if (Utilities.IsValid(probe) && probe.ValueKind != 0)
                name = probe._TypeName() + " " + probe.IntCount + " scalar";
            string status = BuildLabel + "\n選択: " + name + " / " + (ready ? "復元済み・書込み可" : "復元待ち・書込み不可")
                + "\n保存完了通知: 利用不可。送信成功後もRejoinで確認。"
                + "\nPlayerObject使用量: " + _usage + " / " + _limit + " bytes (取得時刻 " + _usageTime + "s)";
            if (Utilities.IsValid(probe))
            {
                status += "\ntype=" + probe._TypeName() + " values=" + probe.IntCount + " / synced fields=" + (probe.ArrayMode ? 1 : probe.IntCount)
                    + " / networkId=" + probe.SceneNetworkId + " / " + probe._Shape()
                    + "\nrequest=" + probe.Requests + " pre=" + probe.PreSerializations + " post=" + probe.PostSerializations
                    + " lastSuccess=" + (probe.PostSerializations == 0 ? "未送信" : probe.LastSuccess.ToString()) + " bytes=" + probe.LastBytes;
                for (int i = 0; i < probe.IntCount; i++)
                {
                    object raw = probe._ReadRaw(i);
                    status += (i % 4 == 0 ? "\n" : "   ") + (i + 1).ToString("D2") + ":" + (raw == null ? "NULL" : raw.ToString());
                }
            }
            status += "\nログ: " + Log.Length + " chars / dropped=" + DroppedRows;
            StatusText.text = status;
        }
    }
}
