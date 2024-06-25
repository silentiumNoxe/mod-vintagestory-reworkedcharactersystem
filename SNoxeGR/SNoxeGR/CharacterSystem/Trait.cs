namespace SNoxeGR.CharacterSystem;

public class Trait : Vintagestory.GameContent.Trait
{
    public int Repeat;
    public long Duration;
    public bool Temp;
    public bool Mul;

    public bool IsTemp()
    {
        return Temp;
    }

    public bool IsMul()
    {
        return Mul;
    }

    public override string ToString()
    {
        return Code;
    }
}