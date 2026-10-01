using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0257 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[257];
    }
}
