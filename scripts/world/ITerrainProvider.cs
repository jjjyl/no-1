namespace No1.World;

/// <summary>
/// Abstract terrain shape provider.
/// Current: FlatTerrainProvider (Y=0 everywhere).
/// Future: NoiseTerrainProvider with FastNoiseLite for rolling hills.
/// </summary>
public interface ITerrainProvider
{
	float GetHeight(float x, float z);
}
