using Btc.Bots;
using Btc.Shared;

namespace Btc.RL.Duel;

/// <summary>
/// One opponent kind. Scripted (Bot != null): the mod's bot runs inside SimBotHost, the trainer-side IOpponent is a no-op.
/// External (Bot == null): the env applies the IOpponent's actions for side 1 (self-play snapshot / frozen .btcpol).
/// </summary>
public sealed class DuelOppDef
{
    public string Name;
    public Func<SimSideSpec> Bot;        // fresh spec per episode; null = External
    public Func<IOpponent> Create;       // trainer-side driver (NullOpponent for scripted bots)
    public bool External => Bot == null;
    public OpponentSpec Spec => new OpponentSpec(Name, Create);
}

/// <summary>Scripted sides need no actions from the trainer.</summary>
public sealed class NullOpponent : IOpponent
{
    public void Act(IEnvBatch env, int e, int side, Rng rng, int[] actionsOut) { Array.Clear(actionsOut); }
}

public static class DuelOpponents
{
    public const string Help = "prod0..prod4 (production bot tier), v1, v2, x, hunter, hunter2 (fair: respects the safe rule), spread, sweeper, baiter, clump, idle, ext (External, Continue only), self, <file>.btcpol, lumina:<file>.btcpol (mod controller)";

    static SimSideSpec Auto(int kind, int tier = 4) => SimSideSpec.AutoPlayer(1, kind, tier);

    /// <summary>Resolves a name. self = frozen snapshot behind selfRef (sampled). sample: frozen .btcpol opponents sample instead of argmax.</summary>
    public static DuelOppDef Get(string name, PolicyRef selfRef = null, bool sample = false, bool luminaArgmax = false)
    {
        Func<IOpponent> nul = () => new NullOpponent();
        DuelOppDef S(Func<SimSideSpec> f) => new DuelOppDef { Name = name, Bot = f, Create = nul };
        if (name.Length == 5 && name.StartsWith("prod") && char.IsDigit(name[4]))
        {
            int tier = name[4] - '0';
            if (tier > 4) throw new ArgumentException("prod tier 0..4");
            return S(() => SimSideSpec.Production(tier));
        }
        switch (name)
        {
            case "v1": return S(() => Auto(SimSideSpec.KindV1));
            case "v2": return S(() => Auto(SimSideSpec.KindV2));
            case "x": return S(() => Auto(SimSideSpec.KindX));
            case "hunter": return S(() => Auto(SimSideSpec.KindHunter));
            case "spread": return S(() => Auto(SimSideSpec.KindSpread));
            case "sweeper": return S(() => Auto(SimSideSpec.KindSweeper));
            case "baiter": return S(() => Auto(SimSideSpec.KindBaiter));
            case "clump": return S(() => Auto(SimSideSpec.KindClump));
            case "presser": return S(() => Auto(SimSideSpec.KindPresser));
            case "hunter2": return S(() => Auto(SimSideSpec.KindHunter2));
            case "idle": return S(() => new SimSideSpec());
            case "ext": return new DuelOppDef { Name = name, Bot = null, Create = nul };
            case "self":
                if (selfRef == null) throw new ArgumentException("self needs a policy reference");
                return new DuelOppDef { Name = name, Bot = null, Create = () => new PolicyOpponent(selfRef, true) };
        }
        if (name.StartsWith("lumina:"))
        {
            var d = Lumina(LoadPolicy(name[7..]), luminaArgmax);
            d.Name = name;
            return d;
        }
        if (name.EndsWith(".btcpol", StringComparison.OrdinalIgnoreCase))
        {
            var r = new PolicyRef(LoadPolicy(name));
            return new DuelOppDef { Name = Path.GetFileNameWithoutExtension(name), Bot = null, Create = () => new PolicyOpponent(r, sample) };
        }
        throw new ArgumentException("unknown duel opponent '" + name + "'; known: " + Help);
    }

    public static DuelOppDef[] Parse(string list, PolicyRef selfRef = null, bool sample = false)
        => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(n => Get(n, selfRef, sample)).ToArray();

    /// <summary>
    /// The shipped controller BtcDuelBotLuminaZero (scripted side inside the host) with a given policy object. argmax=false samples
    /// (the default the mod uses). BtcDuelBotLuminaZero.Argmax/Policy are [ThreadStatic] in SIM builds and read in its constructor,
    /// which runs inside Host.Begin on the thread that resets the env; the spec factory below runs on that same thread right before,
    /// so every worker thread sets its own flag for every episode.
    /// </summary>
    public static DuelOppDef Lumina(MlpPolicy pol, bool argmax, string name = "lumina")
    {
        return new DuelOppDef
        {
            Name = name,
            Bot = () => { LuminaArgmax = argmax; return new SimSideSpec { Kind = SimSideSpec.KindLumina, Policy = pol }; },
            Create = () => new NullOpponent(),
        };
    }

    static readonly System.Reflection.FieldInfo _argmaxField =
        typeof(SimBotHost).Assembly.GetType("BetterThanCastle.BtcDuelBotLuminaZero")?.GetField("Argmax",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    /// <summary>BtcDuelBotLuminaZero.Argmax of the CURRENT thread (internal [ThreadStatic] field of Btc.Bots, reached by reflection).</summary>
    public static bool LuminaArgmax
    {
        get => _argmaxField != null ? (bool)_argmaxField.GetValue(null)! : throw new MissingFieldException("BtcDuelBotLuminaZero.Argmax");
        set { if (_argmaxField == null) throw new MissingFieldException("BtcDuelBotLuminaZero.Argmax"); _argmaxField.SetValue(null, value); }
    }

    /// <summary>
    /// "prod4:3,prod2:1,pool:4" -> names + weights (missing weight = 1). The weight is the text after the LAST ':' when it parses as a
    /// number, so "lumina:C:\p.btcpol" (no weight) and "lumina:C:\p.btcpol:2" both work.
    /// </summary>
    public static (string[] names, float[] weights) ParseMix(string list)
    {
        var ns = new List<string>(); var ws = new List<float>();
        foreach (var part in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int c = part.LastIndexOf(':');
            if (c > 0 && float.TryParse(part[(c + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w))
            {
                if (w < 0f) throw new ArgumentException("negative opponent weight: " + part);
                ns.Add(part[..c]); ws.Add(w);
            }
            else { ns.Add(part); ws.Add(1f); }
        }
        if (ns.Count == 0) throw new ArgumentException("empty opponent list");
        return (ns.ToArray(), ws.ToArray());
    }

    public static MlpPolicy LoadPolicy(string path)
    {
        var p = MlpPolicy.Load(File.ReadAllBytes(path));
        if (p.SchemaHash != MlpPolicy.Fnv1a(DuelObs.Schema)) throw new InvalidDataException(path + ": policy schema hash does not match " + DuelObs.Schema);
        return p;
    }
}
