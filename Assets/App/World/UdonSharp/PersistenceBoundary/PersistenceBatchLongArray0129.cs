using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0129 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[129];
    }
}
