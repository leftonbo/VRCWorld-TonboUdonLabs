using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0001 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[1];
    }
}
