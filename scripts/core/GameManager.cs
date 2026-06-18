namespace No1.Core;

using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; } = null!;

	public override void _Ready()
	{
		Instance = this;
		Combat.SkillData.Load();
		CallDeferred(nameof(LoadTemple));
	}

	void LoadTemple()
	{
		GetTree().ChangeSceneToFile("res://scenes/temple/temple_3d.tscn");
	}

	public void GoToScene(string path)
	{
		if (ResourceLoader.Exists(path))
			GetTree().ChangeSceneToFile(path);
		else
			GD.PrintErr($"Scene not found: {path}");
	}
}
