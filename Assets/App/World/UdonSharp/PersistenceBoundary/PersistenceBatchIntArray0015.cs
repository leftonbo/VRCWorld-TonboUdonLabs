using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0015 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[15];
    }
}
