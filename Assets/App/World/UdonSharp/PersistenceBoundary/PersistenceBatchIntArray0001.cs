using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0001 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[1];
    }
}
