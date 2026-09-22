namespace Triply.Api.Entities;

// Currency exchange rate, relative to USD. One row per Currency (PK = CurrencyId,
// same 1:1-via-FK shape as UserPreferences -> AspNetUsers).
//
// RateToUsd = how many USD one unit of this currency is worth
// (e.g. USD -> 1.00, EUR -> 1.08, JOD -> 1.41 as of the last manual update).
//
// Deliberately NOT auto-refreshed by a scheduled job/external API: with only
// 3 fixed currencies a refresh job would add no real benefit. (The codebase's
// only hosted background service is the AI raw-output retention purge — see
// Modules/AI-Orchestration/AiRawOutputRetention.cs — nothing refreshes rates.)
// Update rows manually (or via a future admin action) when rates drift.
//
// Backend-only concern: never read by DatasetContextService, ItineraryPromptBuilder,
// or exposed to the Gemini prompt — the AI never sees or reasons about currency
// conversion (System Instruction rule #3 in both prompt templates: never output
// any price/cost/currency amount).
public class ExchangeRate
{
    public long CurrencyId { get; set; }              // PK, FK -> Currency
    public Currency Currency { get; set; } = default!;
    public decimal RateToUsd { get; set; }
    public DateTime UpdatedAt { get; set; }
}
