namespace No1.Core;

using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; } = null!;

	public const string SceneTemple = "res://scenes/temple/temple_3d.tscn";
	public const string SceneWorld = "res://scenes/world/world_map.tscn";
	public const string SceneCombat = "res://scenes/combat/combat.tscn";
	public const string SceneMapUI = "res://scenes/map/map.tscn";

	public override void _Ready()
	{
		Instance = this;
		Combat.SkillData.Load();
		CallDeferred(nameof(LoadTemple));
	}

	void LoadTemple()
	{
		GetTree().ChangeSceneToFile(SceneTemple);
	}

	public void GoToScene(string path)
	{
		if (ResourceLoader.Exists(path))
			GetTree().ChangeSceneToFile(path);
		else
			GD.PrintErr($"Scene not found: {path}");
	}
}
