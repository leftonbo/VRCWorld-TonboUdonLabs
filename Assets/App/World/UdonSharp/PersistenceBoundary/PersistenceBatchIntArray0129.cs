using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchIntArray0129 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[129];
    }
}
