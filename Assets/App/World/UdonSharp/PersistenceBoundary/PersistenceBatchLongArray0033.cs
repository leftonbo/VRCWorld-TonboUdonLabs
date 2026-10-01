using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0033 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[33];
    }
}
