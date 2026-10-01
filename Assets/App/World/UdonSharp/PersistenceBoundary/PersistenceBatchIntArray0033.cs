using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0033 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[33];
    }
}
