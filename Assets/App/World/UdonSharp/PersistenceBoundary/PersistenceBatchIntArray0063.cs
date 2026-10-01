using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0063 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[63];
    }
}
