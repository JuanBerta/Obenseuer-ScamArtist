using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Configuration;
using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace ScamArtistMod
{
	/// <summary>
	/// Scam Artist: a Fraud skill, and scam invoices that play out over days. The game's own scam listener
	/// (FakeInvoiceReceiver, in every scene) still receives the mail; the mod replaces what happens next.
	/// </summary>
	[BepInPlugin("JuanBerta.Obenseuer.ScamArtist", "Scam Artist", "1.0.0")]
	public class ScamArtistPlugin : BaseUnityPlugin
	{
		internal static CustomSkill Skill;
		internal static ScamArtistSave Save { get { return SkillHost.Save as ScamArtistSave; } }

		internal static ConfigEntry<KeyboardShortcut> LedgerKey;
		internal static ConfigEntry<bool> ShowTooltip;
		internal static ConfigEntry<bool> ShowAtMailbox;
		internal static ConfigEntry<float> XpPerSuccess;
		internal static ConfigEntry<float> XpPerFailure;
		internal static ConfigEntry<float> MaxDrunkXpBonus;
		internal static ConfigEntry<float> ChancePerLevel;
		internal static ConfigEntry<float> PayoutPerLevel;
		internal static ConfigEntry<float> MinPayoutShare;
		internal static ConfigEntry<float> SuspicionPenalty;
		internal static ConfigEntry<int> SuspicionDays;
		internal static ConfigEntry<float> DrunkPenalty;
		internal static ConfigEntry<float> MinReplyDays;
		internal static ConfigEntry<float> MaxReplyDays;
		internal static ConfigEntry<float> ReviewChance;
		internal static ConfigEntry<float> DocsChance;
		internal static ConfigEntry<int> DocsDays;
		internal static ConfigEntry<int> InvestigationDays;
		internal static ConfigEntry<float> LayLowDropChance;
		internal static ConfigEntry<float> BribeCostPerSeverity;
		internal static ConfigEntry<float> PayBackShare;
		internal static ConfigEntry<float> BaseFreezeChance;
		internal static ConfigEntry<float> FreezeGrowthPer100Rm;
		internal static ConfigEntry<bool> ConArtist;
		internal static ConfigEntry<int> ConArtistDays;

		private void Awake()
		{
			Skill = new CustomSkill("Fraud");
			Skill.BindCommon(Config);
			Bind(Config);
			Skill.CardNote = CardNote;
			SkillHost.Init(this, Logger, Skill, "ScamArtist_Save", go => go.AddComponent<ScamArtistSave>());
			Harmony.CreateAndPatchAll(typeof(Hooks));
			Logger.LogInfo("Scam Artist loaded.");
		}

		private static ConfigDescription D(string t, int o, AcceptableValueBase r = null) { return CustomSkill.D(t, o, r); }

		private static void Bind(ConfigFile cfg)
		{
			LedgerKey = cfg.Bind("General", "LedgerKey", new KeyboardShortcut(KeyCode.F7), D("Opens the scam ledger: your scams, their letters, and what you can do about investigations.", 100));
			ShowTooltip = cfg.Bind("General", "ShowOddsOnInvoice", true, D("Show the odds, payout and company on a scam invoice's tooltip.", 90));
			ShowAtMailbox = cfg.Bind("General", "ShowOddsAtMailbox", true, D("Show your best odds when you look at a mailbox with scam invoices on you.", 80));

			XpPerSuccess = cfg.Bind("Skill", "XpPerSuccess", 25f, D("XP for a scam that pays. Long shots give up to 1.5 times this.", 90, new AcceptableValueRange<float>(0f, 500f)));
			XpPerFailure = cfg.Bind("Skill", "XpPerFailure", 6f, D("XP for a scam that fails (you still learn something).", 85, new AcceptableValueRange<float>(0f, 200f)));
			MaxDrunkXpBonus = cfg.Bind("State", "MaxDrunkXpBonus", 1f, D("Extra XP for scams sent drunk, scaled by alcohol in blood (1 = up to +100%).", 90, new AcceptableValueRange<float>(0f, 3f)));
			DrunkPenalty = cfg.Bind("State", "DrunkPenalty", 0.4f, D("How much lower your odds are for an invoice mailed completely drunk (0.4 = 40% lower). The Drinking skill shrinks it.", 85, new AcceptableValueRange<float>(0f, 1f)));

			ChancePerLevel = cfg.Bind("Odds", "ChancePerLevel", 0.03f, D("Added to the chance of success per Fraud level.", 100, new AcceptableValueRange<float>(0f, 0.1f)));
			SuspicionPenalty = cfg.Bind("Odds", "SuspicionPenalty", 0.08f, D("Taken off the chance for each scam the same company got from you recently.", 90, new AcceptableValueRange<float>(0f, 0.5f)));
			SuspicionDays = cfg.Bind("Odds", "SuspicionDays", 7, D("How many in-game days a company remembers your scams.", 85, new AcceptableValueRange<int>(1, 30)));

			PayoutPerLevel = cfg.Bind("Payout", "PayoutPerLevel", 0.05f, D("Bigger payouts per Fraud level (0.05 = +5% per level).", 100, new AcceptableValueRange<float>(0f, 0.3f)));
			MinPayoutShare = cfg.Bind("Payout", "MinPayoutShare", 0.4f, D("The smallest payout, as a share of the biggest (the game pays anything from 0).", 90, new AcceptableValueRange<float>(0f, 1f)));
			BaseFreezeChance = cfg.Bind("Payout", "BaseFreezeChance", 0.035f, D("Chance the bank freezes the account after a payment (the game's own number).", 80, new AcceptableValueRange<float>(0f, 1f)));
			FreezeGrowthPer100Rm = cfg.Bind("Payout", "FreezeGrowthPer100Rm", 0.25f, D("The freeze chance grows by this share for every 100 RM your scams have moved through the account.", 75, new AcceptableValueRange<float>(0f, 5f)));

			MinReplyDays = cfg.Bind("Letters", "MinReplyDays", 1f, D("Fewest in-game days before a company writes back.", 100, new AcceptableValueRange<float>(0f, 7f)));
			MaxReplyDays = cfg.Bind("Letters", "MaxReplyDays", 2f, D("Most in-game days before a company writes back.", 95, new AcceptableValueRange<float>(0f, 7f)));
			ReviewChance = cfg.Bind("Letters", "ReviewChance", 0.25f, D("When a scam doesn't pass, the chance the company puts it \"under review\" for one more day first.", 90, new AcceptableValueRange<float>(0f, 1f)));
			DocsChance = cfg.Bind("Letters", "MissingDocumentationChance", 0.3f, D("When a scam doesn't pass, the chance they ask for more documentation instead of reporting you (+2% per Fraud level).", 85, new AcceptableValueRange<float>(0f, 1f)));
			DocsDays = cfg.Bind("Letters", "FollowUpDays", 3, D("In-game days you have to mail a better invoice as the follow-up.", 80, new AcceptableValueRange<int>(1, 14)));

			InvestigationDays = cfg.Bind("Investigation", "InvestigationDays", 3, D("In-game days between being reported and the Fraud charge.", 100, new AcceptableValueRange<int>(1, 14)));
			LayLowDropChance = cfg.Bind("Investigation", "LayLowDropChance", 0.4f, D("If you commit no crimes during an investigation, the chance it's dropped for lack of evidence (+2% per Fraud level).", 90, new AcceptableValueRange<float>(0f, 1f)));
			BribeCostPerSeverity = cfg.Bind("Investigation", "BribeCostPerSeverity", 2f, D("OC a bribe costs per point of the charge's severity. Uses the game's Bribery skill.", 80, new AcceptableValueRange<float>(0f, 50f)));
			PayBackShare = cfg.Bind("Investigation", "PayBackShare", 0.5f, D("Settling (paying back) costs this share of what you tried to take, in RM.", 70, new AcceptableValueRange<float>(0f, 3f)));

			ConArtist = cfg.Bind("Perk", "ConArtist", true, D("Level 10 perk: once a week, a scam that would get you reported asks for more documentation instead.", 100));
			ConArtistDays = cfg.Bind("Perk", "ConArtistDays", 7, D("In-game days between Con Artist saves.", 90, new AcceptableValueRange<int>(1, 30)));
		}

		// ---- Time ----

		internal const int Day = 1440;

		internal static int Now
		{
			get
			{
				try
				{
					TimeAndDay t = TimeOfDayAzure.instance.currentTimeAndDay;
					return t.currentDay * Day + Mathf.FloorToInt(t.hours) * 60 + Mathf.FloorToInt(t.minutes);
				}
				catch (Exception) { return 0; }
			}
		}

		private static string When(int minutes)
		{
			if (minutes <= 0) return "now";
			int d = minutes / Day, h = (minutes % Day) / 60;
			return d > 0 ? d + "d " + h + "h" : h + "h";
		}

		// ---- Odds ----

		internal static ScamMode Mode
		{
			get { return Save != null ? (ScamMode)Mathf.Clamp(Save.mode, 0, 2) : ScamMode.Normal; }
			set { if (Save != null) Save.mode = (int)value; }
		}

		private static float ModeChance(ScamMode m) { return m == ScamMode.Small ? 0.1f : m == ScamMode.Greedy ? -0.15f : 0f; }
		private static float ModePay(ScamMode m) { return m == ScamMode.Small ? 0.6f : m == ScamMode.Greedy ? 1.6f : 1f; }

		internal static int SuspicionOf(string company)
		{
			return Save == null ? 0 : Save.Suspicion(company, Now, SuspicionDays.Value * Day);
		}

		internal static void Odds(float quality, Company c, ScamMode mode, out float chance, out float maxPay)
		{
			float level = Skill.EffectiveLevel;
			chance = quality / 2.7f * c.Check;                       // the game's own odds, by company
			chance += ChancePerLevel.Value * level;
			chance -= SuspicionPenalty.Value * SuspicionOf(c.Name);
			chance += ModeChance(mode);
			chance *= 1f - DrunkPenalty.Value * Skill.Drunkenness;
			chance = Mathf.Clamp(chance, 0.02f, 0.95f);
			maxPay = 100f * quality * c.Pay * ModePay(mode) * (1f + PayoutPerLevel.Value * level);   // the game pays 0 to 100 x quality
			maxPay = Mathf.Max(0.5f, Mathf.Round(maxPay * 100f) / 100f);
		}

		internal static Scam OpenDocs()
		{
			if (Save == null) return null;
			foreach (Scam s in Save.Scams()) if (s.State == ScamState.Docs) return s;
			return null;
		}

		private static string SuspicionWord(int n) { return n <= 0 ? "none" : n == 1 ? "low" : n == 2 ? "medium" : "high"; }

		internal static string OddsText(float quality)
		{
			Scam docs = OpenDocs();
			if (docs != null)
			{
				return "Scam Artist: goes as the follow-up to " + docs.Company + "\n" +
				       (quality >= docs.Required ? "Good enough: they'll pay" : "Not good enough: needs quality " + docs.Required.ToString("0.00", CultureInfo.InvariantCulture) + " or they'll report you");
			}
			Company c = Company.ForQuality(quality);
			float chance, max;
			Odds(quality, c, Mode, out chance, out max);
			float lo = max * MinPayoutShare.Value;
			return "Scam Artist: addressed to " + c.Name + " (" + Mode + ")\n" +
			       "Chance " + Mathf.RoundToInt(chance * 100f) + "%  ·  " + lo.ToString("0.#", CultureInfo.InvariantCulture) + "–" + max.ToString("0.#", CultureInfo.InvariantCulture) + " RM  ·  Suspicion: " + SuspicionWord(SuspicionOf(c.Name));
		}

		private static string CardNote()
		{
			if (Save == null) return null;
			int open = 0, paid = 0;
			foreach (Scam s in Save.Scams()) { if (s.Open) open++; if (s.State == ScamState.Paid) paid++; }
			return open + " open · " + paid + " paid";
		}

		// ---- Mailing ----

		private static readonly List<object> handled = new List<object>();
		internal static NoteInfo frozenNote;

		internal static bool OnInvoiceMailed(FakeInvoiceReceiver receiver, MailController.Mail mail)
		{
			if (!Skill.Enabled.Value || Save == null) return true;   // let the game handle it
			MailController.ItemMail im = mail as MailController.ItemMail;
			if (im == null || im.item == null || receiver.item == null || im.item.ID != receiver.item.ID) return true;
			if (handled.Contains(mail)) return false;
			handled.Add(mail);
			if (handled.Count > 50) handled.RemoveAt(0);
			if (receiver.note != null) frozenNote = receiver.note;

			ItemQualityData q = ItemMetaUtilities.GetMetaOfType<ItemQualityData>(im.meta);
			float quality = q != null ? q.quality : 0.5f;
			int now = Now;
			int count = Mathf.Max(1, im.amount);
			for (int i = 0; i < count; i++) Send(quality, receiver.targetBank ?? "", now);
			Save.Store();
			return false;
		}

		private static void Send(float quality, string bank, int now)
		{
			Scam docs = OpenDocs();
			if (docs != null)
			{
				if (quality >= docs.Required)
				{
					docs.State = ScamState.Pending;
					docs.Chance = 1f;
					docs.DueAt = now + Day;
					Letter(docs.Company, "Thank you for the additional documentation. Your invoice is being processed.");
				}
				else
				{
					StartInvestigation(docs, now, "The documentation you sent does not hold up. This matter has been reported to the Militia.");
				}
				return;
			}
			Company c = Company.ForQuality(quality);
			float chance, max;
			Odds(quality, c, Mode, out chance, out max);
			float days = UnityEngine.Random.Range(MinReplyDays.Value, Mathf.Max(MinReplyDays.Value, MaxReplyDays.Value));
			var s = new Scam
			{
				Id = Save.nextId++, Company = c.Name, Quality = quality, Mode = Mode, Chance = chance, MaxPay = max, Bank = bank,
				SentAt = now, DueAt = now + Mathf.RoundToInt(days * Day), State = ScamState.Pending
			};
			Save.AddSend(c.Name, now);
			Save.Scams().Add(s);
			SkillHost.Notify("Scam sent", c.Name + ": " + Mathf.RoundToInt(chance * 100f) + "% chance, up to " + max.ToString("0.#", CultureInfo.InvariantCulture) + " RM. Expect a letter in " + Mathf.Max(1, Mathf.RoundToInt(days)) + " day(s).");
		}

		// ---- Letters and outcomes ----

		private static void Letter(string from, string text)
		{
			if (Save != null) Save.AddLetter(Now, from, text);
			SkillHost.Notify("Letter from " + from, text);
		}

		private static float nextTick;

		private void Update()
		{
			SkillHost.Tick();
			try
			{
				if (LedgerKey.Value.IsDown() && SkillHost.InGame) ledgerOpen = !ledgerOpen;
				if (Time.unscaledTime < nextTick) return;
				nextTick = Time.unscaledTime + 2f;
				if (SkillHost.InGame && Save != null && Time.timeScale > 0f) Process();
			}
			catch (Exception e) { Logger.LogWarning("Scam Artist: " + e.Message); }
		}

		private static void Process()
		{
			int now = Now;
			bool changed = false;
			foreach (Scam s in Save.Scams().ToArray())
			{
				if (!s.Open || now < s.DueAt) continue;
				changed = true;
				switch (s.State)
				{
					case ScamState.Pending:
					case ScamState.Review:
						Resolve(s, now);
						break;
					case ScamState.Docs:
						s.State = ScamState.Failed;
						Letter(s.Company, "We never received the documentation we asked for. We consider the matter closed.");
						break;
					case ScamState.Investigation:
						EndInvestigation(s);
						break;
				}
			}
			if (changed) Save.Store();
		}

		private static float DrunkXp { get { return 1f + MaxDrunkXpBonus.Value * Skill.Alcohol / 100f; } }

		private static void Resolve(Scam s, int now)
		{
			if (UnityEngine.Random.value < s.Chance)
			{
				Pay(s);
				return;
			}
			if (!s.Reviewed && UnityEngine.Random.value < ReviewChance.Value)
			{
				s.Reviewed = true;
				s.State = ScamState.Review;
				s.DueAt = now + Day;
				Letter(s.Company, "Your invoice is under review. You will hear from us shortly.");
				return;
			}
			Skill.AddXp(XpPerFailure.Value, "scam failed");
			if (UnityEngine.Random.value < DocsChance.Value + 0.02f * Skill.Level)
			{
				AskForDocs(s, now, "We are missing documentation for your invoice. Please send a corrected copy within " + DocsDays.Value + " days.");
				return;
			}
			if (ConArtist.Value && Skill.Level >= CustomSkill.MaxLevel && now - Save.perkUsedAt >= ConArtistDays.Value * Day)
			{
				Save.perkUsedAt = now;
				AskForDocs(s, now, "Con Artist: you talked your way out of it. They want a corrected copy within " + DocsDays.Value + " days.");
				return;
			}
			StartInvestigation(s, now, "Your invoice is fraudulent. Our lawyers have contacted the Militia.");
		}

		private static void AskForDocs(Scam s, int now, string text)
		{
			s.State = ScamState.Docs;
			s.Required = Mathf.Min(2f, s.Quality + 0.2f);
			s.DueAt = now + DocsDays.Value * Day;
			Letter(s.Company, text + " (Needs quality " + s.Required.ToString("0.00", CultureInfo.InvariantCulture) + " or better.)");
		}

		private static void StartInvestigation(Scam s, int now, string text)
		{
			s.State = ScamState.Investigation;
			s.Severity = Mathf.Clamp(Mathf.RoundToInt(s.MaxPay * 0.8f), 10, 300);
			s.DueAt = now + InvestigationDays.Value * Day;
			s.LayLowSince = now;
			Letter(s.Company, text + " Charges in " + InvestigationDays.Value + " days. Open the scam ledger (" + LedgerKey.Value + ") to deal with it.");
		}

		private static bool ourCrime;

		private static void EndInvestigation(Scam s)
		{
			bool layLow = Save.lastCrimeAt < s.LayLowSince;
			if (layLow && UnityEngine.Random.value < LayLowDropChance.Value + 0.02f * Skill.Level)
			{
				s.State = ScamState.Dropped;
				Letter("the Militia", "The investigation into the " + s.Company + " invoice was dropped for lack of evidence.");
				Skill.AddXp(XpPerFailure.Value * 1.5f, "laid low");
				return;
			}
			s.State = ScamState.Charged;
			try
			{
				ourCrime = true;
				Crime.instance.CommitCrime(null, s.Severity, Crime.CrimeType.Fraud);
			}
			catch (Exception e) { SkillHost.LogWarning("Fraud charge: " + e.Message); }
			finally { ourCrime = false; }
			Letter("the Militia", "You are charged with fraud over the " + s.Company + " invoice.");
		}

		private static void Pay(Scam s)
		{
			float amount = UnityEngine.Random.Range(MinPayoutShare.Value, 1f) * s.MaxPay;
			amount = Mathf.Max(0.01f, Mathf.Round(amount * 100f) / 100f);
			string paid = amount.ToString("0.00", CultureInfo.InvariantCulture) + " RM";
			if (string.IsNullOrEmpty(s.Bank))
			{
				Money.instance.AddMoney(amount, Money.Currency.RM);
				Letter(s.Company, "Payment processed: " + paid + ".");
			}
			else
			{
				Money.BankAccountData acc = Money.instance.FindBankAccount(s.Bank);
				if (acc != null && acc.Frozen)
				{
					s.State = ScamState.Failed;
					Letter(s.Company, "Our payment of " + paid + " bounced: your " + s.Bank + " account is frozen.");
					return;
				}
				if (acc == null)
				{
					acc = new Money.BankAccountData { Bankname = s.Bank, RM = 0f };
					Money.instance.bankAccounts.Add(acc);
				}
				acc.RM += amount;
				Save.bankMoved += amount;
				Letter(s.Company, "Payment processed: " + paid + " to your " + s.Bank + " account.");
				float freeze = BaseFreezeChance.Value * (1f + FreezeGrowthPer100Rm.Value * Save.bankMoved / 100f);
				if (UnityEngine.Random.value < Mathf.Min(0.5f, freeze))
				{
					acc.Frozen = true;
					try
					{
						if (frozenNote != null) MailController.instance.SendMail("Stalburg Bank - Suspicious activity detected", frozenNote, MailController.Target.Player);
					}
					catch (Exception) { }
					SkillHost.Notify(s.Bank, "Suspicious activity detected: your account is frozen.");
				}
			}
			s.State = ScamState.Paid;
			float xp = XpPerSuccess.Value * Mathf.Clamp(1.5f - s.Chance, 0.5f, 1.5f) * DrunkXp;
			Skill.AddXp(xp, "scam paid");
		}

		// ---- Investigation actions ----

		internal static int BribeCost(Scam s) { return Mathf.Max(1, Mathf.RoundToInt(s.Severity * BribeCostPerSeverity.Value)); }
		internal static float PayBackCost(Scam s) { return Mathf.Round(s.MaxPay * PayBackShare.Value * 100f) / 100f; }

		internal static void TryBribe(Scam s)
		{
			int cost = BribeCost(s);
			if (Money.instance.OCMoneyAmount < cost) { SkillHost.Notify("Scam Artist", "You need " + cost + " OC for the bribe."); return; }
			s.BribeTried = true;
			Money.instance.RemoveMoney(cost, Money.Currency.OC);
			bool ok = PlayerStats.instance.SkillSuccess(PlayerStats.PlayerSkillType.Bribery, Recipe.CurrentFailChance(0.6f, PlayerStats.PlayerSkillType.Bribery));
			if (ok)
			{
				s.State = ScamState.Dropped;
				Letter("the Militia", "After a generous donation, the " + s.Company + " investigation was closed.");
			}
			else
			{
				s.Severity = Mathf.RoundToInt(s.Severity * 1.25f);
				Letter("the Militia", "Your bribe was refused (and kept). The charge got worse.");
			}
			Save.Store();
		}

		internal static void PayBack(Scam s)
		{
			float cost = PayBackCost(s);
			if (Money.instance.RMoneyAmount < cost) { SkillHost.Notify("Scam Artist", "You need " + cost.ToString("0.00", CultureInfo.InvariantCulture) + " RM to settle."); return; }
			Money.instance.RemoveMoney(cost, Money.Currency.RM);
			s.State = ScamState.Dropped;
			Letter(s.Company, "We accept your settlement of " + cost.ToString("0.00", CultureInfo.InvariantCulture) + " RM. We will not press charges.");
			Save.Store();
		}

		// ---- Ledger window ----

		private static bool ledgerOpen;
		private static Rect win = new Rect(80, 80, 560, 520);
		private static Vector2 scroll;
		private static GUIStyle wrap, head;

		private void OnGUI()
		{
			if (!ledgerOpen || Save == null) return;
			if (!SkillHost.InGame) { ledgerOpen = false; return; }
			if (wrap == null)
			{
				wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
				head = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true };
			}
			win = GUI.Window(0x5CA7, win, DrawLedger, "Scam ledger  (" + LedgerKey.Value + " to close)");
		}

		private static string StateText(Scam s, int now)
		{
			switch (s.State)
			{
				case ScamState.Pending: return s.Chance >= 1f ? "follow-up accepted, paying in " + When(s.DueAt - now) : "waiting for a letter (" + When(s.DueAt - now) + ")";
				case ScamState.Review: return "under review (" + When(s.DueAt - now) + ")";
				case ScamState.Docs: return "<color=#e8c060>needs documentation</color>: mail an invoice of quality " + s.Required.ToString("0.00", CultureInfo.InvariantCulture) + "+ within " + When(s.DueAt - now);
				case ScamState.Investigation: return "<color=#e07060>under investigation</color>, charges in " + When(s.DueAt - now) + " (severity " + s.Severity + ")";
				case ScamState.Paid: return "<color=#80d080>paid</color>";
				case ScamState.Failed: return "failed";
				case ScamState.Dropped: return "dropped";
				case ScamState.Charged: return "<color=#e07060>charged with fraud</color>";
			}
			return "";
		}

		private static void DrawLedger(int id)
		{
			int now = Now;
			bool canClick = Cursor.visible;
			GUILayout.BeginHorizontal();
			GUILayout.Label("Next invoices:", head, GUILayout.Width(110));
			GUI.enabled = canClick;
			foreach (ScamMode m in new[] { ScamMode.Small, ScamMode.Normal, ScamMode.Greedy })
			{
				string label = (Mode == m ? "● " : "") + m + (m == ScamMode.Small ? " (safer)" : m == ScamMode.Greedy ? " (riskier)" : "");
				if (GUILayout.Button(label)) Mode = m;
			}
			GUI.enabled = true;
			GUILayout.EndHorizontal();
			if (!canClick) GUILayout.Label("<i>Open your inventory to use the buttons.</i>", wrap);
			if (Save.lastCrimeAt >= now - Day) GUILayout.Label("<color=#e8c060>You committed a crime recently: you're not laying low.</color>", wrap);

			scroll = GUILayout.BeginScrollView(scroll);
			GUILayout.Label("Scams", head);
			var scams = Save.Scams();
			if (scams.Count == 0) GUILayout.Label("None yet. Make a Scam Invoice at the Paperwork station and mail it.", wrap);
			for (int i = scams.Count - 1; i >= 0; i--)
			{
				Scam s = scams[i];
				GUILayout.Label("<b>" + s.Company + "</b>  ·  " + s.Mode + "  ·  up to " + s.MaxPay.ToString("0.#", CultureInfo.InvariantCulture) + " RM  ·  " + StateText(s, now), wrap);
				if (s.State == ScamState.Investigation)
				{
					GUILayout.BeginHorizontal();
					GUI.enabled = canClick && !s.BribeTried;
					if (GUILayout.Button(s.BribeTried ? "Bribe refused" : "Bribe (" + BribeCost(s) + " OC, Bribery skill)")) TryBribe(s);
					GUI.enabled = canClick;
					if (GUILayout.Button("Settle (" + PayBackCost(s).ToString("0.00", CultureInfo.InvariantCulture) + " RM)")) PayBack(s);
					GUI.enabled = true;
					GUILayout.EndHorizontal();
					GUILayout.Label("<i>Or lay low: commit no crimes and it may be dropped.</i>", wrap);
				}
			}
			GUILayout.Space(8);
			GUILayout.Label("Letters", head);
			var letters = Save.Letters();
			if (letters.Count == 0) GUILayout.Label("No letters yet.", wrap);
			for (int i = letters.Count - 1; i >= 0; i--)
			{
				int at;
				int.TryParse(letters[i][0], out at);
				GUILayout.Label("<b>" + letters[i][1] + "</b> <color=#999999>(" + When(now - at) + " ago)</color>\n" + letters[i][2], wrap);
			}
			GUILayout.EndScrollView();
			GUI.DragWindow();
		}

		// ---- Hooks ----

		[ThreadStatic] private static object[] tooltipMeta;
		[ThreadStatic] private static int tooltipItem;

		internal static class Hooks
		{
			[HarmonyPatch(typeof(FakeInvoiceReceiver), "HandleMail")]
			[HarmonyPrefix]
			private static bool HandleMail_Prefix(FakeInvoiceReceiver __instance, MailController.Mail mail)
			{
				try { return OnInvoiceMailed(__instance, mail); }
				catch (Exception e) { SkillHost.LogWarning("Scam Artist mail: " + e.Message); return true; }
			}

			// Any crime ends laying low (except the mod's own fraud charge).
			[HarmonyPatch(typeof(Crime), "CommitCrime", new Type[] { typeof(NPC.NPCReference), typeof(int), typeof(Crime.CrimeType), typeof(bool) })]
			[HarmonyPrefix]
			private static void CommitCrime_Prefix()
			{
				if (!ourCrime && Save != null) Save.lastCrimeAt = Now;
			}

			[HarmonyPatch(typeof(ToolTip), "ConstructDataString")]
			[HarmonyPrefix]
			private static void Tooltip_Prefix(ItemData itemData, object[] metas)
			{
				tooltipMeta = metas;
				tooltipItem = itemData != null && itemData.item != null ? itemData.item.ID : -1;
			}

			[HarmonyPatch(typeof(ToolTip), "MailableDataToString")]
			[HarmonyPostfix]
			private static void Mailable_Postfix(ItemMailableData mailableData, ref string __result)
			{
				try
				{
					if (!ShowTooltip.Value || !Skill.Enabled.Value || Save == null || mailableData == null) return;
					if (mailableData.mailName != "ScamInvoice" && tooltipItem != 16010) return;
					ItemQualityData q = ItemMetaUtilities.GetMetaOfType<ItemQualityData>(tooltipMeta);
					if (q == null) return;
					__result += "\n" + OddsText(q.quality);
				}
				catch (Exception) { }
			}

			[HarmonyPatch(typeof(Mailbox), "UpdateUseMessage")]
			[HarmonyPostfix]
			private static void Mailbox_Postfix(Mailbox __instance)
			{
				try
				{
					if (!ShowAtMailbox.Value || !Skill.Enabled.Value || Save == null || __instance.mode != Mailbox.Mode.Send) return;
					float best = -1f; int n = 0;
					foreach (SlotController sc in Inventory.instance.GetAllSlotsWithItemWithMeta<ItemMailableData>())
					{
						if (sc == null || sc.itemStack == null) continue;
						ItemMailableData md = sc.itemStack.GetMetaOfType<ItemMailableData>();
						if (md == null || md.mailName != "ScamInvoice") continue;
						ItemQualityData q = sc.itemStack.GetMetaOfType<ItemQualityData>();
						if (q == null) continue;
						n += Mathf.Max(1, sc.itemStack.itemAmount);
						float chance, max;
						Odds(q.quality, Company.ForQuality(q.quality), Mode, out chance, out max);
						if (OpenDocs() != null) chance = q.quality >= OpenDocs().Required ? 1f : 0f;
						best = Mathf.Max(best, chance);
					}
					if (n == 0) return;
					UseText.instance.ChangeUseText(__instance.title, __instance.useMessage + "  ·  " + n + " scam" + (n > 1 ? "s" : "") + ", best chance " + Mathf.RoundToInt(best * 100f) + "%");
				}
				catch (Exception) { }
			}
		}
	}
}
