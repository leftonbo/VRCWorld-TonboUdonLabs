using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PersistenceBatchLab : UdonSharpBehaviour
    {
        public string[] Labels;
        public int[] NominalBytes;
        public Text[] Rows;
        public Text StatusText;
        public Button SaveButton;
        public InputField ExportInput;
        public Text PageText;
        public Text StorageText;
        public Text SectionText;
        public int[] CasePages;
        public string[] PageTitles;
        [System.NonSerialized] public int StorageUsage = -1;
        [System.NonSerialized] public int StorageLimit = -1;
        [System.NonSerialized] public int StorageUpdateRequests;
        [System.NonSerialized] public int StorageUpdates;
        [System.NonSerialized] public bool StoragePending;
        private bool _storageStarted;
        private bool _storageAfterSave;
        private bool _storageRefreshScheduled;
        private float _lastStorageRequest = -10f;
        private string _storageUpdatedUtc = "unavailable";
        private bool _exporting;
        private int _exportNext;
        private string _exportText;
        private int _page;
        private PersistenceProbe[] _local;
        private int[] _restoredMatches;
        private int[] _currentMatches;
        private int[] _postsAtSave;
        private int _next;
        private bool _saving;
        private bool _saveStarted;
        private string _warning = "";

        private void Start() { EnsureState(); ShowPage(); }

        public void _PreviousPage()
        {
            EnsureState();
            _page = (_page + PageTitles.Length - 1) % PageTitles.Length;
            ShowPage();
        }

        public void _NextPage()
        {
            EnsureState();
            _page = (_page + 1) % PageTitles.Length;
            ShowPage();
        }

        private void ShowPage()
        {
            if (_page >= PageTitles.Length) _page = 0;
            for (int i = 0; i < Rows.Length; i++) Rows[i].gameObject.SetActive(CasePages[i] == _page);
            if (Utilities.IsValid(PageText)) PageText.text = "ページ " + (_page + 1) + "/" + PageTitles.Length;
            if (Utilities.IsValid(SectionText)) SectionText.text = PageTitles[_page];
            Refresh();
        }

        private void EnsureState()
        {
            if (_local != null) return;
            _local = new PersistenceProbe[Labels.Length];
            _restoredMatches = new int[Labels.Length];
            _currentMatches = new int[Labels.Length];
            _postsAtSave = new int[Labels.Length];
        }

        public void _Bind(PersistenceProbe probe)
        {
            EnsureState();
            int index = probe.CaseIndex;
            if (index < 0 || index >= _local.Length || !Networking.IsOwner(probe.gameObject)) return;
            if (!Utilities.IsValid(_local[index])) _restoredMatches[index] = Matches(probe);
            _local[index] = probe;
            _currentMatches[index] = Matches(probe);
            Refresh();
        }

        private int Matches(PersistenceProbe probe)
        {
            int matched = 0;
            for (int i = 0; i < probe.IntCount; i++)
            {
                if (probe._MatchesExpected(101, i)) matched++;
            }
            return matched;
        }

        public void _Record(PersistenceProbe probe, string eventName, string detail)
        {
            EnsureState();
            int index = probe.CaseIndex;
            if (index < 0 || index >= _local.Length || _local[index] != probe) return;
            if (eventName == "after-write" || eventName == "deserialization") _currentMatches[index] = Matches(probe);
            Refresh();
        }

        public void _SaveAll()
        {
            EnsureState();
            if (_saving || _exporting) return;
            for (int i = 0; i < _local.Length; i++)
                if (!Utilities.IsValid(_local[i]) || !_local[i]._CanWrite()) return;
            for (int i = 0; i < _local.Length; i++) _postsAtSave[i] = _local[i].PostSerializations;
            _next = 0;
            _saving = true;
            _saveStarted = true;
            _storageAfterSave = false;
            Refresh();
            SendCustomEventDelayedFrames(nameof(_WriteNext), 1);
        }

        public void _WriteNext()
        {
            if (!_saving) return;
            // One PlayerObject per frame keeps the full matrix below a single Udon event's work budget.
            var probe = _local[_next];
            if (!Utilities.IsValid(probe) || !probe._CanWrite()) { _saving = false; Refresh(); return; }
            probe._WritePattern(101, probe.IntCount);
            _next++;
            if (_next < _local.Length) SendCustomEventDelayedFrames(nameof(_WriteNext), 1);
            else _saving = false;
            Refresh();
        }

        private void Refresh()
        {
            int ready = 0;
            int sent = 0;
            int failed = 0;
            for (int i = 0; i < _local.Length; i++)
            {
                var probe = _local[i];
                if (!Utilities.IsValid(probe)) { if (Rows[i].gameObject.activeSelf) Rows[i].text = Labels[i] + "\n復元待ち"; continue; }
                if (probe._CanWrite()) ready++;
                if (_saveStarted && probe.PostSerializations > _postsAtSave[i])
                { if (probe.LastSuccess) sent++; else failed++; }
                // Only render the visible page; all cases still participate in saving and readiness.
                if (!Rows[i].gameObject.activeSelf) continue;
                string bytes = probe.PostSerializations == 0 ? "—" : probe.LastBytes.ToString() + "B";
                string nominal = NominalBytes[i] < 0 ? "可変長" : "値" + NominalBytes[i] + "B";
                int restored = _restoredMatches[i];
                string result = restored == probe.IntCount ? "保持" : restored == 0 ? "0一致" : "部分";
                Rows[i].text = Labels[i] + "  " + nominal + "  送信" + bytes
                    + "\n復元 " + restored + "/" + probe.IntCount + " " + result + "  現在 " + _currentMatches[i] + "/" + probe.IntCount;
                Rows[i].color = restored == probe.IntCount ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f);
            }
            if (ready == _local.Length && !_storageStarted)
            {
                _storageStarted = true;
                _RequestStorageUsage();
            }
            if (_saveStarted && !_saving && sent + failed == _local.Length && !_storageAfterSave)
            {
                _storageAfterSave = true;
                _RequestStorageUsage();
            }
            SaveButton.interactable = ready == _local.Length && !_saving && !_exporting;
            StatusText.text = "復元 " + ready + "/" + _local.Length + "  |  "
                + (_saving ? "一括書込み " + _next + "/" + _local.Length
                    : _saveStarted ? "送信成功 " + sent + "/" + _local.Length + " 失敗 " + failed
                    : "一括保存 → Rejoin → 下の『復元』を見るだけ。初回の0一致は未保存です。") + _warning
                + (_exporting ? " | 結果作成 " + _exportNext + "/" + _local.Length : "");
        }

        public void _Export()
        {
            EnsureState();
            if (_saving || _exporting) return;
            _exportText = "# PO-BATCH-v1 UTC=" + System.DateTime.UtcNow.ToString("O")
                + "\n# playerobject_storage_usage_bytes=" + StorageUsage + " limit_bytes=" + StorageLimit
                + " pending=" + StoragePending + " updated_utc=" + _storageUpdatedUtc
                + "\ncase\tvalue_type\tcount\tnominal_bytes\trestored_matches\tcurrent_matches\tpost_success\tsent_bytes\tcurrent_values\tcurrent_shape\n";
            _exportNext = 0;
            _exporting = true;
            Refresh();
            SendCustomEventDelayedFrames(nameof(_ExportNextCase), 1);
        }

        public void _ExportNextCase()
        {
            if (!_exporting) return;
            int i = _exportNext;
            var probe = _local[i];
            if (!Utilities.IsValid(probe)) _exportText += Labels[i] + "\twaiting\n";
            else
            {
                string values = "";
                for (int j = 0; j < probe.IntCount; j++)
                {
                    values += (j == 0 ? "" : "|") + (j + 1) + "=" + probe._ValueText(j);
                }
                _exportText += Labels[i] + "\t" + probe._TypeName() + "\t" + probe.IntCount + "\t" + NominalBytes[i]
                    + "\t" + _restoredMatches[i] + "\t" + _currentMatches[i] + "\t"
                    + (probe.PostSerializations == 0 ? "unavailable" : probe.LastSuccess.ToString()) + "\t"
                    + (probe.PostSerializations == 0 ? "unavailable" : probe.LastBytes.ToString()) + "\t" + values + "\t" + probe._Shape() + "\n";
            }
            // ponytail: one case per frame; split within a case if future arrays exceed 1024 elements.
            _exportNext++;
            if (_exportNext < _local.Length) { SendCustomEventDelayedFrames(nameof(_ExportNextCase), 1); return; }
            _exportText += "# cloud_commit_ack=unavailable\n";
            ExportInput.gameObject.SetActive(!ExportInput.gameObject.activeSelf);
            ExportInput.text = _exportText;
            if (ExportInput.gameObject.activeSelf) ExportInput.ActivateInputField();
            _exporting = false;
            Refresh();
        }

        public void _RequestStorageUsage()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            float remaining = 10f - (Time.realtimeSinceStartup - _lastStorageRequest);
            if (remaining > 0f)
            {
                StoragePending = true;
                ShowStorage();
                if (!_storageRefreshScheduled)
                {
                    _storageRefreshScheduled = true;
                    SendCustomEventDelayedSeconds(nameof(_RequestStorageUsage), remaining + 0.1f);
                }
                return;
            }
            _storageRefreshScheduled = false;
            _lastStorageRequest = Time.realtimeSinceStartup;
            ReadStorage();
            StoragePending = true;
            StorageUpdateRequests++;
            ShowStorage();
            Networking.RequestStorageUsageUpdate();
        }

        public override void OnPersistenceUsageUpdated()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            ReadStorage();
            StoragePending = false;
            StorageUpdates++;
            _storageUpdatedUtc = System.DateTime.UtcNow.ToString("O");
            ShowStorage();
        }

        private void ReadStorage()
        {
            StorageUsage = Networking.GetPlayerObjectStorageUsage(Networking.LocalPlayer);
            StorageLimit = Networking.GetPlayerObjectStorageLimit();
        }

        private void ShowStorage()
        {
            if (!Utilities.IsValid(StorageText)) return;
            StorageText.text = "PlayerObject全体: " + StorageUsage + " / " + StorageLimit + " B"
                + (StoragePending ? "（更新待ち）" : "（最終取得値）") + "  ※保存完了通知ではありません";
        }

        public override void OnPlayerObjectStorageExceeded(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal) { _warning = " | 保存容量超過"; Refresh(); }
        }
    }
}
