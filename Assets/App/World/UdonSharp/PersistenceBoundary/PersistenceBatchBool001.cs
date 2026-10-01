using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchBool001 : PersistenceProbe
    {
        [UdonSynced] public bool V001;
    }
}
