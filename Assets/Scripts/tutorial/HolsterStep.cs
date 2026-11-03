using UnityEngine;

/// <summary>Práctica del cinto: guardar la herramienta en su funda, volver a sacarla y guardarla otra vez.</summary>
public class HolsterStep : TutorialStep
{
    const int Stages = 3;

    public ToolHolster holster;
    public string dropHint = "Suéltalo cerca de tu cadera derecha, cuando la funda se ilumine";

    int stage;
    int storedAtStage;
    float nextHintTime;

    public override float Progress => (float)stage / Stages;
    public override bool IsComplete => stage >= Stages;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        stage = 0;
        storedAtStage = holster.StoredCount;
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        bool taking = stage == 1;
        if (taking ? !holster.IsHolstered : holster.StoredCount > storedAtStage)
        {
            stage++;
            storedAtStage = holster.StoredCount;
        }

        if (Time.time - holster.LastDropTime < 1f && Time.time >= nextHintTime)
        {
            Context.ShowHint(dropHint);
            nextHintTime = Time.time + 3.5f;
        }
    }
}
