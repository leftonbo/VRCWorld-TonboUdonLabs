using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0064 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[64];
    }
}
