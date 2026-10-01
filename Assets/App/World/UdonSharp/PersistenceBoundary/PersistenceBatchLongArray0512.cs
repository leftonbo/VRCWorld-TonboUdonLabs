using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0512 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[512];
    }
}
