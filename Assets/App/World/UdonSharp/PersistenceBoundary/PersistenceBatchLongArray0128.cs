using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0128 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[128];
    }
}
