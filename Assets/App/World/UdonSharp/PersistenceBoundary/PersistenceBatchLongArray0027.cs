using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0027 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[27];
    }
}
