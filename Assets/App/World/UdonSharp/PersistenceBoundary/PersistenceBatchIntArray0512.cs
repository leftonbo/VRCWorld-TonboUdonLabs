using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0512 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[512];
    }
}
