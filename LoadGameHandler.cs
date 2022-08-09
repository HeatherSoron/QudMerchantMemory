using XRL;
using XRL.Core;
using XRL.World;

using XRL.World.Parts;
  
[HasCallAfterGameLoadedAttribute]
public class MerchantMemory_LoadGameHandler
{
    [CallAfterGameLoadedAttribute]
    public static void MyLoadGameCallback()
    {
        // Called whenever loading a save game
        GameObject player = XRLCore.Core?.Game?.Player?.Body;
        if (player != null)
        {
            player.RequirePart<Soron_MerchantMemoryPart>().InitAbilities();
        }
    }
}
