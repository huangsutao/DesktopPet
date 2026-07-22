namespace DesktopPet.Core;

public sealed class PetStateMachine
{
    public PetState Current { get; private set; } = PetState.Idle;

    public event Action<PetState, PetState>? StateChanged;

    public void Reset() => TransitionTo(PetState.Idle);

    public bool TryClick()
    {
        if (Current is not (PetState.Idle or PetState.Walk or PetState.Sleep))
        {
            return false;
        }

        TransitionTo(PetState.Clicked);
        return true;
    }

    public bool TryStartDrag()
    {
        if (Current == PetState.Dragging)
        {
            return true;
        }

        TransitionTo(PetState.Dragging);
        return true;
    }

    public void EndDrag()
    {
        if (Current == PetState.Dragging)
        {
            TransitionTo(PetState.Idle);
        }
    }

    public void EndClick()
    {
        if (Current == PetState.Clicked)
        {
            TransitionTo(PetState.Idle);
        }
    }

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
