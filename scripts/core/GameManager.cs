namespace No1.Core;

using Godot;
using No1.World;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; } = null!;

	public static WorldData CurrentWorldData { get; set; }

	public const string SceneTemple = "res://scenes/temple/temple_3d.tscn";
	public const string SceneWorld = "res://scenes/world/world_map.tscn";
	public const string SceneCombat = "res://scenes/combat/combat.tscn";

	public override void _Ready()
	{
		Instance = this;
		Combat.SkillData.Load();
		Combat.StatusDef.Load();
		CallDeferred(nameof(LoadStartScene));
	}

	void LoadStartScene()
	{
		var cm = CycleManager.Instance;
		if (cm.HasAccountFlag("scene:world") && cm.WorldSeed != 0)
		{
			string savePath = WorldSerializer.GetFilePath(cm.WorldSeed, cm.CurrentCycle, cm.OverridePaths);
			if (FileAccess.FileExists(savePath))
			{
				try
				{
					CurrentWorldData = WorldSerializer.Deserialize(savePath);
					GD.Print("[GameManager] Resuming world session");
					GoToScene(SceneWorld);
					return;
				}
				catch (System.Exception ex)
				{
					GD.PrintErr($"[GameManager] Failed to load world save: {ex.Message}");
				}
			}
		}
		cm.RemoveAccountFlag("scene:world");
		GetTree().ChangeSceneToFile(SceneTemple);
	}

	public void EnterWorld(ulong seed, string[] overridePaths = null)
	{
		CycleManager.Instance.EnterWorld(seed, overridePaths);

		int cycle = CycleManager.Instance.CurrentCycle;
		seed = CycleManager.Instance.WorldSeed;

		string savePath = WorldSerializer.GetFilePath(seed, cycle, overridePaths);

		if (WorldSerializer.Exists(seed, cycle, overridePaths))
		{
			CurrentWorldData = WorldSerializer.Deserialize(savePath);
			GD.Print($"[GameManager] Loaded world from save: {savePath}");
		}
		else
		{
			CurrentWorldData = WorldGenerator.Generate(seed, cycle, overridePaths);
			WorldSerializer.Serialize(CurrentWorldData, savePath);
			GD.Print($"[GameManager] Generated new world, saved to: {savePath}");
		}

		CycleManager.Instance.SetAccountFlag("scene:world");
		GoToScene(SceneWorld);
	}

	public void GoToScene(string path)
	{
		if (ResourceLoader.Exists(path))
			GetTree().ChangeSceneToFile(path);
		else
			GD.PrintErr($"Scene not found: {path}");
	}
}
