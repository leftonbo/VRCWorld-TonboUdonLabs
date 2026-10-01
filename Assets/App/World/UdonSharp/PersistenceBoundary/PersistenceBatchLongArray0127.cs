using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0127 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[127];
    }
}
