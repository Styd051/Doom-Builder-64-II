namespace CodeImp.DoomBuilder.Rendering
{
	public enum ModelRenderMode
	{
		NONE,
		SELECTION,
		ACTIVE_THINGS_FILTER,
		ALL,
	}

	public enum LightRenderMode
	{
		NONE,
		ALL,
		ALL_ANIMATED,
	}

	// styd. How the visual mode shows the Doom 64 things with the Spawner flag, which are not in
	// the level when it starts: half see-through, not at all, or as any other thing
	public enum Doom64SpawnerMode
	{
		GHOST,
		HIDDEN,
		SHOWN,
	}

	public enum ThingRenderMode
	{
		NORMAL,
		MODEL,
		VOXEL,
		WALLSPRITE,
		FLATSPRITE,
	}
}
