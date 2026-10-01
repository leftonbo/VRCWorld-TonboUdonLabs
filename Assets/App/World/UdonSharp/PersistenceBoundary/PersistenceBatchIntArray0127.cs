using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0127 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[127];
    }
}
