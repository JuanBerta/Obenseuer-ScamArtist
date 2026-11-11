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
	[BepInPlugin("JuanBerta.Obenseuer.ScamArtist", "Scam Artist", "1.0.4")]
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
		internal static ConfigEntry<float> ExtraReplyHours;
		internal static ConfigEntry<float> NoTravelMinHours;
		internal static ConfigEntry<float> NoTravelMaxHours;
		internal static ConfigEntry<bool> LetterNotifications;
		internal static ConfigEntry<float> ReviewChance;
		internal static ConfigEntry<float> DocsChance;
		internal static ConfigEntry<int> DocsDays;
		internal static ConfigEntry<int> InvestigationDays;
		internal static ConfigEntry<float> LayLowDropChance;
		internal static ConfigEntry<float> BribeCostPerSeverity;
		internal static ConfigEntry<float> PayBackShare;
		internal static ConfigEntry<float> BaseFreezeChance;
		internal static ConfigEntry<float> FreezeGrowthPer100Rm;
		internal static ConfigEntry<bool> InstantLawyerUnfreeze;
		internal static ConfigEntry<bool> PayIntoFrozenAccount;
		internal static ConfigEntry<bool> LawyerPaidFromAccount;
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
			Harmony.CreateAndPatchAll(typeof(LedgerBlockBool));
			Harmony.CreateAndPatchAll(typeof(LedgerBlockAxis));
			Logger.LogInfo("Scam Artist loaded.");
		}

		private static ConfigDescription D(string t, int o, AcceptableValueBase r = null) { return CustomSkill.D(t, o, r); }

		private static void Bind(ConfigFile cfg)
		{
			LedgerKey = cfg.Bind("General", "LedgerKey", new KeyboardShortcut(KeyCode.F7), D("Opens the scam ledger: your scams, their letters, and what you can do about investigations.", 100));
			ShowTooltip = cfg.Bind("General", "ShowOddsOnInvoice", true, D("Show the odds, payout and company on a scam invoice's tooltip.", 90));
			ShowAtMailbox = cfg.Bind("General", "ShowOddsAtMailbox", true, D("Show your best odds when you look at a mailbox with scam invoices on you.", 80));

			XpPerSuccess = cfg.Bind("Skill", "XpPerSuccess", 25f, D("XP for a scam that pays. Long shots give up to 1.5 times this.", 90, new AcceptableValueRange<float>(0f, 500f)));
			XpPerFailure = cfg.Bind("Skill", "XpPerFailedScam", 15f, D("XP for a scam that fails: you learn from your mistakes. The drunk bonus applies too.", 85, new AcceptableValueRange<float>(0f, 200f)));
			MaxDrunkXpBonus = cfg.Bind("State", "MaxDrunkXpBonus", 1f, D("Extra XP while drunk, scaled by alcohol in blood (1 = up to +100%), for scams that pay and scams that fail.", 90, new AcceptableValueRange<float>(0f, 3f)));
			DrunkPenalty = cfg.Bind("State", "DrunkPenalty", 0.4f, D("How much lower your odds are for an invoice mailed completely drunk (0.4 = 40% lower). The Drinking skill shrinks it.", 85, new AcceptableValueRange<float>(0f, 1f)));

			ChancePerLevel = cfg.Bind("Odds", "ChancePerLevel", 0.03f, D("Added to the chance of success per Fraud level.", 100, new AcceptableValueRange<float>(0f, 0.1f)));
			SuspicionPenalty = cfg.Bind("Odds", "SuspicionPenalty", 0.08f, D("Taken off the chance for each scam the same company got from you recently.", 90, new AcceptableValueRange<float>(0f, 0.5f)));
			SuspicionDays = cfg.Bind("Odds", "SuspicionDays", 7, D("How many in-game days a company remembers your scams.", 85, new AcceptableValueRange<int>(1, 30)));

			PayoutPerLevel = cfg.Bind("Payout", "PayoutPerLevel", 0.05f, D("Bigger payouts per Fraud level (0.05 = +5% per level).", 100, new AcceptableValueRange<float>(0f, 0.3f)));
			MinPayoutShare = cfg.Bind("Payout", "MinPayoutShare", 0.4f, D("The smallest payout, as a share of the biggest (the game pays anything from 0).", 90, new AcceptableValueRange<float>(0f, 1f)));
			BaseFreezeChance = cfg.Bind("Payout", "BaseFreezeChance", 0.035f, D("Chance the bank freezes the account after a payment (the game's own number).", 80, new AcceptableValueRange<float>(0f, 1f)));
			FreezeGrowthPer100Rm = cfg.Bind("Payout", "FreezeGrowthPer100Rm", 0.25f, D("The freeze chance grows by this share for every 100 RM your scams have moved through the account.", 75, new AcceptableValueRange<float>(0f, 5f)));

			ExtraReplyHours = cfg.Bind("Letters", "ExtraReplyHours", 0f, D("Like in the game, an invoice first travels by mail. The company answers when it arrives, plus this many in-game hours.", 100, new AcceptableValueRange<float>(0f, 72f)));
			NoTravelMinHours = cfg.Bind("Letters", "NoTravelMinHours", 6f, D("If an invoice arrives with no travel time (some mailboxes), the fewest hours before the answer.", 96, new AcceptableValueRange<float>(0f, 72f)));
			NoTravelMaxHours = cfg.Bind("Letters", "NoTravelMaxHours", 18f, D("The same, most hours.", 95, new AcceptableValueRange<float>(0f, 72f)));
			LetterNotifications = cfg.Bind("Letters", "LetterNotifications", true, D("Show a short notification when a letter arrives in the scam ledger.", 94));
			ReviewChance = cfg.Bind("Letters", "ReviewChance", 0.25f, D("When a scam doesn't pass, the chance the company puts it \"under review\" for one more day first.", 90, new AcceptableValueRange<float>(0f, 1f)));
			DocsChance = cfg.Bind("Letters", "MissingDocumentationChance", 0.3f, D("When a scam doesn't pass, the chance they ask for more documentation instead of reporting you (+2% per Fraud level).", 85, new AcceptableValueRange<float>(0f, 1f)));
			DocsDays = cfg.Bind("Letters", "FollowUpDays", 3, D("In-game days you have to mail a better invoice as the follow-up.", 80, new AcceptableValueRange<int>(1, 14)));

			InvestigationDays = cfg.Bind("Investigation", "InvestigationDays", 3, D("In-game days between being reported and the Fraud charge.", 100, new AcceptableValueRange<int>(1, 14)));
			LayLowDropChance = cfg.Bind("Investigation", "LayLowDropChance", 0.4f, D("If you commit no crimes during an investigation, the chance it's dropped for lack of evidence (+2% per Fraud level).", 90, new AcceptableValueRange<float>(0f, 1f)));
			BribeCostPerSeverity = cfg.Bind("Investigation", "BribeCostPerSeverity", 2f, D("OC a bribe costs per point of the charge's severity. Uses the game's Bribery skill.", 80, new AcceptableValueRange<float>(0f, 50f)));
			PayBackShare = cfg.Bind("Investigation", "PayBackShare", 0.5f, D("Settling (paying back) costs this share of what you tried to take, in RM.", 70, new AcceptableValueRange<float>(0f, 3f)));

			InstantLawyerUnfreeze = cfg.Bind("Payout", "InstantLawyerUnfreeze", true, D("When you ask the lawyer to unfreeze a bank account, it's unfrozen right away instead of after the game's waiting time. You still get the lawyer's invoice.", 70));
			LawyerPaidFromAccount = cfg.Bind("Payout", "LawyerPaidFromAccount", true, D("The lawyer's fee for unfreezing an account is taken from that account's balance, so you don't need the money on you. If the account can't cover it, you get the game's normal invoice.", 68));
			PayIntoFrozenAccount = cfg.Bind("Payout", "PayIntoFrozenAccount", false, D("Scam payments still go into a frozen account (instead of bouncing). You can't take the money out until the account is unfrozen.", 65));
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

		// Requests for documentation that still need a follow-up invoice, easiest first.
		internal static List<Scam> DocsWaiting()
		{
			var list = new List<Scam>();
			if (Save == null) return list;
			foreach (Scam s in Save.Scams()) if (s.State == ScamState.Docs && !s.FollowUpSent) list.Add(s);
			list.Sort((a, b) => a.Required.CompareTo(b.Required));
			return list;
		}

		// A follow-up in the mail with this quality, if any.
		internal static Scam FollowUpFor(float quality)
		{
			if (Save == null) return null;
			foreach (Scam s in Save.Scams())
				if (s.State == ScamState.Docs && s.FollowUpSent && Mathf.Abs(s.FollowUpQuality - quality) < 0.0005f) return s;
			return null;
		}

		internal struct ScamUnit { public SlotController slot; public float quality; public ItemMailableData mailable; }

		// Every scam invoice you carry, one entry per invoice.
		internal static List<ScamUnit> CarriedInvoices()
		{
			var units = new List<ScamUnit>();
			foreach (SlotController sc in Inventory.instance.GetAllSlotsWithItemWithMeta<ItemMailableData>())
			{
				if (sc == null || sc.itemStack == null) continue;
				ItemMailableData md = sc.itemStack.GetMetaOfType<ItemMailableData>();
				if (md == null || md.isPackage || md.mailName != "ScamInvoice") continue;
				ItemQualityData q = sc.itemStack.GetMetaOfType<ItemQualityData>();
				float quality = q != null ? q.quality : 0.5f;
				for (int i = 0; i < Mathf.Max(1, sc.itemStack.itemAmount); i++) units.Add(new ScamUnit { slot = sc, quality = quality, mailable = md });
			}
			return units;
		}

		// Which invoice answers which request: for each request (easiest first), the weakest invoice that is
		// good enough, so the best ones are kept for the hardest requests. Requests no invoice can meet are left out:
		// a weak follow-up gets you reported, an unanswered request just closes.
		internal static List<KeyValuePair<Scam, ScamUnit>> PlanFollowUps(List<ScamUnit> units)
		{
			var plan = new List<KeyValuePair<Scam, ScamUnit>>();
			var pool = new List<ScamUnit>(units);
			pool.Sort((a, b) => a.quality.CompareTo(b.quality));
			foreach (Scam d in DocsWaiting())
			{
				int pick = pool.FindIndex(u => u.quality >= d.Required);
				if (pick < 0) continue;
				plan.Add(new KeyValuePair<Scam, ScamUnit>(d, pool[pick]));
				pool.RemoveAt(pick);
			}
			return plan;
		}

		private static string SuspicionWord(int n) { return n <= 0 ? "none" : n == 1 ? "low" : n == 2 ? "medium" : "high"; }

		internal static string OddsText(float quality)
		{
			string docs = "";
			var waiting = DocsWaiting();
			if (waiting.Count > 0)
			{
				Scam best = null;
				foreach (Scam d in waiting) if (quality >= d.Required) { best = d; break; }
				docs = "\n" + waiting.Count + " compan" + (waiting.Count > 1 ? "ies want" : "y wants") + " documentation: " +
				       (best != null ? "good enough as the follow-up to " + best.Company + " (needs " + best.Required.ToString("0.00", CultureInfo.InvariantCulture) + ")"
				                     : "not good enough for any of them (easiest needs " + waiting[0].Required.ToString("0.00", CultureInfo.InvariantCulture) + "), so it goes as a new scam") + ".";
			}
			Company c = Company.ForQuality(quality);
			float chance, max;
			Odds(quality, c, Mode, out chance, out max);
			float lo = max * MinPayoutShare.Value;
			return "Scam Artist: addressed to " + c.Name + "\n" +
			       "Chance: " + Mathf.RoundToInt(chance * 100f) + "%\n" +
			       "Payout: " + lo.ToString("0.#", CultureInfo.InvariantCulture) + "–" + max.ToString("0.#", CultureInfo.InvariantCulture) + " RM\n" +
			       "Suspicion: " + SuspicionWord(SuspicionOf(c.Name)) + "\n" +
			       "Risk: " + Mode + " (change it in the ledger, " + LedgerKey.Value + ")" + docs;
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
			// The game's mail travel time is the first phase. If the invoice got here without travelling, wait a while anyway.
			bool travelled = now - lastInsertAt > 30;
			float hours = travelled ? ExtraReplyHours.Value : UnityEngine.Random.Range(NoTravelMinHours.Value, Mathf.Max(NoTravelMinHours.Value, NoTravelMaxHours.Value));
			int count = Mathf.Max(1, im.amount);
			for (int i = 0; i < count; i++) Send(quality, receiver.targetBank ?? "", now, Mathf.RoundToInt(hours * 60f));
			Save.Store();
			return false;
		}

		internal static int lastInsertAt = -100000;

		private static void Send(float quality, string bank, int now, int replyIn)
		{
			Scam docs = FollowUpFor(quality);
			if (docs != null)
			{
				if (quality >= docs.Required)
				{
					docs.State = ScamState.Pending;
					docs.Chance = 1f;
					docs.DueAt = now + Day;
					Letter(docs, docs.Company, "Thank you for the additional documentation. Your invoice is being processed.");
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
			var s = new Scam
			{
				Id = Save.nextId++, Company = c.Name, Quality = quality, Mode = Mode, Chance = chance, MaxPay = max, Bank = bank,
				SentAt = now, DueAt = now + replyIn, State = ScamState.Pending
			};
			Save.AddSend(c.Name, now);
			Save.Scams().Add(s);
		}

		// ---- Letters and outcomes ----

		// Letters go to the scam ledger, not to your mailbox. A short notification says one arrived.
		private static void Letter(Scam s, string from, string text)
		{
			s.From = from;
			s.Letter = text;
			s.LetterAt = Now;
			s.Unread = true;
			if (LetterNotifications.Value) SkillHost.Notify("Scam ledger: new letter", "From " + from + ". Press " + LedgerKey.Value + " to read it.");
		}

		private static float nextTick;

		private void Update()
		{
			SkillHost.Tick();
			try
			{
				if (LedgerKey.Value.IsDown() && SkillHost.InGame) SetLedger(!ledgerOpen);
				else if (ledgerOpen && Input.GetKeyDown(KeyCode.Escape)) closeAtLateUpdate = true;   // keep blocking Esc this frame
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
						if (s.FollowUpSent) { s.DueAt = now + 60; break; }   // the follow-up is still in the mail
						s.State = ScamState.Failed;
						Letter(s, s.Company, "We never received the documentation we asked for. We consider the matter closed.");
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
				Letter(s, s.Company, "Your invoice is under review. You will hear from us shortly.");
				return;
			}
			Skill.AddXp(XpPerFailure.Value * DrunkXp, DrunkXp > 1.01f ? "scam failed, drunk +" + Mathf.RoundToInt((DrunkXp - 1f) * 100f) + "%" : "scam failed");
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
			Letter(s, s.Company, text + " (Needs quality " + s.Required.ToString("0.00", CultureInfo.InvariantCulture) + " or better.)");
		}

		private static void StartInvestigation(Scam s, int now, string text)
		{
			s.State = ScamState.Investigation;
			s.Severity = Mathf.Clamp(Mathf.RoundToInt(s.MaxPay * 0.8f), 10, 300);
			s.DueAt = now + InvestigationDays.Value * Day;
			s.LayLowSince = now;
			Letter(s, s.Company, text + " Charges in " + InvestigationDays.Value + " days. Open the scam ledger (" + LedgerKey.Value + ") to deal with it.");
		}

		private static bool ourCrime;

		private static void EndInvestigation(Scam s)
		{
			bool layLow = Save.lastCrimeAt < s.LayLowSince;
			if (layLow && UnityEngine.Random.value < LayLowDropChance.Value + 0.02f * Skill.Level)
			{
				s.State = ScamState.Dropped;
				Letter(s, "the Militia", "The investigation into the " + s.Company + " invoice was dropped for lack of evidence.");
				Skill.AddXp(XpPerFailure.Value * 1.5f * DrunkXp, "laid low");
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
			Letter(s, "the Militia", "You are charged with fraud over the " + s.Company + " invoice.");
		}

		private static void Pay(Scam s)
		{
			float amount = UnityEngine.Random.Range(MinPayoutShare.Value, 1f) * s.MaxPay;
			amount = Mathf.Max(0.01f, Mathf.Round(amount * 100f) / 100f);
			string paid = amount.ToString("0.00", CultureInfo.InvariantCulture) + " RM";
			if (string.IsNullOrEmpty(s.Bank))
			{
				Money.instance.AddMoney(amount, Money.Currency.RM);
				Letter(s, s.Company, "Payment processed: " + paid + ".");
			}
			else
			{
				Money.BankAccountData acc = Money.instance.FindBankAccount(s.Bank);
				if (acc != null && acc.Frozen && PayIntoFrozenAccount.Value)
				{
					acc.RM += amount;
					Save.bankMoved += amount;
					s.State = ScamState.Paid;
					Letter(s, s.Company, "Payment processed: " + paid + " to your " + s.Bank + " account. The account is frozen: the money stays there until it's unfrozen.");
					Skill.AddXp(XpPerSuccess.Value * Mathf.Clamp(1.5f - s.Chance, 0.5f, 1.5f) * DrunkXp, "scam paid");
					return;
				}
				if (acc != null && acc.Frozen)
				{
					s.State = ScamState.Failed;
					Letter(s, s.Company, "Our payment of " + paid + " bounced: your " + s.Bank + " account is frozen.");
					return;
				}
				if (acc == null)
				{
					acc = new Money.BankAccountData { Bankname = s.Bank, RM = 0f };
					Money.instance.bankAccounts.Add(acc);
				}
				acc.RM += amount;
				Save.bankMoved += amount;
				Letter(s, s.Company, "Payment processed: " + paid + " to your " + s.Bank + " account.");
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
				Letter(s, "the Militia", "After a generous donation, the " + s.Company + " investigation was closed.");
			}
			else
			{
				s.Severity = Mathf.RoundToInt(s.Severity * 1.25f);
				Letter(s, "the Militia", "Your bribe was refused (and kept). The charge got worse.");
			}
			Save.Store();
		}

		internal static void PayBack(Scam s)
		{
			float cost = PayBackCost(s);
			if (Money.instance.RMoneyAmount < cost) { SkillHost.Notify("Scam Artist", "You need " + cost.ToString("0.00", CultureInfo.InvariantCulture) + " RM to settle."); return; }
			Money.instance.RemoveMoney(cost, Money.Currency.RM);
			s.State = ScamState.Dropped;
			Letter(s, s.Company, "We accept your settlement of " + cost.ToString("0.00", CultureInfo.InvariantCulture) + " RM. We will not press charges.");
			Save.Store();
		}

		// ---- Ledger window (styled like the game's own panels) ----

		private static bool ledgerOpen;
		private static Vector2 scroll;
		private static GUIStyle sPanel, sTitle, sSection, sTab, sTabOn, sCard, sCardHead, sCardText, sCardSmall, sButton, sHint, sNew;
		private static Font gameFont;
		private static bool hadFont;
		private static readonly Color Paper = new Color(0.91f, 0.88f, 0.81f), Ink = new Color(0.14f, 0.11f, 0.08f), Ochre = new Color(0.79f, 0.6f, 0.23f), Cream = new Color(0.94f, 0.9f, 0.82f);

		internal static bool LedgerOpen { get { return ledgerOpen; } }
		private static bool closeAtLateUpdate;
		private static CursorLockMode prevLock;
		private static bool prevVisible;
		private static UnityEngine.EventSystems.EventSystem pausedEvents;
		private static GameObject holder;

		// While the ledger is open it owns the mouse: the cursor is free, the game's controls and its own
		// menus underneath don't react, and Esc closes the ledger.
		internal static void SetLedger(bool open)
		{
			if (open == ledgerOpen) return;
			if (open)
			{
				prevLock = Cursor.lockState;
				prevVisible = Cursor.visible;
				try
				{
					pausedEvents = UnityEngine.EventSystems.EventSystem.current;
					if (pausedEvents != null && pausedEvents.enabled) pausedEvents.enabled = false; else pausedEvents = null;
				}
				catch (Exception) { pausedEvents = null; }
				// The game's own way to freeze camera, movement and hotkeys (what its menus use).
				try
				{
					if (holder == null) { holder = new GameObject("ScamArtist_Ledger"); UnityEngine.Object.DontDestroyOnLoad(holder); }
					if (GameController.instance != null) GameController.instance.ControlsDisabled(holder, true, true);
				}
				catch (Exception e) { SkillHost.LogWarning("Ledger controls: " + e.Message); }
			}
			else
			{
				if (Save != null) Save.RemoveSeenClosed();   // read and closed scams disappear
				try { if (pausedEvents != null) pausedEvents.enabled = true; } catch (Exception) { }
				try { if (holder != null && GameController.instance != null) GameController.instance.ControlsEnabled(holder); } catch (Exception) { }
				pausedEvents = null;
				Cursor.lockState = prevLock;
				Cursor.visible = prevVisible;
			}
			ledgerOpen = open;
		}

		private void LateUpdate()
		{
			if (closeAtLateUpdate) { closeAtLateUpdate = false; SetLedger(false); return; }
			if (!ledgerOpen) return;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		private static Texture2D Framed(Color fill, Color edge, int width)
		{
			var t = new Texture2D(8, 8) { hideFlags = HideFlags.HideAndDontSave };
			for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
				t.SetPixel(x, y, x < width || y < width || x >= 8 - width || y >= 8 - width ? edge : fill);
			t.Apply();
			return t;
		}

		private static GUIStyle Box(Color fill, Color edge, int width, GUIStyle from)
		{
			var s = new GUIStyle(from) { border = new RectOffset(3, 3, 3, 3) };
			s.normal.background = s.onNormal.background = Framed(fill, edge, width);
			return s;
		}

		private static string Spaced(string text) { return string.Join(" ", Array.ConvertAll(text.ToUpperInvariant().ToCharArray(), c => c.ToString())); }

		private static void InitStyles()
		{
			if (sPanel != null && sPanel.normal.background != null && sCard.normal.background != null && (gameFont != null || !hadFont)) return;
			gameFont = null;
			foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
			{
				string n = f.name.ToLowerInvariant();
				if (n.Contains("special") || n.Contains("elite") || n.Contains("typewriter") || n.Contains("courier") || n.Contains("american")) { gameFont = f; break; }
			}
			hadFont = gameFont != null;
			sPanel = Box(new Color(0.1f, 0.09f, 0.08f, 0.98f), new Color(0.27f, 0.25f, 0.21f), 2, GUI.skin.window);
			sPanel.padding = new RectOffset(16, 16, 46, 14);
			sTitle = new GUIStyle(GUI.skin.label) { font = gameFont, fontSize = 17, alignment = TextAnchor.MiddleCenter };
			sTitle.normal.textColor = Cream;
			sSection = Box(new Color(0.15f, 0.14f, 0.13f), new Color(0.2f, 0.19f, 0.17f), 1, GUI.skin.label);
			sSection.font = gameFont; sSection.fontSize = 13; sSection.padding = new RectOffset(12, 12, 8, 8); sSection.margin = new RectOffset(0, 0, 12, 6);
			sSection.normal.textColor = Cream;
			sTab = Box(new Color(0.42f, 0.29f, 0.08f), new Color(0.27f, 0.18f, 0.05f), 1, GUI.skin.button);
			sTab.font = gameFont; sTab.fontSize = 12; sTab.padding = new RectOffset(10, 10, 7, 7);
			sTab.normal.textColor = sTab.hover.textColor = new Color(0.98f, 0.93f, 0.8f);
			sTab.hover.background = Framed(new Color(0.6f, 0.42f, 0.12f), new Color(0.27f, 0.18f, 0.05f), 1);
			sTabOn = new GUIStyle(sTab);
			sTabOn.normal.background = sTabOn.hover.background = Framed(Ochre, new Color(0.95f, 0.75f, 0.35f), 1);
			sTabOn.normal.textColor = sTabOn.hover.textColor = Ink;
			sCard = Box(Paper, new Color(0.55f, 0.5f, 0.42f), 1, GUI.skin.box);
			sCard.padding = new RectOffset(12, 12, 9, 9); sCard.margin = new RectOffset(0, 0, 0, 6);
			sCardHead = new GUIStyle(GUI.skin.label) { font = gameFont, fontSize = 14, richText = true, wordWrap = true };
			sCardHead.normal.textColor = Ink;
			sCardText = new GUIStyle(sCardHead) { fontSize = 13 };
			sCardText.normal.textColor = new Color(0.25f, 0.21f, 0.16f);
			sCardSmall = new GUIStyle(sCardHead) { fontSize = 11 };
			sCardSmall.normal.textColor = new Color(0.42f, 0.37f, 0.3f);
			sButton = Box(new Color(0.42f, 0.29f, 0.08f), new Color(0.27f, 0.18f, 0.05f), 1, GUI.skin.button);
			sButton.font = gameFont; sButton.fontSize = 12; sButton.richText = true;
			sButton.normal.textColor = sButton.hover.textColor = new Color(0.98f, 0.93f, 0.8f);
			sButton.hover.background = Framed(new Color(0.6f, 0.42f, 0.12f), new Color(0.27f, 0.18f, 0.05f), 1);
			sHint = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
			sHint.normal.textColor = new Color(0.66f, 0.62f, 0.53f);
			sNew = new GUIStyle(GUI.skin.label) { font = gameFont, fontSize = 11, alignment = TextAnchor.MiddleRight };
			sNew.normal.textColor = new Color(0.7f, 0.2f, 0.12f);
		}

		private void OnGUI()
		{
			if (!ledgerOpen || Save == null) return;
			if (!SkillHost.InGame) { SetLedger(false); return; }
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { closeAtLateUpdate = true; Event.current.Use(); }
			InitStyles();
			float w = Mathf.Min(720f, Screen.width - 40f), h = Mathf.Min(680f, Screen.height - 80f);
			Rect r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
			GUI.Box(r, GUIContent.none, sPanel);
			GUI.Label(new Rect(r.x, r.y + 10f, r.width, 26f), Spaced("Scam ledger"), sTitle);
			GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 46f, r.width - 32f, r.height - 60f));
			DrawLedger();
			GUILayout.EndArea();
		}

		private struct InMail { public string company; public int minutes; }

		private static List<InMail> MailInTransit()
		{
			var list = new List<InMail>();
			try
			{
				foreach (var kv in MailController.instance.unprocessedMail)
				{
					MailController.ItemMail im = kv.Key as MailController.ItemMail;
					if (im == null || im.item == null || im.item.ID != 16010) continue;
					ItemQualityData q = ItemMetaUtilities.GetMetaOfType<ItemQualityData>(im.meta);
					float mq = q != null ? q.quality : 0.5f;
					Scam fu = FollowUpFor(mq);
					string c = fu != null ? fu.Company + " (follow-up)" : Company.ForQuality(mq).Name;
					int n = Mathf.Max(1, im.amount);
					for (int i = 0; i < n; i++) list.Add(new InMail { company = c, minutes = kv.Value != null ? kv.Value.remainingInSeconds / 60 : 0 });
				}
			}
			catch (Exception) { }
			return list;
		}

		private static string Status(Scam s, int now)
		{
			switch (s.State)
			{
				case ScamState.Pending: return s.Chance >= 1f ? "Follow-up accepted: payment in " + When(s.DueAt - now) : "Received: answer in " + When(s.DueAt - now);
				case ScamState.Review: return "Under review: answer in " + When(s.DueAt - now);
				case ScamState.Docs: return "<b>Needs documentation:</b> mail an invoice of quality " + s.Required.ToString("0.00", CultureInfo.InvariantCulture) + "+ within " + When(s.DueAt - now);
				case ScamState.Investigation: return "<b><color=#8a2a18>Under investigation:</color></b> charges in " + When(s.DueAt - now) + " (severity " + s.Severity + ")";
				case ScamState.Paid: return "<b><color=#2f6a22>Paid</color></b>";
				case ScamState.Failed: return "<b>Failed</b>";
				case ScamState.Dropped: return "<b><color=#2f6a22>Dropped</color></b>";
				case ScamState.Charged: return "<b><color=#8a2a18>Charged with fraud</color></b>";
			}
			return "";
		}

		private static void Card(Scam s, int now, bool canClick, bool repaint)
		{
			GUILayout.BeginVertical(sCard);
			GUILayout.BeginHorizontal();
			GUILayout.Label("<b>" + s.Company + "</b>", sCardHead);
			if (s.Unread) GUILayout.Label("NEW LETTER", sNew, GUILayout.Width(110));
			GUILayout.EndHorizontal();
			GUILayout.Label(s.Mode + " risk  ·  up to " + s.MaxPay.ToString("0.#", CultureInfo.InvariantCulture) + " RM  ·  " + Mathf.RoundToInt(Mathf.Min(1f, s.Chance) * 100f) + "% chance", sCardSmall);
			GUILayout.Label(Status(s, now), sCardText);
			if (!string.IsNullOrEmpty(s.Letter))
				GUILayout.Label("<i>From " + s.From + " (" + When(now - s.LetterAt) + " ago):</i>\n\"" + s.Letter + "\"", sCardText);
			if (s.State == ScamState.Investigation)
			{
				GUILayout.BeginHorizontal();
				GUI.enabled = canClick && !s.BribeTried;
				if (GUILayout.Button(s.BribeTried ? "Bribe refused" : "Bribe: " + BribeCost(s) + " OC", sButton)) TryBribe(s);
				GUI.enabled = canClick;
				if (GUILayout.Button("Settle: " + PayBackCost(s).ToString("0.00", CultureInfo.InvariantCulture) + " RM", sButton)) PayBack(s);
				GUI.enabled = true;
				GUILayout.EndHorizontal();
				GUILayout.Label("Or lay low: commit no crimes and it may be dropped.", sCardSmall);
			}
			GUILayout.EndVertical();
			if (repaint) { s.Unread = false; if (!s.Open) s.Seen = true; }
		}

		private static int filter, sortMode;

		private static void FilterTab(int id, string label, int count)
		{
			if (GUILayout.Button(label.ToUpperInvariant() + " " + count, filter == id ? sTabOn : sTab)) { filter = id; scroll = Vector2.zero; }
		}

		private static void SortTab(int id, string label)
		{
			if (GUILayout.Button(label.ToUpperInvariant(), sortMode == id ? sTabOn : sTab)) { sortMode = id; scroll = Vector2.zero; }
		}

		private static void Section(string title, string empty, List<Scam> list, int now, bool canClick, bool repaint)
		{
			GUILayout.Label(Spaced(title) + "   (" + list.Count + ")", sSection);
			if (list.Count == 0) GUILayout.Label(empty, sHint);
			foreach (Scam sc in list) Card(sc, now, canClick, repaint);
		}

		private static void DrawLedger()
		{
			int now = Now;
			bool canClick = true;
			bool repaint = Event.current.type == EventType.Repaint;

			GUILayout.BeginHorizontal();
			GUILayout.Label(Spaced("Next invoices"), sSection, GUILayout.Width(200));
			GUI.enabled = canClick;
			foreach (ScamMode m in new[] { ScamMode.Small, ScamMode.Normal, ScamMode.Greedy })
				if (GUILayout.Button(Spaced(m.ToString()), Mode == m ? sTabOn : sTab)) Mode = m;
			GUI.enabled = true;
			GUILayout.EndHorizontal();
			GUILayout.Label("Small is safer and pays less, Greedy is riskier and pays much more.", sHint);
			if (Save.lastCrimeAt >= now - Day) GUILayout.Label("<color=#e8c060>You committed a crime recently, so you're not laying low.</color>", sHint);

			var mail = MailInTransit();
			var scams = Save.Scams();
			var waitingList = scams.FindAll(x => x.State == ScamState.Pending || x.State == ScamState.Review);
			var docsList = scams.FindAll(x => x.State == ScamState.Docs);
			var invList = scams.FindAll(x => x.State == ScamState.Investigation);
			var closed = scams.FindAll(x => !x.Open);

			// Filter tabs, one per stage, with counts.
			GUILayout.BeginHorizontal();
			FilterTab(0, "All", mail.Count + scams.Count);
			FilterTab(1, "Mail", mail.Count);
			FilterTab(2, "Waiting", waitingList.Count);
			FilterTab(3, "Docs", docsList.Count);
			FilterTab(4, "Reported", invList.Count);
			FilterTab(5, "Closed", closed.Count);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.Label("Sort:", sHint, GUILayout.Width(40));
			SortTab(0, "Newest first");
			SortTab(1, "Due soonest");
			SortTab(2, "Biggest payout");
			GUILayout.EndHorizontal();

			Comparison<Scam> order;
			if (sortMode == 1) order = (x, y) => { int c = x.DueAt.CompareTo(y.DueAt); return c != 0 ? c : y.Id.CompareTo(x.Id); };
			else if (sortMode == 2) order = (x, y) => { int c = y.MaxPay.CompareTo(x.MaxPay); return c != 0 ? c : y.Id.CompareTo(x.Id); };
			else order = (x, y) => { int c = Mathf.Max(y.LetterAt, y.SentAt).CompareTo(Mathf.Max(x.LetterAt, x.SentAt)); return c != 0 ? c : y.Id.CompareTo(x.Id); };
			Comparison<Scam> newest = (x, y) => { int c = Mathf.Max(y.LetterAt, y.SentAt).CompareTo(Mathf.Max(x.LetterAt, x.SentAt)); return c != 0 ? c : y.Id.CompareTo(x.Id); };
			waitingList.Sort(order); docsList.Sort(order); invList.Sort(order); closed.Sort(sortMode == 2 ? order : newest);
			if (sortMode == 1) mail.Sort((x, y) => x.minutes.CompareTo(y.minutes)); else mail.Reverse();   // the game keeps mail oldest first

			scroll = GUILayout.BeginScrollView(scroll);
			if (filter == 0 || filter == 1)
			{
				GUILayout.Label(Spaced("In the mail") + "   (" + mail.Count + ")", sSection);
				if (mail.Count == 0) GUILayout.Label("Nothing on its way. Make a Scam Invoice at the Paperwork station and mail it.", sHint);
				foreach (InMail m in mail)
				{
					GUILayout.BeginVertical(sCard);
					GUILayout.Label("<b>" + m.company + "</b>", sCardHead);
					GUILayout.Label("On its way: arrives in " + When(m.minutes), sCardText);
					GUILayout.EndVertical();
				}
			}
			if (filter == 0 || filter == 4) Section("Reported", "No investigations. Keep it that way.", invList, now, canClick, repaint);
			if (filter == 0 || filter == 3) Section("Needs documentation", "No company is waiting for documentation.", docsList, now, canClick, repaint);
			if (filter == 0 || filter == 2) Section("Waiting for an answer", "No scams waiting for an answer.", waitingList, now, canClick, repaint);
			if ((filter == 0 && closed.Count > 0) || filter == 5)
			{
				GUILayout.Label(Spaced("Closed") + "   (" + closed.Count + ")", sSection);
				GUILayout.Label(closed.Count > 0 ? "Closed scams disappear when you close the ledger." : "No closed scams.", sHint);
				foreach (Scam sc in closed) Card(sc, now, canClick, repaint);
			}
			GUILayout.EndScrollView();
			GUILayout.Label("Press Esc or " + LedgerKey.Value + " to close.", sHint);
		}

		// ---- Hooks ----

		[ThreadStatic] private static object[] tooltipMeta;
		[ThreadStatic] private static int tooltipItem;

		// While companies wait for documentation, a mailbox drop sends only the follow-ups (one good-enough invoice
		// per request) plus any other mail; the other scam invoices stay in your inventory for the next drop.
		// This is the game's Mailbox.InsertMail with that one change.
		internal static bool SendFollowUpsFirst(Mailbox box)
		{
			if (!Skill.Enabled.Value || Save == null || box.mode != Mailbox.Mode.Send) return false;
			var units = CarriedInvoices();
			if (units.Count == 0) return false;
			var plan = PlanFollowUps(units);
			if (plan.Count == 0) return false;

			TaskItemsManager.instance.RemoveAllMail();
			var used = new Dictionary<SlotController, int>();
			foreach (var kv in plan)
			{
				ScamUnit u = kv.Value;
				var mail = new MailController.ItemMail
				{
					mailName = u.mailable.mailName,
					item = new ItemReference(u.slot.itemStack.itemReference.Item),
					meta = Item.CloneMeta(u.slot.itemStack.Meta),
					amount = 1
				};
				if (u.mailable.targetNPC != null) mail.receiver = new NPC.NPCReference(u.mailable.targetNPC.ID);
				MailController.instance.SendMail(mail, MailController.Target.World, u.mailable.useDelay ? MailController.instance.delay : null);
				kv.Key.FollowUpSent = true;
				kv.Key.FollowUpQuality = u.quality;
				int c; used.TryGetValue(u.slot, out c); used[u.slot] = c + 1;
			}
			foreach (var kv in used)
			{
				SlotController sc = kv.Key;
				if (sc.itemStack.itemAmount <= kv.Value) sc.RemoveItem();
				else sc.itemStack.itemAmount -= kv.Value;
			}
			// Other (non-scam) mail goes as usual.
			foreach (SlotController sc in Inventory.instance.GetAllSlotsWithItemWithMeta<ItemMailableData>())
			{
				if (sc == null || sc.itemStack == null) continue;
				ItemMailableData md = sc.itemStack.GetMetaOfType<ItemMailableData>();
				if (md == null) { sc.RemoveItem(); continue; }
				if (md.isPackage || md.mailName == "ScamInvoice") continue;
				var mail = new MailController.ItemMail
				{
					mailName = md.mailName,
					item = new ItemReference(sc.itemStack.itemReference.Item),
					meta = Item.CloneMeta(sc.itemStack.Meta),
					amount = sc.itemStack.itemAmount
				};
				if (md.targetNPC != null) mail.receiver = new NPC.NPCReference(md.targetNPC.ID);
				MailController.instance.SendMail(mail, MailController.Target.World, md.useDelay ? MailController.instance.delay : null);
				sc.RemoveItem();
			}
			try { Traverse.Create(box).Field("onMailInserted").GetValue<Relay>()?.triggerOutputs(); } catch (Exception) { }
			try { Inventory.instance.UpdateAllSlots(); } catch (Exception) { }
			Save.Store();
			int kept = units.Count - plan.Count;
			SkillHost.Notify("Scam Artist", "Mailed " + plan.Count + " follow-up" + (plan.Count > 1 ? "s" : "") + "." + (kept > 0 ? " Your other " + kept + " scam invoice" + (kept > 1 ? "s stay" : " stays") + " with you: drop " + (kept > 1 ? "them" : "it") + " again to send as new scams." : ""));
			return true;
		}

		internal static class Hooks
		{
			[HarmonyPatch(typeof(FakeInvoiceReceiver), "HandleMail")]
			[HarmonyPrefix]
			private static bool HandleMail_Prefix(FakeInvoiceReceiver __instance, MailController.Mail mail)
			{
				try { return OnInvoiceMailed(__instance, mail); }
				catch (Exception e) { SkillHost.LogWarning("Scam Artist mail: " + e.Message); return true; }
			}

			// The lawyer's "unfreeze account" service, paid from the frozen account itself when it can cover the fee.
			// (The game's QueueAction, without the invoice.)
			[HarmonyPatch(typeof(LegalServicesController), "QueueAction")]
			[HarmonyPrefix]
			private static bool QueueAction_Prefix(LegalServicesController __instance, string name)
			{
				try
				{
					if (!LawyerPaidFromAccount.Value || __instance.legalActions == null || __instance.actionQueue == null) return true;
					LegalServicesController.LegalAction action = __instance.legalActions.Find(x => x.name == name);
					if (action == null || action.type != LegalServicesController.LegalAction.ActionType.UnfreezeAccount || __instance.actionQueue.ContainsKey(name)) return true;
					Money.BankAccountData acc = Money.instance.FindBankAccount(action.actionData);
					if (acc == null) return true;
					Money.money price = action.GetPrice();
					float needRm = price.RM + price.OC * Money.instance.OCtoRM;
					float haveRm = acc.RM + acc.OC * Money.instance.OCtoRM;
					if (needRm > 0f && haveRm + 0.001f < needRm) return true;   // not enough in the account: the game's invoice
					// RM first, then OC.
					float fromRm = Mathf.Min(acc.RM, needRm);
					acc.RM -= fromRm;
					float rest = needRm - fromRm;
					if (rest > 0f) acc.OC = Mathf.Max(0, acc.OC - Mathf.CeilToInt(rest / Money.instance.OCtoRM));
					__instance.actionQueue.Add(name, action.Clone());
					try { Achievements.instance.TriggerAchievement("BETTER_CALL_SAMUEL"); } catch (Exception) { }
					if (!InstantLawyerUnfreeze.Value)
						SkillHost.Notify("Legal services", "The lawyer's fee (" + price.GetPricestring() + ") was taken from your " + action.actionData + " account.");
					else
					{
						__instance.actionQueue[name].TriggerAction();
						SkillHost.Notify("Legal services", "Your " + action.actionData + " account is unfrozen. The lawyer's fee (" + price.GetPricestring() + ") was taken from it.");
					}
					return false;
				}
				catch (Exception e) { SkillHost.LogWarning("Lawyer fee: " + e.Message); return true; }
			}

			// The lawyer's "unfreeze account" service: do it now instead of waiting for the game's timer.
			[HarmonyPatch(typeof(LegalServicesController), "QueueAction")]
			[HarmonyPostfix]
			private static void QueueAction_Postfix(LegalServicesController __instance, string name)
			{
				try
				{
					if (!InstantLawyerUnfreeze.Value || __instance.actionQueue == null) return;
					LegalServicesController.LegalAction a;
					if (!__instance.actionQueue.TryGetValue(name, out a) || a == null || a.type != LegalServicesController.LegalAction.ActionType.UnfreezeAccount) return;
					a.TriggerAction();   // unfreezes and takes it off the queue
					SkillHost.Notify("Legal services", "Your lawyer got your " + a.actionData + " account unfrozen right away.");
				}
				catch (Exception e) { SkillHost.LogWarning("Lawyer unfreeze: " + e.Message); }
			}

			[HarmonyPatch(typeof(Mailbox), "InsertMail")]
			[HarmonyPrefix]
			private static bool InsertMail_Prefix(Mailbox __instance)
			{
				lastInsertAt = Now;
				try { return !SendFollowUpsFirst(__instance); }
				catch (Exception e) { SkillHost.LogWarning("Scam Artist mailbox: " + e.Message); return true; }
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
						best = Mathf.Max(best, chance);
					}
					if (n == 0) return;
					var plan = PlanFollowUps(CarriedInvoices());
					if (plan.Count > 0)
					{
						UseText.instance.ChangeUseText(__instance.title, __instance.useMessage + "  \u00b7  " + plan.Count + " follow-up" + (plan.Count > 1 ? "s" : "") + " first, " + (n - plan.Count) + " invoice" + (n - plan.Count == 1 ? "" : "s") + " kept");
						return;
					}
					UseText.instance.ChangeUseText(__instance.title, __instance.useMessage + "  ·  " + n + " scam" + (n > 1 ? "s" : "") + ", best chance " + Mathf.RoundToInt(best * 100f) + "%");
				}
				catch (Exception) { }
			}
		}
	}
}
