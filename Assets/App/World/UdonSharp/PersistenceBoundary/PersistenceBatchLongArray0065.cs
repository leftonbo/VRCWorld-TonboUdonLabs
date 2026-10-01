using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0065 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[65];
    }
}
