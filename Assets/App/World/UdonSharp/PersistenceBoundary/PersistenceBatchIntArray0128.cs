using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0128 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[128];
    }
}
