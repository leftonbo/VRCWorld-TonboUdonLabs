using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0032 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[32];
    }
}
