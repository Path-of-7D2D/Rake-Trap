using UnityEngine.Scripting;

namespace RakeTrap
{
    [Preserve]
    public class RakeTrapModApi : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            Log.Out("[RakeTrap] Loaded.");
        }
    }
}

