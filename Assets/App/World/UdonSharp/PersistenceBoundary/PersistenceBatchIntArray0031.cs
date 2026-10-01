using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0031 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[31];
    }
}
