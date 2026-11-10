using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ScamArtistMod
{
	/// <summary>One company the mod's scams can be addressed to.</summary>
	internal sealed class Company
	{
		public readonly string Name;
		public readonly float Pay;     // payout multiplier
		public readonly float Check;   // odds multiplier (lower = they check harder)
		public Company(string name, float pay, float check) { Name = name; Pay = pay; Check = check; }

		// Businesses of the Bazaar, the district the game's scam listener lives in.
		internal static readonly Company[] All =
		{
			new Company("Chamber of Commerce", 1.6f, 0.75f),
			new Company("Power Company", 1.5f, 0.8f),
			new Company("Casino", 1.4f, 0.85f),
			new Company("Mallment", 1.3f, 0.9f),
			new Company("Ankea", 1.2f, 0.95f),
			new Company("Clock Tower Offices", 1.0f, 1.0f),
			new Company("Storage Building", 0.9f, 1.05f),
			new Company("Restaurant", 0.8f, 1.1f),
			new Company("Pontikka", 0.8f, 1.1f),
			new Company("Beauty Salon", 0.7f, 1.15f),
		};

		internal static Company ByName(string name)
		{
			foreach (var c in All) if (c.Name == name) return c;
			return All[5];
		}

		// Each invoice is addressed to a company picked from its quality, so the same invoice always shows
		// the same company and different invoices spread over the list.
		internal static Company ForQuality(float quality)
		{
			unchecked
			{
				uint h = (uint)Mathf.RoundToInt(quality * 10000f) * 2654435761u;
				h ^= h >> 15;
				return All[(int)(h % (uint)All.Length)];
			}
		}
	}

	internal enum ScamMode { Small = 0, Normal = 1, Greedy = 2 }

	internal enum ScamState { Pending, Review, Docs, Investigation, Paid, Failed, Dropped, Charged }

	/// <summary>One scam in progress, saved as text.</summary>
	internal sealed class Scam
	{
		public int Id;
		public string Company;
		public float Quality;
		public ScamMode Mode;
		public float Chance;      // odds when sent
		public float MaxPay;      // biggest possible payout in RM
		public string Bank;       // where the money goes ("" = cash)
		public int SentAt;        // game clock in minutes
		public int DueAt;         // next step at this time
		public ScamState State;
		public float Required;    // quality needed for a follow-up (Docs)
		public int Severity;      // investigation
		public bool Reviewed;
		public bool BribeTried;
		public int LayLowSince;

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		public override string ToString()
		{
			return string.Join(";", new[]
			{
				Id.ToString(Inv), Company.Replace(";", ","), Quality.ToString("0.####", Inv), ((int)Mode).ToString(Inv),
				Chance.ToString("0.####", Inv), MaxPay.ToString("0.##", Inv), (Bank ?? "").Replace(";", ","), SentAt.ToString(Inv),
				DueAt.ToString(Inv), ((int)State).ToString(Inv), Required.ToString("0.####", Inv), Severity.ToString(Inv),
				Reviewed ? "1" : "0", BribeTried ? "1" : "0", LayLowSince.ToString(Inv)
			});
		}

		public static Scam Parse(string s)
		{
			string[] p = s.Split(';');
			if (p.Length < 15) return null;
			try
			{
				return new Scam
				{
					Id = int.Parse(p[0], Inv), Company = p[1], Quality = float.Parse(p[2], Inv), Mode = (ScamMode)int.Parse(p[3], Inv),
					Chance = float.Parse(p[4], Inv), MaxPay = float.Parse(p[5], Inv), Bank = p[6], SentAt = int.Parse(p[7], Inv),
					DueAt = int.Parse(p[8], Inv), State = (ScamState)int.Parse(p[9], Inv), Required = float.Parse(p[10], Inv),
					Severity = int.Parse(p[11], Inv), Reviewed = p[12] == "1", BribeTried = p[13] == "1", LayLowSince = int.Parse(p[14], Inv)
				};
			}
			catch (Exception) { return null; }
		}

		public bool Open { get { return State == ScamState.Pending || State == ScamState.Review || State == ScamState.Docs || State == ScamState.Investigation; } }
	}

	public class ScamArtistSave : SkillSaveBase
	{
		protected override string SaveGuid { get { return "JuanBerta_ScamArtist_Save_001"; } }
		protected override string SaveTitle { get { return "Scam Artist"; } }

		public string scams = "";        // open and recent scams
		public string letters = "";      // recent letters: "clock~title~text|..."
		public string sends = "";        // "company@clock|..." for suspicion
		public float bankMoved = 0f;     // RM paid into bank accounts by scams
		public int perkUsedAt = -100000; // Con Artist perk
		public int lastCrimeAt = -100000;
		public int nextId = 1;
		public int mode = 1;

		[NonSerialized] private List<Scam> list;
		[NonSerialized] private string listFrom;

		internal List<Scam> Scams()
		{
			if (list != null && listFrom == scams) return list;
			list = new List<Scam>();
			foreach (string s in (scams ?? "").Split('|'))
			{
				if (s.Length == 0) continue;
				Scam sc = Scam.Parse(s);
				if (sc != null) list.Add(sc);
			}
			listFrom = scams;
			return list;
		}

		internal void Store()
		{
			var keep = new List<Scam>();
			var done = new List<Scam>();
			foreach (Scam s in Scams()) (s.Open ? keep : done).Add(s);
			// keep the 15 latest finished scams for the ledger
			int skip = Mathf.Max(0, done.Count - 15);
			for (int i = skip; i < done.Count; i++) keep.Add(done[i]);
			keep.Sort((a, b) => a.Id.CompareTo(b.Id));
			var parts = new List<string>();
			foreach (Scam s in keep) parts.Add(s.ToString());
			scams = string.Join("|", parts.ToArray());
			list = keep;
			listFrom = scams;
		}

		internal void AddLetter(int clock, string title, string text)
		{
			var parts = new List<string>((letters ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
			parts.Add(clock.ToString(CultureInfo.InvariantCulture) + "~" + title.Replace("|", "/").Replace("~", "-") + "~" + text.Replace("|", "/").Replace("~", "-"));
			while (parts.Count > 20) parts.RemoveAt(0);
			letters = string.Join("|", parts.ToArray());
		}

		internal List<string[]> Letters()
		{
			var r = new List<string[]>();
			foreach (string p in (letters ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string[] f = p.Split('~');
				if (f.Length == 3) r.Add(f);
			}
			return r;
		}

		// Scams sent to this company in the last week.
		internal int Suspicion(string company, int now, int window)
		{
			int n = 0;
			var keep = new List<string>();
			foreach (string p in (sends ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
			{
				int at = p.LastIndexOf('@');
				int t;
				if (at <= 0 || !int.TryParse(p.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) continue;
				if (now - t > window) continue;
				keep.Add(p);
				if (p.Substring(0, at) == company) n++;
			}
			sends = string.Join("|", keep.ToArray());
			return n;
		}

		internal void AddSend(string company, int now)
		{
			sends = (string.IsNullOrEmpty(sends) ? "" : sends + "|") + company + "@" + now.ToString(CultureInfo.InvariantCulture);
		}

		public override void ResetSkill(string owner)
		{
			base.ResetSkill(owner);
			scams = ""; letters = ""; sends = ""; bankMoved = 0f; perkUsedAt = -100000; lastCrimeAt = -100000; nextId = 1; mode = 1;
			list = null;
		}
	}
}
