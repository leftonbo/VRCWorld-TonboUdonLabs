using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0065 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[65];
    }
}
