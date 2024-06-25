using Vintagestory.API.Common;

namespace SNoxeGR.CharacterSystem;

public interface ITraitAttributeBehaviour
{
    string PropertyName();

    void Process(EntityPlayer player, float value);
}