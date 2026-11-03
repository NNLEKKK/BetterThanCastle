using System.Globalization;
using System.Reflection;
using System.Text;

namespace Btc.RL;

/// <summary>PPO / MAPPO hyper-parameters. Serialized as "key=value" lines into checkpoints.</summary>
public sealed class PpoConfig
{
    public int NumEnvs = 64;
    public int Steps = 128;          // decision steps per env per update
    public int Epochs = 4;
    public int Minibatches = 4;
    public float Gamma = 0.997f;
    public float Lambda = 0.95f;
    public float Clip = 0.2f;
    public float VClip = 0.2f;       // <= 0 disables value clipping (value-space units)
    public float EntCoef = 0.01f;    // entropy coefficient at step 0
    public float EntEnd = -1f;       // < 0: constant EntCoef; else linear EntCoef -> EntEnd over EntAnneal env steps, then EntEnd
    public long EntAnneal = 0;
    public bool DeadCredit = true;   // agents that die before the episode ends get the team's later rewards (see PpoTrainer.CreditDeadAgents)
    public float VfCoef = 1f;        // critic is a separate net; this only scales its loss
    public float Lr = 3e-4f;
    public float CriticLr = 1e-3f;
    public float MaxGradNorm = 0.5f;
    public float TargetKl = 0.03f;   // <= 0 disables KL early stop (stop at 1.5x)
    public string Hidden = "128,128";
    public string CriticHidden = "128,128";
    public string Activation = "tanh";
    public int Threads = Environment.ProcessorCount;
    public long Seed = 1;
    public long TotalSteps = 0;      // env decision steps; >0 enables linear LR anneal
    public bool LrAnneal = true;
    public float LrFloor = 0.1f;     // anneal never goes below Lr*LrFloor
    public bool NormAdv = true;
    public float KlAdapt = 0f;       // > 0: adaptive lr by the mean KL of each update (target KL); 0 = fixed lr
    public float KlAdaptMin = 0.1f;  // lr multiplier bounds
    public float KlAdaptMax = 10f;
    public bool KlAdaptCritic = false; // also scale the critic lr

    public float EntAt(long step)
    {
        if (EntEnd < 0f || EntAnneal <= 0) return EntCoef;
        float f = (float)Math.Min(1.0, (double)step / EntAnneal);
        return EntCoef + (EntEnd - EntCoef) * f;
    }

    /// <summary>"256" + layers 3 -> "256,256,256"; a comma list is kept as is.</summary>
    public static string Layers(string hidden, int layers)
    {
        if (hidden.Contains(',') || layers <= 0) return hidden;
        return string.Join(",", Enumerable.Repeat(hidden.Trim(), layers));
    }

    public int[] HiddenSizes => Parse(Hidden);
    public int[] CriticHiddenSizes => Parse(CriticHidden);
    public Act HiddenAct => Activation == "relu" ? Act.Relu : Act.Tanh;

    static int[] Parse(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<int>();
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var r = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++) r[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
        return r;
    }

    public string ToText()
    {
        var sb = new StringBuilder();
        foreach (var f in typeof(PpoConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            sb.Append(f.Name).Append('=').Append(Convert.ToString(f.GetValue(this), CultureInfo.InvariantCulture)).Append('\n');
        return sb.ToString();
    }

    public static PpoConfig FromText(string text)
    {
        var c = new PpoConfig();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            c.Set(line[..eq], line[(eq + 1)..]);
        }
        return c;
    }

    /// <summary>Sets a field by (case-insensitive) name; returns false if unknown.</summary>
    public bool Set(string name, string value)
    {
        foreach (var f in typeof(PpoConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            f.SetValue(this, Convert.ChangeType(value, f.FieldType, CultureInfo.InvariantCulture));
            return true;
        }
        return false;
    }
}
