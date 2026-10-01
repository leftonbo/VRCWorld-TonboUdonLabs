using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchDouble001 : PersistenceProbe
    {
        [UdonSynced] public double V001;
    }
}
