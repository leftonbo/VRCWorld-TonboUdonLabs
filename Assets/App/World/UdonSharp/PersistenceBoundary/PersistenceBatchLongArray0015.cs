using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0015 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[15];
    }
}
