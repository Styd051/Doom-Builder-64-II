
#region ================== Namespaces

using System.Collections.Generic;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Data
{
	// styd. The light effects of the sectors of a Doom 64 map. In the game a sector has a light
	// level that is not in the map: it starts at nothing, an effect of the sector moves it in every
	// tic, and it is added to the textures of what the sector shows. This plays what the game does
	// from the start of a level, 30 tics in a second. The map is never changed.
	internal sealed class Doom64SectorLights
	{
		private const float STEP_TIME = 1000f / 30f;	// Milliseconds of a tic
		private const int MAX_STEPS = 8;				// Tics in one go, after a long wait
		private const string SYNC_FLAG = "8";			// "Sync Specials": the sector follows another one

		// What the game calls these
		private const int GLOWSPEED = 2;
		private const int STROBEBRIGHT = 3;
		private const int STROBEBRIGHT2 = 1;
		private const int FASTDARK = 15;
		private const int SLOWDARK = 30;
		private const int SEQUENCELIGHTMAX = 48;
		private const int PULSENORMAL = 0;
		private const int PULSESLOW = 1;
		private const int PULSERANDOM = 2;
		private const int SEQUENCE_EFFECT = 205;

		private enum Kind { Flash, Strobe, Glow, Fire, Sequence, Combine }

		// What moves the light of one sector
		private sealed class Thinker
		{
			public Kind Kind;
			public bool Removed;
			public int Sector;
			public int Special;		// The effect of the sector when this was made
			public int Count;		// Tics until this acts again
			public int MaxLight, MinLight;
			public int BrightTime, DarkTime;
			public int Direction;	// Of a glow: 1 up, -1 down. Of a sequence: 1 up, -1 down, 0 waiting
			public int Type;		// Of a glow
			public int Head;		// Of a sequence: the sector that starts it, or -1
			public int Combiner;	// Of a combine: the sector whose light is copied
		}

		// The lines of a sector, as the game lists them: the sector in front of each and the one behind
		private struct SectorLine
		{
			public int Front, Back;
		}

		private int[] levels;				// The light of each sector
		private int[] specials;				// The effect of each sector, as the game changes it while it plays
		private int[] tags;
		private List<SectorLine>[] lines;	// Only made when the map has a sequence
		private List<Thinker> thinkers;
		private int rndindex;
		private float time;					// Milliseconds since the last tic
		private long signature;				// Of the map that these lights were made for

		// The light that the effect of a sector adds, of 255
		public int GetLevel(Sector s)
		{
			if((levels == null) || (s == null) || (s.Index < 0) || (s.Index >= levels.Length)) return 0;
			return levels[s.Index];
		}

		// This lets the time go by. A map whose effects have changed starts again, as a level does.
		public void Advance(MapSet map, float milliseconds)
		{
			long now = Signature(map);
			if((levels == null) || (now != signature))
			{
				Spawn(map);
				signature = now;
			}

			time += milliseconds;
			int steps = (int)(time / STEP_TIME);
			time -= steps * STEP_TIME;
			for(int i = 0; (i < steps) && (i < MAX_STEPS); i++) Tick();
		}

		// What the lights depend on in a map: the effect, the tag and the "Sync Specials" flag of
		// every sector, and for a sequence which sectors the lines are between
		private static long Signature(MapSet map)
		{
			long h = 1469598103934665603L;
			bool sequence = false;
			unchecked
			{
				foreach(Sector s in map.Sectors)
				{
					h = (h ^ s.Effect) * 1099511628211L;
					h = (h ^ s.Tag) * 1099511628211L;
					h = (h ^ (s.IsFlagSet(SYNC_FLAG) ? 1 : 0)) * 1099511628211L;
					if(s.Effect == SEQUENCE_EFFECT) sequence = true;
				}
				h = (h ^ map.Sectors.Count) * 1099511628211L;

				if(sequence)
				{
					foreach(Linedef l in map.Linedefs)
					{
						h = (h ^ SectorOf(l.Front)) * 1099511628211L;
						h = (h ^ SectorOf(l.Back)) * 1099511628211L;
					}
				}
			}
			return h;
		}

		private static int SectorOf(Sidedef sd)
		{
			return (((sd != null) && (sd.Sector != null)) ? sd.Sector.Index : -1);
		}

		// A random number as the game makes them (P_Random)
		private int Random()
		{
			rndindex = (rndindex + 1) & 0xff;
			return Doom64SkyAnimation.RandomTable[rndindex];
		}

		// The start of a level (P_SpawnSpecials): every sector in turn gets what its effect asks for
		private void Spawn(MapSet map)
		{
			int count = map.Sectors.Count;
			levels = new int[count];
			specials = new int[count];
			tags = new int[count];
			bool[] sync = new bool[count];
			lines = null;
			thinkers = new List<Thinker>();
			rndindex = 0;
			time = 0f;

			bool sequence = false;
			foreach(Sector s in map.Sectors)
			{
				if((s.Index < 0) || (s.Index >= count)) continue;
				specials[s.Index] = s.Effect;
				tags[s.Index] = unchecked((short)s.Tag);	// (the game has tags from -32768 to 32767)
				sync[s.Index] = s.IsFlagSet(SYNC_FLAG);
				if(s.Effect == SEQUENCE_EFFECT) sequence = true;
			}

			if(sequence)
			{
				// The lines of every sector, in the order of the lines of the map (P_GroupLines)
				lines = new List<SectorLine>[count];
				for(int i = 0; i < count; i++) lines[i] = new List<SectorLine>();
				foreach(Linedef l in map.Linedefs)
				{
					SectorLine sl;
					sl.Front = SectorOf(l.Front);
					sl.Back = SectorOf(l.Back);
					if(sl.Front < 0) continue;
					lines[sl.Front].Add(sl);
					if((sl.Back >= 0) && (sl.Back != sl.Front)) lines[sl.Back].Add(sl);
				}
			}

			for(int i = 0; i < count; i++) AddSectorSpecial(i, sync[i]);
		}

		// P_AddSectorSpecial
		private void AddSectorSpecial(int sector, bool sync)
		{
			if(sync && (specials[sector] != 0))
			{
				CombineLightSpecials(sector);
				return;
			}

			switch(specials[sector])
			{
				case 1: SpawnLightFlash(sector); break;
				case 2: SpawnStrobeFlash(sector, FASTDARK); break;
				case 3: SpawnStrobeFlash(sector, SLOWDARK); break;
				case 8: SpawnGlowingLight(sector, PULSENORMAL); break;
				case 9: SpawnGlowingLight(sector, PULSESLOW); break;
				case 11: SpawnGlowingLight(sector, PULSERANDOM); break;
				case 17: SpawnFireFlicker(sector); break;
				case 202: SpawnStrobeAltFlash(sector, 3); break;
				case 204: SpawnStrobeFlash(sector, 7); break;
				case SEQUENCE_EFFECT: SpawnSequenceLight(sector, true); break;
				case 206: SpawnStrobeFlash(sector, 90); break;
				case 208: SpawnStrobeAltFlash(sector, 6); break;
			}
		}

		private Thinker AddThinker(Kind kind, int sector)
		{
			Thinker t = new Thinker();
			t.Kind = kind;
			t.Sector = sector;
			t.Special = specials[sector];
			t.Head = -1;
			thinkers.Add(t);
			return t;
		}

		// A tic of the game (P_RunThinkers): every thinker in the order they were made, those that
		// are made in this tic as well
		private void Tick()
		{
			bool removed = false;
			for(int i = 0; i < thinkers.Count; i++)
			{
				Thinker t = thinkers[i];
				if(t.Removed) { removed = true; continue; }

				switch(t.Kind)
				{
					case Kind.Flash: LightFlash(t); break;
					case Kind.Strobe: StrobeFlash(t); break;
					case Kind.Glow: Glow(t); break;
					case Kind.Fire: FireFlicker(t); break;
					case Kind.Sequence: SequenceGlow(t); break;
					case Kind.Combine: Combine(t); break;
				}
			}

			if(removed) thinkers.RemoveAll(delegate(Thinker t) { return t.Removed; });
		}

		// T_FireFlicker: every 3 tics, a light of 0 to 31
		private void FireFlicker(Thinker flick)
		{
			if(--flick.Count != 0) return;

			if(specials[flick.Sector] != flick.Special)
			{
				flick.Removed = true;
				return;
			}

			levels[flick.Sector] = Random() & 31;
			flick.Count = 3;
		}

		private void SpawnFireFlicker(int sector)
		{
			Thinker flick = AddThinker(Kind.Fire, sector);
			flick.Count = 3;
		}

		// T_Glow: the light goes up and down by 2 every 2 tics, between two levels
		private void Glow(Thinker g)
		{
			if(--g.Count != 0) return;

			if(specials[g.Sector] != g.Special)
			{
				g.Removed = true;
				return;
			}

			g.Count = 2;

			switch(g.Direction)
			{
				case -1:
					levels[g.Sector] -= GLOWSPEED;
					if(levels[g.Sector] < g.MinLight)
					{
						levels[g.Sector] = g.MinLight;
						if(g.Type == PULSERANDOM) g.MaxLight = (Random() & 31) + 17;
						g.Direction = 1;
					}
					break;

				case 1:
					levels[g.Sector] += GLOWSPEED;
					if(g.MaxLight < levels[g.Sector])
					{
						levels[g.Sector] = g.MaxLight;
						if(g.Type == PULSERANDOM) g.MinLight = (Random() & 15);
						g.Direction = -1;
					}
					break;
			}
		}

		private void SpawnGlowingLight(int sector, int type)
		{
			Thinker g = AddThinker(Kind.Glow, sector);
			g.Count = 2;
			g.Direction = 1;
			g.MinLight = 0;
			g.Type = type;
			g.MaxLight = ((type == PULSENORMAL) ? 32 : 48);
		}

		// T_LightFlash: the light is on for 1 or 33 tics, off for 1 to 8 tics
		private void LightFlash(Thinker flash)
		{
			if(--flash.Count != 0) return;

			if(specials[flash.Sector] != flash.Special)
			{
				flash.Removed = true;
				return;
			}

			if(levels[flash.Sector] == 32)
			{
				levels[flash.Sector] = 0;
				flash.Count = (Random() & 7) + 1;
			}
			else
			{
				levels[flash.Sector] = 32;
				flash.Count = (Random() & 32) + 1;
			}
		}

		private void SpawnLightFlash(int sector)
		{
			// The game has nothing special about this sector while it plays
			specials[sector] = 0;

			Thinker flash = AddThinker(Kind.Flash, sector);
			flash.Count = (Random() & 63) + 1;
		}

		// T_StrobeFlash: the light is on for some tics, off for others
		private void StrobeFlash(Thinker flash)
		{
			if(--flash.Count != 0) return;

			if(specials[flash.Sector] != flash.Special)
			{
				flash.Removed = true;
				return;
			}

			if(levels[flash.Sector] == 0)
			{
				levels[flash.Sector] = flash.MaxLight;
				flash.Count = flash.BrightTime;
			}
			else
			{
				levels[flash.Sector] = 0;
				flash.Count = flash.DarkTime;
			}
		}

		private void SpawnStrobeFlash(int sector, int darktime)
		{
			Thinker flash = AddThinker(Kind.Strobe, sector);
			flash.BrightTime = STROBEBRIGHT;
			flash.MaxLight = 16;
			flash.DarkTime = darktime;
			flash.Count = (Random() & 7) + 1;
		}

		private void SpawnStrobeAltFlash(int sector, int darktime)
		{
			Thinker flash = AddThinker(Kind.Strobe, sector);
			flash.BrightTime = STROBEBRIGHT2;
			flash.MaxLight = 127;
			flash.DarkTime = darktime;
			flash.Count = 1;
		}

		// T_SequenceGlow: the light of a sector goes up to its highest and back down, and on its way
		// up it starts the sectors next to it whose tag is one more. The first sector of a sequence
		// waits for the light of the sector that starts it.
		private void SequenceGlow(Thinker seq)
		{
			if(--seq.Count != 0) return;

			if(specials[seq.Sector] != seq.Special)
			{
				seq.Removed = true;
				return;
			}

			seq.Count = 1;

			switch(seq.Direction)
			{
				case -1:
					levels[seq.Sector] -= GLOWSPEED;
					if(levels[seq.Sector] > 0) return;

					levels[seq.Sector] = 0;

					if(seq.Head < 0)
					{
						specials[seq.Sector] = 0;
						seq.Removed = true;
						return;
					}

					seq.Direction = 0;
					break;

				case 0:
					if(levels[seq.Head] == 0) return;
					seq.Direction = 1;
					break;

				case 1:
					levels[seq.Sector] += GLOWSPEED;
					if(levels[seq.Sector] < (SEQUENCELIGHTMAX + 1))
					{
						if(levels[seq.Sector] != 8) return;

						foreach(SectorLine l in lines[seq.Sector])
						{
							int next = l.Back;
							if((next >= 0) && (specials[next] == 0) && (tags[next] == (tags[seq.Sector] + 1)))
							{
								specials[next] = specials[seq.Sector];
								SpawnSequenceLight(next, false);
							}
						}
					}
					else
					{
						levels[seq.Sector] = SEQUENCELIGHTMAX;
						seq.Direction = -1;
					}
					break;
			}
		}

		// P_SpawnSequenceLight. The sector that starts the first one is, among the sectors in front of
		// its lines, the first other sector with its tag; the game takes the last one it looked at
		// when there is none.
		private void SpawnSequenceLight(int sector, bool first)
		{
			int head = -1;

			if(first)
			{
				foreach(SectorLine l in lines[sector])
				{
					head = l.Front;
					if((head != sector) && (tags[sector] == tags[head])) break;
				}

				if(head < 0) return;
			}

			Thinker seq = AddThinker(Kind.Sequence, sector);
			seq.Count = 1;
			seq.Direction = ((head < 0) ? 1 : 0);
			seq.Head = head;
		}

		// T_Combine: the light of another sector, in every tic
		private void Combine(Thinker combine)
		{
			if(specials[combine.Sector] != combine.Special)
			{
				combine.Removed = true;
				return;
			}

			levels[combine.Sector] = levels[combine.Combiner];
		}

		// P_CombineLightSpecials: a sector with "Sync Specials" follows the sector of the first
		// thinker of the kind of its effect that was made before it, and has no light without one
		private void CombineLightSpecials(int sector)
		{
			Kind kind;
			switch(specials[sector])
			{
				case 1:
					kind = Kind.Flash;
					break;

				case 2:
				case 3:
				case 202:
				case 204:
				case 206:
				case 208:
					kind = Kind.Strobe;
					break;

				case 8:
				case 9:
				case 11:
					kind = Kind.Glow;
					break;

				case 17:
					kind = Kind.Fire;
					break;

				default:
					return;
			}

			for(int i = 0; i < thinkers.Count; i++)
			{
				if(thinkers[i].Removed || (thinkers[i].Kind != kind)) continue;

				int combiner = thinkers[i].Sector;
				Thinker combine = AddThinker(Kind.Combine, sector);
				combine.Combiner = combiner;
				return;
			}
		}
	}
}
