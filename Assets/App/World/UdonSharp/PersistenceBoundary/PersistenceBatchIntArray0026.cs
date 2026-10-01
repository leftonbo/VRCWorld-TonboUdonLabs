using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0026 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[26];
    }
}
