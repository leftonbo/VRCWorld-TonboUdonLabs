using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray1024 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[1024];
    }
}
