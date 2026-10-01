using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0063 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[63];
    }
}
