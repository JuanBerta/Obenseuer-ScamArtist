# Changelog

## 1.0.6
- A frozen bank account is unfrozen on its own after AutoUnfreezeDays (default 7) in-game days, when the bank closes its review. The scam ledger shows the countdown. 0 = off.
- Shipped as part of Skills Pack; this mod is no longer released on its own.

## 1.0.5
- The lawyer (030-112-112) always answers the phone. In the game every number only picks up some of the time, so his line often seemed busy. Setting: LawyerAlwaysAnswers.

## 1.0.4
- Scam ledger: filter tabs for each stage (All, Mail, Waiting, Docs, Reported, Closed) with counts, and sorting (newest first, due soonest, biggest payout). Scams are grouped by stage: reported first, then documentation requests, then those waiting for an answer.
- Invoice tooltip: always shows the invoice's own odds and payout; when companies want documentation, an extra line says whether this invoice is good enough as a follow-up or goes as a new scam.

## 1.0.3
- The lawyer's fee for unfreezing an account is taken from that account, so you don't need the money on you. If the account can't cover it, you get the game's normal invoice. Setting: LawyerPaidFromAccount.

## 1.0.2
- The lawyer unfreezes a frozen bank account right away instead of after a week (you still pay the invoice). Setting: InstantLawyerUnfreeze.
- New option PayIntoFrozenAccount (off by default): scam payments go into a frozen account instead of bouncing, and stay there until it's unfrozen.

## 1.0.1
- Failed scams give more Fraud XP (15 instead of 6), and the drunk XP bonus now applies to failed scams too.
- The setting is now called XpPerFailedScam, so the new value applies even if you had changed the old one.

## 1.0.0
- First release.
