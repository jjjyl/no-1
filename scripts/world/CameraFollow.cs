namespace No1.World;

using Godot;

public partial class CameraFollow : Node
{
	[Export] public Node2D Target;
	[Export] public float FollowSpeed = 4f;
	[Export] public float LookAhead = 80f;

	Camera2D _cam;
	Vector2 _lastTargetPos;

	public override void _Ready()
	{
		_cam = GetParent<Camera2D>();
	}

	public override void _Process(double delta)
	{
		if (Target == null || _cam == null) return;

		var currentTargetPos = Target.GlobalPosition;
		var moveDir = (currentTargetPos - _lastTargetPos).Normalized();
		_lastTargetPos = currentTargetPos;

		// 视线前移
		var lookAheadOffset = moveDir * LookAhead;
		var desiredPos = currentTargetPos + lookAheadOffset;

		_cam.GlobalPosition = _cam.GlobalPosition.Lerp(desiredPos, (float)delta * FollowSpeed);
	}
}
