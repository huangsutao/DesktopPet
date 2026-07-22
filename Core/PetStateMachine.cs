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

    public bool TryStartWalk()
    {
        if (Current != PetState.Idle)
        {
            return false;
        }

        TransitionTo(PetState.Walk);
        return true;
    }

    public void EndWalk()
    {
        if (Current == PetState.Walk)
        {
            TransitionTo(PetState.Idle);
        }
    }

    /// <summary>Autonomy one-shot action (reuses Clicked + animation-complete → Idle).</summary>
    public bool TryStartAct()
    {
        if (Current != PetState.Idle)
        {
            return false;
        }

        TransitionTo(PetState.Clicked);
        return true;
    }

    public bool TryStartSleep()
    {
        if (Current is not (PetState.Idle or PetState.Walk))
        {
            return false;
        }

        TransitionTo(PetState.Sleep);
        return true;
    }

    public void Wake()
    {
        if (Current == PetState.Sleep)
        {
            TransitionTo(PetState.Idle);
        }
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
