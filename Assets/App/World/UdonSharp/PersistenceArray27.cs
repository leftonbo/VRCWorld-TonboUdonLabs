using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceArray27 : PersistenceProbe
    {
        [UdonSynced] public int[] Values = new int[27];
    }
}
