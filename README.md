# Scam Artist

Better scam invoices for Obenseuer, and a new **Fraud** skill.

In the base game a scam invoice is pure luck: it passes or not on one roll, pays anything from 0 RM, and a failure is an instant crime with no explanation. Scam Artist turns it into a small con game that plays out over a few in-game days.

## Features
- **A Fraud skill on the Character tab.** It looks like the game's own skills, starts unlearned, and raises your odds (+3% per level) and payouts (+5% per level). You get XP when a scam pays (more for long shots) and a little when one fails.
- **Know your odds.** The invoice's tooltip shows who it's addressed to, the chance it passes, the payout range, how suspicious that company is and your risk mode. Looking at a mailbox shows your best chance.
- **Fairer payouts.** Quality sets a range (40-100% of the maximum) instead of a 0-to-max lottery. Big companies pay more but check harder.
- **Pick your risk.** In the scam ledger, choose Small (safer, smaller), Normal or Greedy (riskier, much bigger) for the next invoices.
- **It plays out like real mail.** An invoice first travels by mail like in the base game. When it arrives, the company writes back, and the letter goes to your scam ledger (not your mailbox):
  - *Payment processed*: the money goes to your Stalburg Bank account, like in the game.
  - *Under review*: one more day of waiting.
  - *Missing documentation*: send a better invoice within 3 days as the follow-up and they pay.
  - *Reported*: an investigation starts.
- **Follow-ups first.** While companies are waiting for documentation, a mailbox drop sends only the follow-ups: one good-enough invoice per request (the weakest one that's still good enough, so your best ones are saved for the hardest requests). Your other scam invoices stay with you for the next drop. A request no invoice can meet is left alone, so you're never reported for a weak follow-up.
- **Investigations.** A failed scam doesn't give an instant crime. You get a few days to:
  - **bribe** it away (uses the game's Bribery skill; a refused bribe makes it worse),
  - **settle** by paying back part of what you tried to take,
  - or **lay low**: commit no crimes and it may be dropped for lack of evidence.
- **Suspicion.** Every scam on the same company lowers your odds there for a week, so spread them out.
- **Bank freezes.** The more your scams move through the bank account, the likelier it gets frozen.
- **Level 10 perk, Con Artist:** once a week, a scam that would get you reported asks for more documentation instead.
- **Scam ledger (F7):** a window in the game's own style with your scams in the mail, open and closed, newest first, each with its latest letter and the investigation buttons. It frees the mouse and pauses the game's controls while open; Esc or F7 closes it. Closed scams disappear once you've read them.
- Everything is saved with your save game and belongs to that character only.

Every number can be changed in the config file or with ConfigurationManager.

## Install
1. Install BepInEx 6 (Unity.Mono, x64) for Obenseuer.
2. Put `Obenseuer-ScamArtist.dll` in `BepInEx\plugins\Obenseuer-ScamArtist\`.

## Build
`dotnet build -c Release` (set `/p:ObenseuerDir=...` if the game is not in the default Steam folder).

Made by JuanBerta, with AI assistance (Claude).
