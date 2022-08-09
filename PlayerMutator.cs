using XRL;
using XRL.World;

using XRL.World.Parts;

[PlayerMutator]
public class MerchantMemory_PlayerMutator : IPlayerMutator
{
    public void mutate(GameObject player)
    {
        player.RequirePart<soron_MerchantMemoryPart>().InitAbilities();
    }
}
