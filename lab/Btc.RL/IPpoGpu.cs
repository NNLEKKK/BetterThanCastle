namespace Btc.RL;

/// <summary>
/// GPU backend of the PPO update (lab/Btc.Gpu). The GPU runs the actor/critic forward and backward passes; the trainer keeps the
/// PPO loss (per-row, on CPU: logits -> dLogits with the exact PpoLoss math) and Adam. Rows are addressed by their ordinal in
/// PpoTrainer.ValidOrig (the valid samples of the update before any shuffle).
/// </summary>
public interface IPpoGpu
{
    /// <summary>Uploads this update's samples (obs, central) and the current weights.</summary>
    void BeginUpdate(PpoTrainer t);
    /// <summary>Forward of the minibatch rows ord[0..rows): logits (rows x L) and values (rows).</summary>
    void Forward(int[] ord, int rows, float[] logits, float[] values);
    /// <summary>Backward from dLogits / dValues of the last Forward; writes the parameter gradients (overwrites gA, gC).</summary>
    void Backward(int rows, float[] dLogits, float[] dValues, float[] gA, float[] gC);
    /// <summary>Weights changed on the CPU (Adam step): upload them.</summary>
    void WeightsChanged(PpoTrainer t);
    /// <summary>Drops the device context and every buffer and creates them again (after a driver reset / lost context).</summary>
    void Reset();
    string Name { get; }
}
