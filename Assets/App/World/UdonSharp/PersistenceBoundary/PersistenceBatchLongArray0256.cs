using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0256 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[256];
    }
}
