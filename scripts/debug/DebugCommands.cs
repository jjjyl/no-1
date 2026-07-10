// =============================================================================
// DebugCommands.cs — Static command registry for the No1 debug system.
//
// Usage:
//   DebugCommands.Register("mycmd", (args, output) => output("Hello!"));
//   DebugCommands.Execute("mycmd arg1 arg2", Console.WriteLine);
//   string result = DebugCommands.ExecuteToString("help");
//
// Built-in commands: help, unlock_nodes, give, set_stat, set_fatigue, god, tp
// =============================================================================

using No1.Core;
using No1.Data;
using No1.World;

namespace No1.Debug;

public static class DebugCommands
{
    // ── Registry ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, Action<string[], Action<string>>> _commands = new();

    /// <summary>God mode flag — toggled via the `god` command.</summary>
    public static bool GodMode { get; private set; }

    /// <summary>
    /// Register a custom debug command.  Throws if a command with the same
    /// name (case-insensitive) is already registered.
    /// </summary>
    public static void Register(string name, Action<string[], Action<string>> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name must not be empty.", nameof(name));

        string key = name.ToLowerInvariant();
        _commands[key] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    // ── Built-in commands (static constructor) ────────────────────────────

    static DebugCommands()
    {
        // ── help ──────────────────────────────────────────────────────────
        Register("help", (args, output) =>
        {
            var names = _commands.Keys.OrderBy(k => k);
            output("Available commands: " + string.Join(", ", names));
        });

        // ── unlock_nodes ──────────────────────────────────────────────────
        Register("unlock_nodes", (args, output) =>
        {
            var cm = CycleManager.Instance;
            if (cm == null)
            {
                output("CycleManager not available.");
                return;
            }
            cm.SetFlag("all_nodes_unlocked");
            output("All map nodes unlocked.");
        });

        // ── give <item_id> [count] ────────────────────────────────────────
        Register("give", (args, output) =>
        {
            if (args.Length == 0)
            {
                output("Usage: give <item_id> [count]");
                return;
            }

            string itemId = args[0];
            int count = 1;
            if (args.Length > 1 && int.TryParse(args[1], out int parsed))
                count = parsed;

            var def = ItemDef.Get(itemId);
            if (def == null)
            {
                output($"Unknown item: {itemId}");
                return;
            }

            var cm = CycleManager.Instance;
            if (cm == null || cm.PlayerInventory == null)
            {
                output("Inventory not available.");
                return;
            }

            cm.PlayerInventory.AddItem(itemId, count);
            output($"Gave {count}x {itemId}");
        });

        // ── set_stat <stat_name> <value> ──────────────────────────────────
        Register("set_stat", (args, output) =>
        {
            if (args.Length < 2)
            {
                output("Usage: set_stat <stat_name> <value>");
                return;
            }

            if (!int.TryParse(args[1], out int value))
            {
                output($"Invalid value: {args[1]}");
                return;
            }

            var cm = CycleManager.Instance;
            if (cm == null || cm.PlayerStats == null)
            {
                output("Player stats not available.");
                return;
            }

            var stats = cm.PlayerStats;
            switch (args[0].ToLowerInvariant())
            {
                case "power":   case "力": stats.Power   = value; break;
                case "body":    case "体": stats.Body    = value; break;
                case "agility": case "敏": stats.Agility = value; break;
                case "heart":   case "心": stats.Heart   = value; break;
                case "fortune": case "运": stats.Fortune = value; break;
                default:
                    output($"Unknown stat: {args[0]}");
                    return;
            }

            output($"Set {args[0]} to {value}");
        });

        // ── set_fatigue <0-100> ───────────────────────────────────────────
        Register("set_fatigue", (args, output) =>
        {
            output("Fatigue system not yet implemented.");
        });

        // ── god ───────────────────────────────────────────────────────────
        Register("god", (args, output) =>
        {
            GodMode = !GodMode;
            output(GodMode ? "God mode: ON" : "God mode: OFF");
        });

        // ── tp <node_id> ──────────────────────────────────────────────────
        Register("tp", (args, output) =>
        {
            if (args.Length == 0)
            {
                output("Usage: tp <region_id>");
                return;
            }

            string regionId = args[0];

            var sceneTree = Engine.GetMainLoop() as SceneTree;
            if (sceneTree == null)
            {
                output("Not running in a scene tree.");
                return;
            }

            var worldMap = sceneTree.Root.FindChild("WorldMap3D", true, false) as WorldMap3D;
            if (worldMap == null)
            {
                output("World map not loaded.");
                return;
            }

            var regionsParent = worldMap.FindChild("Regions", false, false);
            if (regionsParent == null)
            {
                output("No region nodes found.");
                return;
            }

            RegionNode targetRegion = null;
            foreach (var child in regionsParent.GetChildren())
            {
                if (child is RegionNode rn && rn.RegionId == regionId)
                {
                    targetRegion = rn;
                    break;
                }
            }

            if (targetRegion == null)
            {
                output($"Unknown node: {regionId}");
                return;
            }

            var player = worldMap.FindChild("Player", false, false) as Node3D;
            if (player == null)
            {
                output("Player not found on world map.");
                return;
            }

            player.GlobalPosition = targetRegion.GlobalPosition;
            output($"Teleported to {regionId}");
        });

        // ── quit ─────────────────────────────────────────────────────────
        Register("quit", (args, output) =>
        {
            output("Saving...");
            CycleManager.Instance?.SaveAccount();
            output("Quitting...");
            ((SceneTree)Engine.GetMainLoop()).Quit();
        });
    }

    // ── Execution ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parse <paramref name="input"/> and dispatch to the matching handler.
    /// Format: first word = command name, remaining words = arguments.
    /// </summary>
    public static void Execute(string input, Action<string> output)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            output("Type 'help' for available commands.");
            return;
        }

        string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string commandName = parts[0].ToLowerInvariant();
        string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        if (_commands.TryGetValue(commandName, out var handler))
        {
            handler(args, output);
        }
        else
        {
            output($"Unknown command: {commandName}. Type 'help' for available commands.");
        }
    }

    /// <summary>
    /// Convenience wrapper that calls <see cref="Execute"/> and returns all
    /// output lines joined with newlines.
    /// </summary>
    public static string ExecuteToString(string input)
    {
        var lines = new List<string>();
        Execute(input, line => lines.Add(line));
        return string.Join("\n", lines);
    }
}
