using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
	struct Note
	{
		/// <summary>
		/// True if the note is a rest
		/// </summary>
		public bool IsRest;
		/// <summary>
		/// True if the note uses pitch modulation
		/// </summary>
		public bool UsePitchModulation;
		/// <summary>
		/// True if the note uses noise
		/// </summary>
		public bool UseNoise;
		/// <summary>
		/// True if the note uses echo.
		/// </summary>
		public bool UseEcho;
		/// <summary>
		/// Gets the note length in milliseconds.
		/// </summary>
		public int NoteLength;
		/// <summary>
		/// Gets the sample used for the note
		/// </summary>
		public int Sample;
		/// <summary>
		/// Gets all pitch changes captured
		/// </summary>
		public int[] PitchCache;
		/// <summary>
		/// Gets all volume changes captured.
		/// </summary>
		public int[] VolumeCache;
		/// <summary>
		/// Gets all GAIN changes captured.
		/// </summary>
		public int[] GainCache;
		/// <summary>
		/// Contains special events.
		/// </summary>
		public NoteEvent[] Events;

		public override int GetHashCode()
		{
			int hash = 0;
			hash ^= IsRest.GetHashCode();
			hash ^= UsePitchModulation.GetHashCode();
			hash ^= UseNoise.GetHashCode();
			hash ^= NoteLength.GetHashCode();
			hash ^= Sample.GetHashCode();
			return hash;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Note))
			{
				return false;
			}

			Note note = (Note)obj;


			if (note.IsRest != this.IsRest) { return false; }
			if (note.UsePitchModulation != this.UsePitchModulation) { return false; }
			if (note.UseNoise != this.UseNoise) { return false; }
			if (note.UseEcho != this.UseEcho) { return false; }
			if (note.NoteLength != this.NoteLength) { return false; }
			if (note.Sample != this.Sample) { return false; }

			if (!checkArray(ref note.PitchCache, ref this.PitchCache)) { return false; }
			if (!checkArray(ref note.VolumeCache, ref this.VolumeCache)) { return false; }
			if (!checkArray(ref note.GainCache, ref this.GainCache)) { return false; }
			if (!checkArray(ref note.Events, ref this.Events)) { return false; }
			return true;
		}

		private static bool checkArray<T>(ref T[] a, ref T[] b)
		{
			if (a == null && b == null)
			{
				return true;
			}
			if (a == null || b == null)
			{
				return false;
			}
			return a.SequenceEqual(b);
		}

		public static bool operator ==(Note a, Note b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Note a, Note b)
		{
			return !a.Equals(b);
		}
	}

	enum NoteEventType : byte
	{
		PitchModulationUpdate = 0,
		MasterVolumeUpdate = 1,
		EchoVolumeUpdate = 2,
		NoiseUpdate = 3,
		EchoFeedbackUpdate = 4,
		EchoDelayUpdate = 5,
		EchoEnableUpdate = 6,
		EchoFirFilterUpdate = 7,
		EchoSync = 8,
		DisableEcho = 9,
		EnableEcho = 10,
	}

	struct NoteEvent
	{
		public NoteEvent(NoteEventType type, params int[] data) : this()
		{
			EventType = type;
			EventData = data;
		}

		public NoteEventType EventType { get; private set; }
		public int[] EventData { get; private set; }

		public override bool Equals(object obj)
		{
			if (!(obj is NoteEvent) || ((NoteEvent)obj).EventType != this.EventType)
			{
				return false;
			}
			return ((NoteEvent)obj).EventData.SequenceEqual(this.EventData);
		}

		public override int GetHashCode()
		{
			return EventType.GetHashCode() ^ EventData.Sum();
		}

		public static bool operator ==(NoteEvent a, NoteEvent b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(NoteEvent a, NoteEvent b)
		{
			return !a.Equals(b);
		}
	}
}
