using System;

public class PracticeState
{
    public static readonly PracticeStep[] Sequence =
    {
        PracticeStep.StripPointA, 
        PracticeStep.InsertRJ45PointA,
        PracticeStep.CrimpPointA,

        PracticeStep.StripPointB,
        PracticeStep.InsertRJ45PointB,
        PracticeStep.CrimpPointB,

        PracticeStep.LANTest,

        PracticeStep.Finished
    };

    private readonly PracticeStep startStep;
    private int index;

    public event Action<PracticeStep> OnStepChanged;

    public PracticeState() : this(PracticeStep.StripPointA)
    {
    }

    public PracticeState(PracticeStep startStep)
    {
        this.startStep = startStep;
        index = IndexOf(startStep);
    }

    public PracticeStep CurrentStep => Sequence[index];

    public bool IsFinished => CurrentStep == PracticeStep.Finished;

    public bool Advance()
    {
        if (index >= Sequence.Length - 1)
            return false;

        SetIndex(index + 1);

        return true;
    }

    public void Reset()
    {
        SetIndex(IndexOf(startStep));
    }

    private void SetIndex(int value)
    {
        if (index == value)
            return;

        index = value;

        OnStepChanged?.Invoke(CurrentStep);
    }

    private static int IndexOf(PracticeStep step)
    {
        int found = Array.IndexOf(Sequence, step);
        return found < 0 ? 0 : found;
    }
}
