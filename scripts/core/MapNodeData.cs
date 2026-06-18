namespace No1.Core;

using Godot;

public class MapNodeData
{
	public string Name, Desc;
	public int[] Connections;

	public static MapNodeData[] LoadAll()
	{
		var file = FileAccess.Open("res://assets/data/map_nodes.json", FileAccess.ModeFlags.Read);
		if (file == null) return Array.Empty<MapNodeData>();

		var dict = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		var arr = dict["nodes"].AsGodotArray();
		var nodes = new MapNodeData[arr.Count];
		for (int i = 0; i < arr.Count; i++)
		{
			var d = arr[i].AsGodotDictionary();
			var conns = d["connections"].AsGodotArray();
			nodes[i] = new MapNodeData
			{
				Name = d["name"].AsString(),
				Desc = d["desc"].AsString(),
				Connections = Enumerable.Range(0, conns.Count).Select(j => (int)conns[j].AsDouble()).ToArray(),
			};
		}
		return nodes;
	}
}
