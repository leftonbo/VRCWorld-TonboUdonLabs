using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0257 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[257];
    }
}
