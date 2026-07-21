namespace DesktopPet.Core;

public sealed class PetStateMachine
{
    public PetState Current { get; private set; } = PetState.Idle;

    public event Action<PetState, PetState>? StateChanged;

    public void TransitionTo(PetState next)
    {
        if (Current == next)
        {
            return;
        }

        var previous = Current;
        Current = next;
        StateChanged?.Invoke(previous, next);
    }
}
