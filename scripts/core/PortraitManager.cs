namespace No1.Core;

using Godot;
using System.Collections.Generic;

/// <summary>
/// Static utility for loading character portraits by name + expression.
/// Falls back to colored placeholder when PNG is missing.
/// </summary>
public static class PortraitManager
{
	const string BasePath = "res://assets/portraits/";
	const int PlaceholderSize = 128;

	static readonly Dictionary<string, Color> _defaultColors = new()
	{
		["艾薇"] = Color.Color8(255, 181, 194),
		["天一"] = Color.Color8(139, 184, 232),
		["塞拉菲娜"] = Color.Color8(232, 213, 163),
		["真冬"] = Color.Color8(168, 216, 234),
		["阿斯特"] = Color.Color8(123, 104, 174),
		["静"] = Color.Color8(47, 72, 88),
	};

	static readonly Dictionary<string, ImageTexture> _placeholderCache = new();
	static readonly Dictionary<string, Dictionary<string, Texture2D>> _textureCache = new();

	public static readonly string[] DefaultExpressions = { "normal", "happy", "sad", "angry", "surprised" };

	/// <summary>
	/// Load a portrait texture for the given character and expression.
	/// Returns null if neither PNG nor placeholder color is available.
	/// </summary>
	public static Texture2D LoadPortrait(string charName, string expression = "normal")
	{
		if (string.IsNullOrEmpty(charName)) return null;

		// Try cached first
		if (_textureCache.TryGetValue(charName, out var expDict) &&
			expDict.TryGetValue(expression, out var cached))
			return cached;

		// Try loading PNG
		var path = $"{BasePath}{charName}/{expression}.png";
		if (ResourceLoader.Exists(path))
		{
			var tex = ResourceLoader.Load<Texture2D>(path);
			CacheTexture(charName, expression, tex);
			return tex;
		}

		// Try normal as fallback for missing expression
		if (expression != "normal")
			return LoadPortrait(charName, "normal");

		// Fallback: colored placeholder
		return CreatePlaceholder(charName);
	}

	/// <summary>
	/// Check if any portrait exists for this character (PNG or placeholder-able).
	/// </summary>
	public static bool HasPortrait(string charName)
	{
		if (string.IsNullOrEmpty(charName)) return false;
		if (ResourceLoader.Exists($"{BasePath}{charName}/normal.png")) return true;
		return _defaultColors.ContainsKey(charName);
	}

	/// <summary>
	/// Create a colored placeholder square with the character's initial.
	/// Cached per character name.
	/// </summary>
	public static ImageTexture CreatePlaceholder(string charName)
	{
		if (_placeholderCache.TryGetValue(charName, out var cached))
			return cached;

		var color = GetDefaultColor(charName);
		var img = Image.CreateEmpty(PlaceholderSize, PlaceholderSize, false, Image.Format.Rgba8);

		// Fill with color, add a subtle border
		for (int y = 0; y < PlaceholderSize; y++)
		{
			for (int x = 0; x < PlaceholderSize; x++)
			{
				bool isBorder = x < 4 || x >= PlaceholderSize - 4 || y < 4 || y >= PlaceholderSize - 4;
				img.SetPixel(x, y, isBorder ? Colors.White * 0.3f : color);
			}
		}

		var tex = ImageTexture.CreateFromImage(img);
		_placeholderCache[charName] = tex;
		return tex;
	}

	static Color GetDefaultColor(string charName)
	{
		if (_defaultColors.TryGetValue(charName, out var c)) return c;
		// Deterministic color from name hash
		uint hash = 0;
		foreach (var ch in charName) hash = hash * 31 + ch;
		return Color.FromHsv((hash % 360) / 360f, 0.5f, 0.7f);
	}

	static void CacheTexture(string charName, string expression, Texture2D tex)
	{
		if (!_textureCache.ContainsKey(charName))
			_textureCache[charName] = new();
		_textureCache[charName][expression] = tex;
	}

	/// <summary>
	/// Clear all caches (e.g., on scene change to free memory).
	/// </summary>
	public static void ClearCache()
	{
		_textureCache.Clear();
		_placeholderCache.Clear();
	}
}
