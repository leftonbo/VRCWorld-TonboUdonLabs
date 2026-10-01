using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchULong012 : PersistenceProbe
    {
        [UdonSynced] public ulong V001;
        [UdonSynced] public ulong V002;
        [UdonSynced] public ulong V003;
        [UdonSynced] public ulong V004;
        [UdonSynced] public ulong V005;
        [UdonSynced] public ulong V006;
        [UdonSynced] public ulong V007;
        [UdonSynced] public ulong V008;
        [UdonSynced] public ulong V009;
        [UdonSynced] public ulong V010;
        [UdonSynced] public ulong V011;
        [UdonSynced] public ulong V012;
    }
}
