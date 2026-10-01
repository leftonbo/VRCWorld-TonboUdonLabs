using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchLongArray0026 : PersistenceProbe
    {
        [UdonSynced] public long[] Values = new long[26];
    }
}
