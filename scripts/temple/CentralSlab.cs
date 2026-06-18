namespace No1.Temple;

using Godot;
using No1.Data;

public partial class CentralSlab : Node3D
{
	[Export] Area3D _grooveInsight;
	[Export] Area3D _grooveValor;
	[Export] Area3D _grooveWanderer;
	[Export] Area3D _slabCenter;

	[Export] OmniLight3D _glowInsight;
	[Export] OmniLight3D _glowValor;
	[Export] OmniLight3D _glowWanderer;

	[Export] Label3D _labelInsight;
	[Export] Label3D _labelValor;
	[Export] Label3D _labelWanderer;

	const float GlowBase = 0.3f;
	const float GlowHover = 0.9f;
	const float GlowSelected = 1.4f;
	const float GlowIdle = 0.15f;

	BlessingType? _selected;
	BlessingType? _hoveredGroove;
	Label3D _descLabel;

	public BlessingType? SelectedBlessing => _selected;

	public Area3D GrooveInsight => _grooveInsight;
	public Area3D GrooveValor => _grooveValor;
	public Area3D GrooveWanderer => _grooveWanderer;
	public Area3D SlabCenter => _slabCenter;

	[Signal] public delegate void BlessingSelectedEventHandler(BlessingType type);
	[Signal] public delegate void EnterWorldEventHandler();

	public override void _Ready()
	{
		const uint LayerGroove = 1;
		const uint LayerCenter = 2;

		if (_grooveInsight != null) _grooveInsight.CollisionLayer = LayerGroove;
		if (_grooveValor != null) _grooveValor.CollisionLayer = LayerGroove;
		if (_grooveWanderer != null) _grooveWanderer.CollisionLayer = LayerGroove;
		if (_slabCenter != null) _slabCenter.CollisionLayer = LayerCenter;

		_descLabel = new Label3D
		{
			Text = "",
			Position = new Vector3(0, 0.6f, 0),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 20,
			OutlineSize = 2,
			Modulate = new Color(1, 1, 0.85f),
			Visible = false,
			AutowrapMode = TextServer.AutowrapMode.Word,
			Width = 400
		};
		AddChild(_descLabel);

		ResetGlow();
	}

	public void HoverGroove(BlessingType? type)
	{
		if (_hoveredGroove == type) return;
		_hoveredGroove = type;

		SetLabel(_labelInsight, false);
		SetLabel(_labelValor, false);
		SetLabel(_labelWanderer, false);

		if (type == null)
		{
			SetGlow(_glowInsight, _selected == BlessingType.察知 ? GlowSelected : GlowBase);
			SetGlow(_glowValor, _selected == BlessingType.战意 ? GlowSelected : GlowBase);
			SetGlow(_glowWanderer, _selected == BlessingType.旅人 ? GlowSelected : GlowBase);
		}
		else
		{
			SetGlow(_glowInsight, type == BlessingType.察知 ? GlowHover : (_selected == BlessingType.察知 ? GlowSelected : GlowIdle));
			SetGlow(_glowValor, type == BlessingType.战意 ? GlowHover : (_selected == BlessingType.战意 ? GlowSelected : GlowIdle));
			SetGlow(_glowWanderer, type == BlessingType.旅人 ? GlowHover : (_selected == BlessingType.旅人 ? GlowSelected : GlowIdle));
			SetLabelByType(type.Value, true);
		}
	}

	public void ClickGroove(BlessingType type)
	{
		SelectBlessing(type);
	}

	public bool TryEnterWorld()
	{
		if (_selected == null) return false;
		GD.Print($"[CentralSlab] Enter world with blessing: {_selected}");
		EmitSignal(SignalName.EnterWorld);
		return true;
	}

	void SelectBlessing(BlessingType type)
	{
		_selected = type;
		_hoveredGroove = null;

		_glowInsight.LightEnergy = type == BlessingType.察知 ? GlowSelected : GlowIdle;
		_glowValor.LightEnergy = type == BlessingType.战意 ? GlowSelected : GlowIdle;
		_glowWanderer.LightEnergy = type == BlessingType.旅人 ? GlowSelected : GlowIdle;

		SetLabel(_labelInsight, false);
		SetLabel(_labelValor, false);
		SetLabel(_labelWanderer, false);

		var data = BlessingData.Get(type);
		_descLabel.Text = $"{data.Name}: {data.Description}";
		_descLabel.Visible = true;

		GD.Print($"[CentralSlab] Blessing selected: {type}");
		EmitSignal(SignalName.BlessingSelected, (int)type);
	}

	void ResetGlow()
	{
		if (_glowInsight != null) _glowInsight.LightEnergy = GlowBase;
		if (_glowValor != null) _glowValor.LightEnergy = GlowBase;
		if (_glowWanderer != null) _glowWanderer.LightEnergy = GlowBase;
	}

	static void SetGlow(OmniLight3D glow, float energy)
	{
		if (glow != null) glow.LightEnergy = energy;
	}

	static void SetLabel(Label3D label, bool visible)
	{
		if (label != null) label.Visible = visible;
	}

	void SetLabelByType(BlessingType type, bool visible)
	{
		var label = type switch
		{
			BlessingType.察知 => _labelInsight,
			BlessingType.战意 => _labelValor,
			BlessingType.旅人 => _labelWanderer,
			_ => null
		};
		SetLabel(label, visible);
	}
}
