using System;
using UnityEngine;

public class PracticeManager : MonoBehaviour
{
    [Header("Cable Ends")]
    [Tooltip("Dipakai hanya untuk mengenali ujung mana yang diklik. State-nya tidak pernah diubah dari sini.")]
    [SerializeField] private CablePoint pointA;
    [SerializeField] private CablePoint pointB;

    [Header("Practice")]
    [SerializeField] private PracticeStep startStep = PracticeStep.StripPointA;

    private PracticeState state;

    public event Action<PracticeStep> OnStepChanged;

    public PracticeStep CurrentStep => State.CurrentStep;

    public bool IsFinished => State.IsFinished;

    private PracticeState State
    {
        get
        {
            if (state == null)
                Configure(pointA, pointB, startStep);

            return state;
        }
    }

    private void Awake()
    {
        _ = State;
    }

    public void Configure(CablePoint a, CablePoint b, PracticeStep from = PracticeStep.StripPointA)
    {
        if (state != null)
            state.OnStepChanged -= HandleStepChanged;

        pointA = a;
        pointB = b;
        startStep = from;

        state = new PracticeState(from);
        state.OnStepChanged += HandleStepChanged;
    }


    public bool CanStrip(CablePoint point) => CanDo(PracticeAction.Strip, point);

    public bool CanInsertRJ45(CablePoint point) => CanDo(PracticeAction.InsertRJ45, point);

    public bool CanCrimp(CablePoint point) => CanDo(PracticeAction.Crimp, point);

    public bool CanTest() => PracticeValidator.IsAllowed(CurrentStep, PracticeAction.LANTest);

    private bool CanDo(PracticeAction action, CablePoint point)
    {
        return TryResolveSide(point, out PracticeSide side)
            && PracticeValidator.IsAllowed(CurrentStep, action, side);
    }

    private bool TryResolveSide(CablePoint point, out PracticeSide side)
    {
        if (point != null && point == pointA)
        {
            side = PracticeSide.A;
            return true;
        }

        if (point != null && point == pointB)
        {
            side = PracticeSide.B;
            return true;
        }

        side = default;
        return false;
    }

    public bool NextStep() => State.Advance();

    public void CompleteCurrentStep() => NextStep();

    public void ResetPractice() => State.Reset();

    private void HandleStepChanged(PracticeStep step)
    {
        OnStepChanged?.Invoke(step);

        Debug.Log($"Practice Step : {step}");
    }

#if UNITY_EDITOR
    [ContextMenu("Test/Next Step")]
    private void TestNextStep()
    {
        bool moved = NextStep();

        Debug.Log($"Result : {moved}");
        Debug.Log($"Step : {CurrentStep}");
    }

    [ContextMenu("Test/Reset Practice")]
    private void TestResetPractice()
    {
        ResetPractice();

        Debug.Log($"Step : {CurrentStep}");
    }
#endif
}
