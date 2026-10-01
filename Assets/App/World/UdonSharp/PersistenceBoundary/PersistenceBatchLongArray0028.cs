using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0028 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[28];
    }
}
