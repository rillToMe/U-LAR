public enum PracticeAction
{
    Strip,
    InsertRJ45,
    Crimp,
    LANTest
}


public static class PracticeValidator
{
   
    public static bool IsAllowed(PracticeStep step, PracticeAction action, PracticeSide side)
    {
        return RequiredStep(action, side) == step;
    }

    public static bool IsAllowed(PracticeStep step, PracticeAction action)
    {
        return action == PracticeAction.LANTest
            ? step == PracticeStep.LANTest
            : IsAllowed(step, action, PracticeSide.A) || IsAllowed(step, action, PracticeSide.B);
    }

    public static PracticeStep RequiredStep(PracticeAction action, PracticeSide side)
    {
        bool isA = side == PracticeSide.A;

        switch (action)
        {
            case PracticeAction.Strip:
                return isA ? PracticeStep.StripPointA : PracticeStep.StripPointB;

            case PracticeAction.InsertRJ45:
                return isA ? PracticeStep.InsertRJ45PointA : PracticeStep.InsertRJ45PointB;

            case PracticeAction.Crimp:
                return isA ? PracticeStep.CrimpPointA : PracticeStep.CrimpPointB;

            case PracticeAction.LANTest:
                return PracticeStep.LANTest;

            default:
                return PracticeStep.Finished;
        }
    }
}
