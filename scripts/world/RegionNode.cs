namespace No1.World;
using Godot;
using No1.Core;

public partial class RegionNode : Node3D
{
	[Signal]
	public delegate void CombatPendingEventHandler();

	public string RegionId;
	public string RegionName;
	public int RegionIndex;

	public void Initialize(RegionPlacement placement, int index)
	{
		RegionId = placement.Id;
		RegionName = placement.Name;
		RegionIndex = index;
		Position = new Vector3(placement.TileX * WorldConstants.TileSizeMeters, 0, placement.TileY * WorldConstants.TileSizeMeters);

		// Area3D trigger
		var area = new Area3D { Name = "Trigger" };
		var colShape = new CollisionShape3D();
		float halfSize = placement.Radius * WorldConstants.TileSizeMeters;
		colShape.Shape = new BoxShape3D { Size = new Vector3(halfSize * 2, 2f, halfSize * 2) };
		area.AddChild(colShape);
		area.BodyEntered += OnBodyEntered;
		AddChild(area);

		// Label3D billboard
		var label = new Label3D
		{
			Name = "Label",
			Text = RegionName,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(0, 1.5f, 0),
			FontSize = 48,
			OutlineSize = 2,
			Modulate = new Color(1, 1, 1, 0.7f)
		};
		AddChild(label);
	}

	void OnBodyEntered(Node3D body)
	{
		if (body is not Player3D) return;
		CycleManager.Instance.CurrentNodeIndex = RegionIndex;
		CycleManager.Instance.CurrentRegionIndex = RegionIndex;
		EventManager.CheckEvents(RegionName, CycleManager.Instance, _ => { });
		if (!string.IsNullOrEmpty(CycleManager.Instance.PendingEnemyScene))
			EmitSignal(SignalName.CombatPending);
	}
}
